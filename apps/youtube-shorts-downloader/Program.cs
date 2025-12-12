using System.Globalization;
using System.Text.RegularExpressions;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Extensions.System;
using FFMpegCore.Arguments;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddSingleton(_ => new YoutubeClient());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/download", async Task<IResult> (DownloadRequest request, YoutubeClient client, HttpContext context) =>
{
    if (string.IsNullOrWhiteSpace(request.Url))
    {
        return Results.BadRequest(new { error = "Please provide a valid YouTube Shorts URL." });
    }

    Video video;
    try
    {
        video = await client.Videos.GetAsync(request.Url);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Unable to resolve the provided YouTube URL.", detail = ex.Message });
    }

    if (video.Duration is null || video.Duration > TimeSpan.FromSeconds(60))
    {
        return Results.BadRequest(new { error = "This tool only supports public Shorts that are 60 seconds or less." });
    }

    var manifest = await client.Videos.Streams.GetManifestAsync(video.Id);
    var muxed = manifest.GetMuxedStreams();

    var desiredContainer = ParseContainer(request.Format);
    var containerFiltered = desiredContainer is null
        ? muxed
        : muxed.Where(s => s.Container == desiredContainer);

    var verticalStreams = containerFiltered
        .Where(s => s.VideoQuality is not null && s.VideoQuality.Width < s.VideoQuality.Height)
        .OrderByDescending(s => s.VideoQuality.Height)
        .ThenByDescending(s => s.Bitrate);

    var fallbackStreams = containerFiltered
        .OrderByDescending(s => s.VideoQuality.Height)
        .ThenByDescending(s => s.Bitrate);

    var selected = verticalStreams.FirstOrDefault() ?? fallbackStreams.FirstOrDefault();

    if (selected is null)
    {
        return Results.BadRequest(new { error = "No downloadable streams were found for this Shorts URL." });
    }

    var extension = selected.Container.Name;
    var downloadName = SanitizeFileName(video.Title);
    downloadName = string.IsNullOrWhiteSpace(downloadName) ? $"shorts.{extension}" : $"{downloadName}.{extension}";

    var tempVideoPath = Path.Combine(Path.GetTempPath(), $"short-{Guid.NewGuid():N}.{extension}");
    await client.Videos.Streams.DownloadAsync(selected, tempVideoPath);

    var outputPath = tempVideoPath;
    var appliedSquareCrop = false;

    if (request.SquareCrop)
    {
        try
        {
            outputPath = await EnsureSquareCropAsync(tempVideoPath, request.Format ?? extension, app.Environment.ContentRootPath);
            appliedSquareCrop = true;
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new
            {
                error = "Square crop failed. Ensure FFmpeg binaries are reachable and try again.",
                detail = ex.Message
            });
        }
    }

    context.Response.Headers.Append("X-Video-Title", video.Title);
    context.Response.Headers.Append("X-Video-Duration", video.Duration.Value.TotalSeconds.ToString("0", CultureInfo.InvariantCulture));
    context.Response.Headers.Append("X-Video-Orientation", selected.VideoQuality.Width < selected.VideoQuality.Height ? "vertical" : "horizontal");
    context.Response.Headers.Append("X-Download-FileName", appliedSquareCrop
        ? Path.ChangeExtension(downloadName, request.Format ?? extension)
        : downloadName);

    var mime = selected.Container == Container.WebM ? "video/webm" : "video/mp4";
    var stream = File.OpenRead(outputPath);

    context.Response.RegisterForDisposeAsync(stream);
    context.Response.OnCompleted(() =>
    {
        try
        {
            if (File.Exists(tempVideoPath))
            {
                File.Delete(tempVideoPath);
            }

            if (appliedSquareCrop && File.Exists(outputPath) && outputPath != tempVideoPath)
            {
                File.Delete(outputPath);
            }
        }
        catch
        {
            // best effort cleanup
        }

        return Task.CompletedTask;
    });

    return Results.File(stream, mime, fileDownloadName: context.Response.Headers["X-Download-FileName"].ToString());
});

app.Run();

static Container? ParseContainer(string? format) => format?.ToLowerInvariant() switch
{
    "mp4" => Container.Mp4,
    "webm" => Container.WebM,
    _ => null
};

static string SanitizeFileName(string input)
{
    var invalid = string.Concat(Path.GetInvalidFileNameChars()) + "\\r\\n";
    var regex = new Regex($"[{Regex.Escape(invalid)}]+", RegexOptions.Compiled);
    var cleaned = regex.Replace(input, " ").Trim();
    return string.IsNullOrWhiteSpace(cleaned) ? "shorts" : cleaned.Truncate(80);
}

static async Task<string> EnsureSquareCropAsync(string inputPath, string preferredFormat, string contentRoot)
{
    var ffmpegDir = Path.Combine(contentRoot, "ffmpeg-binaries");
    Directory.CreateDirectory(ffmpegDir);

    GlobalFFOptions.Configure(new FFOptions
    {
        BinaryFolder = ffmpegDir,
        TemporaryFilesFolder = Path.GetTempPath()
    });

    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full, ffmpegDir);

    var analysis = await FFProbe.AnalyseAsync(inputPath);
    var videoStream = analysis.VideoStreams.FirstOrDefault();

    if (videoStream is null || videoStream.Width is null || videoStream.Height is null)
    {
        throw new InvalidOperationException("Unable to read video dimensions for cropping.");
    }

    var squareSize = Math.Min(videoStream.Width.Value, videoStream.Height.Value);
    var outputExt = ParseContainer(preferredFormat) == Container.WebM ? "webm" : "mp4";
    var outputPath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(inputPath)}-square.{outputExt}");

    await FFMpegArguments
        .FromFileInput(inputPath)
        .OutputToFile(outputPath, true, options => options
            .WithVideoFilters(filterOptions => filterOptions.Crop(squareSize, squareSize))
            .WithAudioCodec(AudioCodec.Aac)
            .WithVideoCodec(outputExt.Equals("webm", StringComparison.OrdinalIgnoreCase)
                ? VideoCodec.LibVpx
                : VideoCodec.LibX264)
            .ForceFormat(outputExt))
        .ProcessAsynchronously();

    return outputPath;
}

public record DownloadRequest(string Url, string? Format, bool SquareCrop);
