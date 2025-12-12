using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/extract", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with a .pptx upload." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "Upload a non-empty .pptx file to continue." });
    }

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (extension != ".pptx")
    {
        return Results.BadRequest(new { error = "Only .pptx files are supported." });
    }

    await using var uploadCopy = new MemoryStream();
    await file.CopyToAsync(uploadCopy);
    uploadCopy.Position = 0;

    var allowedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".webp"
    };

    var extractedImages = new List<(string FileName, byte[] Bytes)>();

    try
    {
        using var archive = new ZipArchive(uploadCopy, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith("ppt/media/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entryExtension = Path.GetExtension(entry.FullName);
            if (!allowedImageExtensions.Contains(entryExtension))
            {
                continue;
            }

            await using var entryStream = entry.Open();
            await using var buffer = new MemoryStream();
            await entryStream.CopyToAsync(buffer);

            var safeName = Path.GetFileName(string.IsNullOrWhiteSpace(entry.Name) ? entry.FullName : entry.Name);
            var sanitizedName = Regex.Replace(safeName, "[^a-zA-Z0-9._-]", "-");
            extractedImages.Add((sanitizedName, buffer.ToArray()));
        }
    }
    catch (InvalidDataException)
    {
        return Results.BadRequest(new { error = "The file is not a valid .pptx package." });
    }

    if (extractedImages.Count == 0)
    {
        return Results.BadRequest(new { error = "No embedded images were found in the presentation." });
    }

    await using var downloadStream = new MemoryStream();
    using (var outputArchive = new ZipArchive(downloadStream, ZipArchiveMode.Create, leaveOpen: true))
    {
        var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var counter = 1;

        foreach (var image in extractedImages)
        {
            var extensionToUse = Path.GetExtension(image.FileName);
            var baseName = Path.GetFileNameWithoutExtension(image.FileName);
            var candidateName = string.IsNullOrWhiteSpace(baseName) ? $"image-{counter}" : baseName;

            var finalName = candidateName;
            while (!uniqueNames.Add(finalName + extensionToUse))
            {
                counter++;
                finalName = $"{candidateName}-{counter}";
            }

            var entry = outputArchive.CreateEntry(finalName + extensionToUse, CompressionLevel.Fastest);
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(image.Bytes);
            counter++;
        }
    }

    downloadStream.Position = 0;
    var safeArchiveName = Regex.Replace(Path.GetFileNameWithoutExtension(file.FileName), "[^a-zA-Z0-9._-]", "-");
    var archiveName = string.IsNullOrWhiteSpace(safeArchiveName) ? "presentation-images.zip" : $"{safeArchiveName}-images.zip";

    return Results.File(downloadStream.ToArray(), "application/zip", archiveName);
});

app.Run();
