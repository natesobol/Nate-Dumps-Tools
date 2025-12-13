using System.IO.Compression;
using System.Text;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/scan", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with archive uploads." });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;

    var extraction = await ExtractAudioMessages(files, keepFiles: false);

    if (extraction.AudioMessages.Count == 0)
    {
        return Results.BadRequest(new
        {
            error = "No supported audio files (.opus, .ogg) were found.",
            warnings = extraction.Warnings
        });
    }

    return Results.Ok(new
    {
        count = extraction.AudioMessages.Count,
        audioMessages = extraction.AudioMessages.Select(a => new
        {
            a.FileName,
            a.RelativePath,
            a.Extension,
            timestamp = a.Timestamp.ToString("o"),
        }),
        warnings = extraction.Warnings
    });
});

app.MapPost("/api/download", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with archive uploads." });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;
    var targetFormat = form["targetFormat"].FirstOrDefault()?.ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(targetFormat) || (targetFormat != "mp3" && targetFormat != "wav"))
    {
        targetFormat = "mp3";
    }

    var extraction = await ExtractAudioMessages(files, keepFiles: true);

    if (extraction.AudioMessages.Count == 0)
    {
        return Results.BadRequest(new
        {
            error = "No supported audio files (.opus, .ogg) were found.",
            warnings = extraction.Warnings
        });
    }

    try
    {
        await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full);
    }
    catch (Exception ex)
    {
        CleanupTemporaryRoot(extraction.RootPath);
        return Results.Problem($"Unable to download FFmpeg binaries: {ex.Message}");
    }

    var outputRoot = Path.Combine(extraction.RootPath, "converted");
    Directory.CreateDirectory(outputRoot);

    var csvBuilder = new StringBuilder();
    csvBuilder.AppendLine("FileName,SourcePath,Timestamp,DurationSeconds");

    var convertedFiles = new List<string>();

    foreach (var message in extraction.AudioMessages)
    {
        try
        {
            var outputPath = Path.Combine(outputRoot, Path.GetFileNameWithoutExtension(message.FileName) + $".{targetFormat}");
            var conversion = await FFmpeg.Conversions.FromSnippet.Convert(message.TempFilePath, outputPath);
            await conversion.Start();

            var mediaInfo = await FFmpeg.GetMediaInfo(outputPath);
            var durationSeconds = mediaInfo.Duration.TotalSeconds;

            convertedFiles.Add(outputPath);
            csvBuilder.AppendLine($"{EscapeCsv(Path.GetFileName(outputPath))},{EscapeCsv(message.RelativePath)},{message.Timestamp:o},{durationSeconds:0.###}");
        }
        catch (Exception ex)
        {
            extraction.Warnings.Add($"Failed to convert {message.FileName}: {ex.Message}");
        }
    }

    if (convertedFiles.Count == 0)
    {
        CleanupTemporaryRoot(extraction.RootPath);
        return Results.Problem("Unable to convert any audio files. See warnings for details.");
    }

    await using var archiveStream = new MemoryStream();
    using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
    {
        foreach (var filePath in convertedFiles)
        {
            var entry = archive.CreateEntry(Path.GetFileName(filePath));
            await using var entryStream = entry.Open();
            await using var fileStream = File.OpenRead(filePath);
            await fileStream.CopyToAsync(entryStream);
        }

        var metadataEntry = archive.CreateEntry("metadata.csv");
        await using var metadataStream = metadataEntry.Open();
        var metadataBytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
        await metadataStream.WriteAsync(metadataBytes);
    }

    archiveStream.Seek(0, SeekOrigin.Begin);

    CleanupTemporaryRoot(extraction.RootPath);

    return Results.File(archiveStream.ToArray(), "application/zip", "voice-notes.zip");
});

app.Run();

static async Task<ExtractionResult> ExtractAudioMessages(IFormFileCollection files, bool keepFiles)
{
    var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".opus", ".ogg" };
    var warnings = new List<string>();
    var audioMessages = new List<AudioMessage>();
    var rootPath = Path.Combine(Path.GetTempPath(), "voice-message-extractor", Guid.NewGuid().ToString());

    Directory.CreateDirectory(rootPath);

    foreach (var file in files)
    {
        if (file.Length == 0)
        {
            warnings.Add($"{file.FileName} was empty.");
            continue;
        }

        var extension = Path.GetExtension(file.FileName);

        if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            await using var zipStream = file.OpenReadStream();
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue; // skip directories
                }

                var entryExtension = Path.GetExtension(entry.FullName);
                if (!supportedExtensions.Contains(entryExtension))
                {
                    continue;
                }

                var destinationFileName = Path.GetFileName(entry.FullName);
                var tempFilePath = Path.Combine(rootPath, Guid.NewGuid().ToString() + entryExtension);

                if (keepFiles)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath)!);
                    await using var entryStream = entry.Open();
                    await using var outputStream = File.Create(tempFilePath);
                    await entryStream.CopyToAsync(outputStream);
                }

                audioMessages.Add(new AudioMessage(
                    FileName: destinationFileName,
                    RelativePath: entry.FullName.Replace('\\', '/'),
                    Extension: entryExtension.Trim('.'),
                    Timestamp: entry.LastWriteTime,
                    TempFilePath: keepFiles ? tempFilePath : string.Empty
                ));
            }
        }
        else if (supportedExtensions.Contains(extension))
        {
            var tempFilePath = Path.Combine(rootPath, Guid.NewGuid().ToString() + extension);

            if (keepFiles)
            {
                await using var inputStream = file.OpenReadStream();
                await using var outputStream = File.Create(tempFilePath);
                await inputStream.CopyToAsync(outputStream);
            }

            audioMessages.Add(new AudioMessage(
                FileName: file.FileName,
                RelativePath: file.FileName,
                Extension: extension.Trim('.'),
                Timestamp: DateTimeOffset.UtcNow,
                TempFilePath: keepFiles ? tempFilePath : string.Empty
            ));
        }
        else
        {
            warnings.Add($"{file.FileName} is not a supported archive or audio format.");
        }
    }

    if (!keepFiles)
    {
        CleanupTemporaryRoot(rootPath);
    }

    return new ExtractionResult(audioMessages, warnings, rootPath);
}

static void CleanupTemporaryRoot(string rootPath)
{
    try
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
    catch
    {
        // best effort cleanup
    }
}

static string EscapeCsv(string value)
{
    if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    return value;
}

record AudioMessage(string FileName, string RelativePath, string Extension, DateTimeOffset Timestamp, string TempFilePath);
record ExtractionResult(List<AudioMessage> AudioMessages, List<string> Warnings, string RootPath);
