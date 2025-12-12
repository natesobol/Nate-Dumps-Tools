using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/export", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with a .pptx or .ppt file." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    var mode = (form["mode"].ToString() ?? "images").ToLowerInvariant();

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "Upload a PowerPoint file to export." });
    }

    if (!file.FileName.EndsWith(".ppt", StringComparison.OrdinalIgnoreCase)
        && !file.FileName.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { error = "Only .ppt and .pptx files are supported." });
    }

    var exportImages = mode is "images" or "both";
    var exportHtml = mode is "html" or "both";

    if (!exportImages && !exportHtml)
    {
        return Results.BadRequest(new { error = "Pick either images, HTML, or both for export." });
    }

    var tempRoot = Path.Combine(Path.GetTempPath(), $"ppt-export-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);

    try
    {
        var inputPath = Path.Combine(tempRoot, Path.GetFileName(file.FileName));
        await using (var inputStream = File.Create(inputPath))
        {
            await file.CopyToAsync(inputStream);
        }

        var zipName = Path.GetFileNameWithoutExtension(file.FileName) + "-slides.zip";
        var pngPaths = new List<string>();
        var htmlRoot = exportHtml ? Path.Combine(tempRoot, "html") : null;
        var htmlFiles = new List<string>();

        if (exportImages)
        {
            var imageOut = Path.Combine(tempRoot, "images");
            Directory.CreateDirectory(imageOut);
            await ConvertWithLibreOffice(inputPath, imageOut, "png", "impress_png_Export");
            pngPaths.AddRange(Directory.GetFiles(imageOut, "*.png", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase));

            if (pngPaths.Count == 0)
            {
                return Results.BadRequest(new { error = "No images were produced. LibreOffice may be missing slide export support." });
            }
        }

        if (exportHtml && htmlRoot is not null)
        {
            Directory.CreateDirectory(htmlRoot);
            await ConvertWithLibreOffice(inputPath, htmlRoot, "html", "impress_html_Export");
            htmlFiles.AddRange(Directory.GetFiles(htmlRoot, "*", SearchOption.AllDirectories)
                .Where(p => !string.Equals(p, inputPath, StringComparison.OrdinalIgnoreCase)));

            if (!htmlFiles.Any(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase)))
            {
                return Results.BadRequest(new { error = "HTML export failed. LibreOffice may not be installed or could not render this deck." });
            }
        }

        await using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var png in pngPaths)
            {
                await AddFileToZip(archive, png, Path.Combine("images", Path.GetFileName(png)));
            }

            foreach (var filePath in htmlFiles)
            {
                var relative = Path.GetRelativePath(htmlRoot!, filePath);
                var entryName = Path.Combine("html", relative);
                await AddFileToZip(archive, filePath, entryName);
            }
        }

        archiveStream.Position = 0;
        return Results.File(archiveStream.ToArray(), "application/zip", zipName);
    }
    catch (Win32Exception)
    {
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    finally
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
            // ignore cleanup failures
        }
    }
});

app.Run();

static async Task ConvertWithLibreOffice(string inputPath, string outputDir, string target, string? filter = null)
{
    var arguments = new List<string>
    {
        "--headless",
        "--nologo",
        "--nodefault",
        "--nofirststartwizard",
        "--norestore",
        "--convert-to",
        string.IsNullOrWhiteSpace(filter) ? target : $"{target}:{filter}",
        "--outdir",
        outputDir,
        inputPath
    };

    var startInfo = new ProcessStartInfo
    {
        FileName = "soffice",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    foreach (var arg in arguments)
    {
        startInfo.ArgumentList.Add(arg);
    }

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start LibreOffice process.");
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        var stderr = await process.StandardError.ReadToEndAsync();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        throw new InvalidOperationException($"LibreOffice conversion failed. Exit code {process.ExitCode}. {stderr} {stdout}");
    }
}

static async Task AddFileToZip(ZipArchive archive, string path, string entryName)
{
    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
    await using var entryStream = entry.Open();
    await using var fileStream = File.OpenRead(path);
    await fileStream.CopyToAsync(entryStream);
}
