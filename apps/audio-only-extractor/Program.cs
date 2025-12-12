using System.Diagnostics;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract-audio", async Task<IResult> (AudioExtractionRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Url))
    {
        return Results.BadRequest(new { error = "Provide a video URL." });
    }

    if (!IsSupportedPlatform(request.Url))
    {
        return Results.BadRequest(new { error = "Only YouTube, Vimeo, or TikTok links are supported." });
    }

    var format = request.Format?.Trim().ToLowerInvariant();
    if (format is null || !SupportedFormats.All.Contains(format))
    {
        return Results.BadRequest(new { error = "Choose a supported format: mp3, wav, or aac." });
    }

    if (request.TrimStartSeconds is < 0)
    {
        return Results.BadRequest(new { error = "Trim start must be zero or greater." });
    }

    if (request.TrimEndSeconds is < 0)
    {
        return Results.BadRequest(new { error = "Trim end must be zero or greater." });
    }

    if (request.TrimStartSeconds is not null && request.TrimEndSeconds is not null && request.TrimEndSeconds <= request.TrimStartSeconds)
    {
        return Results.BadRequest(new { error = "Trim end must be greater than trim start when both are provided." });
    }

    if (!await ToolExistsAsync("yt-dlp", "--version"))
    {
        return Results.BadRequest(new { error = "yt-dlp is required on the server to download audio." });
    }

    if (!await ToolExistsAsync("ffmpeg", "-version"))
    {
        return Results.BadRequest(new { error = "ffmpeg is required on the server to transcode audio." });
    }

    var workingDirectory = Path.Combine(Path.GetTempPath(), "audio-only-extractor");
    Directory.CreateDirectory(workingDirectory);

    var jobId = Guid.NewGuid().ToString("N");
    var downloadBase = Path.Combine(workingDirectory, jobId);
    var downloadTemplate = downloadBase + ".%(ext)s";

    var downloadResult = await RunCommandAsync("yt-dlp", $"-f bestaudio --no-playlist --no-progress -o \"{downloadTemplate}\" \"{request.Url}\"");

    if (!downloadResult.Success)
    {
        var error = string.IsNullOrWhiteSpace(downloadResult.Error) ? "Unknown error." : downloadResult.Error;
        return Results.BadRequest(new { error = $"Failed to download audio: {error}" });
    }

    var downloadedFile = Directory.GetFiles(workingDirectory, jobId + ".*").FirstOrDefault();

    if (downloadedFile is null)
    {
        return Results.BadRequest(new { error = "Download did not produce an audio file." });
    }

    var outputPath = Path.Combine(workingDirectory, $"{jobId}-clean.{format}");
    var ffmpegArguments = BuildFfmpegArguments(downloadedFile, outputPath, format, request);

    var ffmpegResult = await RunCommandAsync("ffmpeg", ffmpegArguments);

    if (!ffmpegResult.Success || !File.Exists(outputPath))
    {
        var message = ffmpegResult.Error ?? "Failed to transcode audio.";
        return Results.BadRequest(new { error = message });
    }

    var downloadName = GetDownloadFileName(request.Url, format);
    var stream = File.OpenRead(outputPath);

    return Results.File(stream, GetMimeType(format), downloadName);
});

app.Run();

static bool IsSupportedPlatform(string url)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        return false;
    }

    var host = uri.Host.ToLowerInvariant();
    return host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
        || host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)
        || host.Contains("vimeo.com", StringComparison.OrdinalIgnoreCase)
        || host.Contains("tiktok.com", StringComparison.OrdinalIgnoreCase);
}

static string GetMimeType(string format) => format.ToLowerInvariant() switch
{
    "mp3" => "audio/mpeg",
    "wav" => "audio/wav",
    "aac" => "audio/aac",
    _ => "application/octet-stream"
};

static string GetDownloadFileName(string url, string format)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        return $"audio.{format}";
    }

    var slug = uri.Segments.LastOrDefault()?.Trim('/') ?? "audio";
    if (string.IsNullOrWhiteSpace(slug))
    {
        slug = uri.Host.Replace('.', '-');
    }

    return $"{slug}-audio.{format}";
}

static string BuildFfmpegArguments(string input, string output, string format, AudioExtractionRequest request)
{
    var args = new List<string>
    {
        "-y",
        $"-i \"{input}\"",
        "-vn"
    };

    if (request.TrimStartSeconds is not null)
    {
        args.Add($"-ss {request.TrimStartSeconds.Value.ToString(CultureInfo.InvariantCulture)}");
    }

    if (request.TrimEndSeconds is not null)
    {
        args.Add($"-to {request.TrimEndSeconds.Value.ToString(CultureInfo.InvariantCulture)}");
    }

    var filters = new List<string>();
    if (request.NormalizeLevels)
    {
        filters.Add("loudnorm");
    }

    if (filters.Count > 0)
    {
        args.Add($"-af \"{string.Join(',', filters)}\"");
    }

    args.Add($"-acodec {GetCodec(format)}");
    args.Add($"\"{output}\"");

    return string.Join(' ', args);
}

static string GetCodec(string format) => format.ToLowerInvariant() switch
{
    "mp3" => "libmp3lame",
    "aac" => "aac",
    "wav" => "pcm_s16le",
    _ => "copy"
};

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

record AudioExtractionRequest
{
    public string Url { get; init; } = string.Empty;
    public string? Format { get; init; }
    public bool NormalizeLevels { get; init; }
    public double? TrimStartSeconds { get; init; }
    public double? TrimEndSeconds { get; init; }
}

record CommandResult(bool Success, string? Error, string StandardOutput);

static class SupportedFormats
{
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "wav", "aac"
    };
}
