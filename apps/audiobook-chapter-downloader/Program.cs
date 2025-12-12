using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/chapters", async Task<IResult> (string? url, IHttpClientFactory httpClientFactory) =>
{
    if (string.IsNullOrWhiteSpace(url))
    {
        return Results.BadRequest(new { error = "Provide a book URL." });
    }

    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        return Results.BadRequest(new { error = "Provide a valid URL." });
    }

    var client = httpClientFactory.CreateClient();

    ChaptersResponse response;
    if (uri.Host.Contains("librivox.org", StringComparison.OrdinalIgnoreCase))
    {
        response = await FetchLibrivoxChaptersAsync(uri, client);
    }
    else if (uri.Host.Contains("archive.org", StringComparison.OrdinalIgnoreCase))
    {
        response = await FetchArchiveChaptersAsync(uri, client);
    }
    else
    {
        return Results.BadRequest(new { error = "Only LibriVox and Archive.org links are supported." });
    }

    return Results.Ok(response);
});

app.MapPost("/api/download", async Task<IResult> (DownloadRequest request, IHttpClientFactory httpClientFactory) =>
{
    if (string.IsNullOrWhiteSpace(request.SourceUrl))
    {
        return Results.BadRequest(new { error = "Provide the original book URL." });
    }

    if (request.Chapters is null || request.Chapters.Count == 0)
    {
        return Results.BadRequest(new { error = "Select at least one chapter to download." });
    }

    var httpClient = httpClientFactory.CreateClient();
    var tempBase = Path.Combine(Path.GetTempPath(), "audiobook-chapter-downloader");
    Directory.CreateDirectory(tempBase);
    var workingFolder = Path.Combine(tempBase, Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(workingFolder);

    var downloadedFiles = new List<string>();

    try
    {
        foreach (var chapter in request.Chapters)
        {
            if (string.IsNullOrWhiteSpace(chapter.AudioUrl))
            {
                return Results.BadRequest(new { error = "One of the selected chapters has no audio URL." });
            }

            var safeTitle = SanitizeFileName(string.IsNullOrWhiteSpace(chapter.Title) ? chapter.Id ?? "chapter" : chapter.Title);
            var outputPath = Path.Combine(workingFolder, safeTitle + ".mp3");

            using var response = await httpClient.GetAsync(chapter.AudioUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return Results.BadRequest(new { error = $"Failed to download {chapter.Title ?? chapter.Id}: {response.StatusCode}" });
            }

            await using (var fs = File.Create(outputPath))
            {
                await response.Content.CopyToAsync(fs);
            }

            downloadedFiles.Add(outputPath);
        }

        if (request.MergeIntoSingleFile)
        {
            if (!await ToolExistsAsync("ffmpeg", "-version"))
            {
                return Results.BadRequest(new { error = "Merging requires ffmpeg to be installed on the server." });
            }

            var listFile = Path.Combine(workingFolder, "files.txt");
            await File.WriteAllLinesAsync(listFile, downloadedFiles.Select(path => $"file '{path.Replace("'", "'\\''")}'"));

            var mergedPath = Path.Combine(workingFolder, "audiobook-merged.mp3");
            var mergeArgs = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{mergedPath}\"";
            var result = await RunCommandAsync("ffmpeg", mergeArgs);

            if (!result.Success || !File.Exists(mergedPath))
            {
                var message = string.IsNullOrWhiteSpace(result.Error) ? "Failed to merge audio." : result.Error;
                return Results.BadRequest(new { error = message });
            }

            var mergedStream = File.OpenRead(mergedPath);
            return Results.File(mergedStream, "audio/mpeg", "audiobook-merged.mp3");
        }
        else
        {
            var zipPath = Path.Combine(workingFolder, "chapters.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var file in downloadedFiles)
                {
                    archive.CreateEntryFromFile(file, Path.GetFileName(file));
                }
            }

            var zipStream = File.OpenRead(zipPath);
            return Results.File(zipStream, "application/zip", "chapters.zip");
        }
    }
    finally
    {
        _ = Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(workingFolder))
                {
                    Directory.Delete(workingFolder, recursive: true);
                }
            }
            catch
            {
                // cleanup best effort
            }
        });
    }
});

app.Run();

static async Task<ChaptersResponse> FetchLibrivoxChaptersAsync(Uri uri, HttpClient client)
{
    var slug = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    if (string.IsNullOrWhiteSpace(slug))
    {
        throw new InvalidOperationException("Could not determine book slug from URL.");
    }

    var bookUrl = $"https://librivox.org/api/feed/audiobooks/?format=json&url_slug={Uri.EscapeDataString(slug)}";
    using var bookResponse = await client.GetAsync(bookUrl);
    bookResponse.EnsureSuccessStatusCode();

    using var bookJson = await JsonDocument.ParseAsync(await bookResponse.Content.ReadAsStreamAsync());
    var books = bookJson.RootElement.GetProperty("books");
    if (books.GetArrayLength() == 0)
    {
        throw new InvalidOperationException("No LibriVox projects found for that URL.");
    }

    var book = books[0];
    var id = book.GetProperty("id").GetString();
    var title = book.GetProperty("title").GetString() ?? "LibriVox Audiobook";

    if (string.IsNullOrWhiteSpace(id))
    {
        throw new InvalidOperationException("Could not determine LibriVox project id.");
    }

    var sectionsUrl = $"https://librivox.org/api/feed/audiotracks/?format=json&project_id={id}";
    using var sectionsResponse = await client.GetAsync(sectionsUrl);
    sectionsResponse.EnsureSuccessStatusCode();

    using var sectionsJson = await JsonDocument.ParseAsync(await sectionsResponse.Content.ReadAsStreamAsync());
    if (!sectionsJson.RootElement.TryGetProperty("sections", out var sections))
    {
        throw new InvalidOperationException("LibriVox response did not include sections.");
    }

    var chapterList = new List<ChapterInfo>();
    foreach (var section in sections.EnumerateArray())
    {
        var chapter = new ChapterInfo
        {
            Id = section.GetProperty("id").GetString(),
            Title = section.GetProperty("title").GetString() ?? "Chapter",
            DurationSeconds = ParseDuration(section.GetProperty("playtime").GetString()),
            AudioUrl = section.GetProperty("listen_url").GetString() ?? string.Empty,
            Source = "LibriVox"
        };

        if (!string.IsNullOrWhiteSpace(chapter.AudioUrl))
        {
            chapterList.Add(chapter);
        }
    }

    return new ChaptersResponse(title, "LibriVox", chapterList);
}

