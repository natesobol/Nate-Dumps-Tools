using System.Text.RegularExpressions;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.AspNetCore.WebUtilities;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();
builder.Services.AddSingleton<YoutubeClient>();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/streams", async (string url, YoutubeClient client) =>
{
    if (!TryParseVideoId(url, out var videoId))
    {
        return Results.BadRequest(new { error = "Please provide a valid YouTube video URL." });
    }

    var video = await client.Videos.GetAsync(videoId);
    var manifest = await client.Videos.Streams.GetManifestAsync(videoId);

    var muxed = manifest
        .GetMuxedStreams()
        .Where(s => s.Container == Container.Mp4 || s.Container == Container.WebM)
        .OrderByDescending(s => s.VideoQuality.MaxHeight)
        .ThenByDescending(s => s.Bitrate)
        .Select(s => new StreamOption(
            s.Itag,
            $"{s.VideoQualityLabel} · {s.Container.Name.ToUpperInvariant()} {(s.Bitrate.KiloBitsPerSecond / 1000d):F2} Mbps",
            s.VideoQualityLabel,
            s.Container.Name,
            Math.Round(s.Size.MegaBytes, 2)))
        .ToList();

    var audio = manifest
        .GetAudioOnlyStreams()
        .OrderByDescending(a => a.Bitrate)
        .Select(a => new AudioOption(
            a.Itag,
            $"{a.Bitrate.KiloBitsPerSecond:F0} kbps · {a.Container.Name.ToUpperInvariant()}",
            a.Container.Name,
            Math.Round(a.Size.MegaBytes, 2),
            a.Codec))
        .ToList();

    return Results.Ok(new StreamsResponse(
        video.Title,
        video.Author.ChannelTitle,
        video.Duration?.ToString("hh\\:mm\\:ss") ?? "Live",
        video.Thumbnails.GetWithHighestResolution().Url,
        muxed,
        audio));
});

app.MapGet("/api/download", async (
    HttpContext context,
    string url,
    int itag,
    string type,
    string? audioFormat,
    YoutubeClient client) =>
{
    if (!TryParseVideoId(url, out var videoId))
    {
        return Results.BadRequest(new { error = "Please provide a valid YouTube video URL." });
    }

    var video = await client.Videos.GetAsync(videoId);
    var manifest = await client.Videos.Streams.GetManifestAsync(videoId);

    if (string.Equals(type, "video", StringComparison.OrdinalIgnoreCase))
    {
        var streamInfo = manifest.GetMuxedStreams().FirstOrDefault(s => s.Itag == itag);
        if (streamInfo is null)
        {
            return Results.NotFound(new { error = "Selected video stream was not found." });
        }

        var fileName = $"{SanitizeFileName(video.Title)}_{streamInfo.VideoQualityLabel}.{streamInfo.Container.Name}";
        var mime = streamInfo.Container == Container.WebM ? "video/webm" : "video/mp4";
        var stream = await client.Videos.Streams.GetAsync(streamInfo, context.RequestAborted);
        return Results.Stream(stream, mime, fileName);
    }

    if (string.Equals(type, "audio", StringComparison.OrdinalIgnoreCase))
    {
        var audioInfo = manifest.GetAudioOnlyStreams().FirstOrDefault(a => a.Itag == itag);
        if (audioInfo is null)
        {
            return Results.NotFound(new { error = "Selected audio stream was not found." });
        }

        var normalizedFormat = (audioFormat ?? "aac").ToLowerInvariant();

        if (normalizedFormat == "mp3")
        {
            if (!TryFindFfmpeg(out var ffmpegDir))
            {
                return Results.BadRequest(new { error = "MP3 conversion requires FFmpeg to be installed and available on PATH." });
            }

            var fileName = $"{SanitizeFileName(video.Title)}.mp3";
            var tempInput = Path.Combine(Path.GetTempPath(), $"yt-input-{Guid.NewGuid()}.{audioInfo.Container.Name}");
            var tempOutput = Path.Combine(Path.GetTempPath(), $"yt-output-{Guid.NewGuid()}.mp3");

            await using (var source = await client.Videos.Streams.GetAsync(audioInfo, context.RequestAborted))
            await using (var destination = File.Create(tempInput))
            {
                await source.CopyToAsync(destination, context.RequestAborted);
            }

            GlobalFFOptions.Configure(new FFOptions
            {
                BinaryFolder = ffmpegDir,
                TemporaryFilesFolder = Path.GetTempPath()
            });

            await FFMpegArguments
                .FromFileInput(tempInput)
                .OutputToFile(tempOutput, true, options => options
                    .WithAudioCodec(AudioCodec.LibMp3Lame)
                    .WithCustomArgument("-vn"))
                .ProcessAsynchronously(context.RequestAborted);

            var fileStream = File.OpenRead(tempOutput);
            context.Response.RegisterForDisposeAsync(fileStream);
            context.Response.OnCompleted(() =>
            {
                TryDelete(tempInput);
                TryDelete(tempOutput);
                return Task.CompletedTask;
            });

            return Results.Stream(fileStream, "audio/mpeg", fileName);
        }

        var audioExtension = audioInfo.Container == Container.WebM ? "webm" : "m4a";
        var contentType = audioInfo.Container == Container.WebM ? "audio/webm" : "audio/mp4";
        var finalExtension = normalizedFormat == "aac" && audioInfo.Container != Container.WebM ? "aac" : audioExtension;
        var audioFileName = $"{SanitizeFileName(video.Title)}.{finalExtension}";
        var audioStream = await client.Videos.Streams.GetAsync(audioInfo, context.RequestAborted);
        return Results.Stream(audioStream, contentType, audioFileName);
    }

    return Results.BadRequest(new { error = "Type must be either 'video' or 'audio'." });
});

app.Run();

static bool TryParseVideoId(string url, out VideoId videoId)
{
    if (VideoId.TryParse(url, out videoId))
    {
        return true;
    }

    if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Contains("youtube", StringComparison.OrdinalIgnoreCase))
    {
        var query = QueryHelpers.ParseQuery(uri.Query);
        if (query.TryGetValue("v", out var v) && VideoId.TryParse(v.ToString(), out videoId))
        {
            return true;
        }
    }

    videoId = default;
    return false;
}

static string SanitizeFileName(string name)
{
    var cleaned = Regex.Replace(name, "[\\/:*?\"<>|]", " ");
    cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
    return string.IsNullOrWhiteSpace(cleaned) ? "video" : cleaned;
}

static bool TryFindFfmpeg(out string? directory)
{
    var binaryName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
    var searchPaths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

    foreach (var path in searchPaths)
    {
        var candidate = Path.Combine(path, binaryName);
        if (File.Exists(candidate))
        {
            directory = Path.GetDirectoryName(candidate);
            return true;
        }
    }

    directory = null;
    return false;
}

static void TryDelete(string path)
{
    try
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    catch
    {
        // ignored
    }
}

record StreamsResponse(string Title, string Channel, string Duration, string ThumbnailUrl, List<StreamOption> Video, List<AudioOption> Audio);

record StreamOption(int Itag, string Label, string Quality, string Container, double SizeMb);

record AudioOption(int Itag, string Label, string Container, double SizeMb, string Codec);