static async Task<ChaptersResponse> FetchArchiveChaptersAsync(Uri uri, HttpClient client)
{
    var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    var identifier = segments.LastOrDefault();
    if (segments.Length >= 2 && segments[^2].Equals("details", StringComparison.OrdinalIgnoreCase))
    {
        identifier = segments[^1];
    }

    if (string.IsNullOrWhiteSpace(identifier))
    {
        throw new InvalidOperationException("Could not determine Archive.org identifier from URL.");
    }

    var metaUrl = $"https://archive.org/metadata/{identifier}";
    using var metadataResponse = await client.GetAsync(metaUrl);
    metadataResponse.EnsureSuccessStatusCode();

    using var metadataJson = await JsonDocument.ParseAsync(await metadataResponse.Content.ReadAsStreamAsync());
    if (!metadataJson.RootElement.TryGetProperty("files", out var files))
    {
        throw new InvalidOperationException("Archive.org response did not include files.");
    }

    var title = metadataJson.RootElement.TryGetProperty("metadata", out var meta) && meta.TryGetProperty("title", out var t)
        ? t.GetString() ?? "Archive.org Audiobook"
        : "Archive.org Audiobook";

    var chapters = new List<ChapterInfo>();
    foreach (var file in files.EnumerateArray())
    {
        var includesMp3 = file.TryGetProperty("format", out var format)
            && (format.GetString()?.Contains("MP3", StringComparison.OrdinalIgnoreCase) ?? false);

        if (!includesMp3)
        {
            continue;
        }

        if (!file.TryGetProperty("name", out var nameElement))
        {
            continue;
        }

        var name = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(name))
        {
            continue;
        }

        var length = file.TryGetProperty("length", out var len) ? len.GetString() : null;
        var trackTitle = file.TryGetProperty("title", out var tt) ? tt.GetString() : Path.GetFileNameWithoutExtension(name);
        var track = new ChapterInfo
        {
            Id = name,
            Title = trackTitle ?? name,
            DurationSeconds = ParseDuration(length),
            AudioUrl = $"https://archive.org/download/{identifier}/{Uri.EscapeDataString(name)}",
            Source = "Archive.org"
        };

        chapters.Add(track);
    }

    if (chapters.Count == 0)
    {
        throw new InvalidOperationException("No MP3 files found for this Archive.org item.");
    }

    return new ChaptersResponse(title, "Archive.org", chapters.OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase).ToList());
}

static int? ParseDuration(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    if (int.TryParse(value, out var seconds))
    {
        return seconds;
    }

    var parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 2 && int.TryParse(parts[0], out var minutes) && double.TryParse(parts[1], out var secPart))
    {
        return (int)Math.Round(minutes * 60 + secPart);
    }

    if (parts.Length == 3 && int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out minutes) && double.TryParse(parts[2], out secPart))
    {
        return (int)Math.Round(hours * 3600 + minutes * 60 + secPart);
    }

    return null;
}

static string SanitizeFileName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    foreach (var ch in invalid)
    {
        name = name.Replace(ch, '_');
    }

    return name.Trim();
}

static async Task<bool> ToolExistsAsync(string fileName, string arguments)
{
    try
    {
        var result = await RunCommandAsync(fileName, arguments);
        return result.Success;
    }
    catch
    {
        return false;
    }
}

static async Task<CommandResult> RunCommandAsync(string fileName, string arguments)
{
    var tcs = new TaskCompletionSource<CommandResult>();
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        },
        EnableRaisingEvents = true
    };

    var stderr = new List<string>();
    var stdout = new List<string>();

    process.OutputDataReceived += (_, data) =>
    {
        if (data.Data is not null)
        {
            stdout.Add(data.Data);
        }
    };

    process.ErrorDataReceived += (_, data) =>
    {
        if (data.Data is not null)
        {
            stderr.Add(data.Data);
        }
    };

    process.Exited += (_, _) =>
    {
        var success = process.ExitCode == 0;
        var errorMessage = success ? null : string.Join("\n", stderr);
        tcs.TrySetResult(new CommandResult(success, errorMessage, string.Join("\n", stdout)));
        process.Dispose();
    };

    if (!process.Start())
    {
        return new CommandResult(false, $"Failed to start {fileName}.", string.Empty);
    }

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(3)));
    if (completedTask != tcs.Task)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignored
        }

        return new CommandResult(false, "Operation timed out.", string.Empty);
    }

    return await tcs.Task;
}

record ChapterInfo
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public int? DurationSeconds { get; init; }
    public string AudioUrl { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}

record ChaptersResponse(string Title, string Source, List<ChapterInfo> Chapters);

record DownloadRequest
{
    public string SourceUrl { get; init; } = string.Empty;
    public List<ChapterInfo> Chapters { get; init; } = new();
    public bool MergeIntoSingleFile { get; init; }
}

record CommandResult(bool Success, string? Error, string StandardOutput);
