using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

var youtube = new YoutubeClient();
var httpClient = new HttpClient
{
    DefaultRequestHeaders =
    {
        UserAgent = { ProductInfoHeaderValue.Parse("PlaylistDownloader/1.0") }
    }
};

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/playlist/info", async Task<IResult> (PlaylistRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.PlaylistUrl))
    {
        return Results.BadRequest(new { error = "Provide a public YouTube playlist URL." });
    }

    Playlist playlist;
    try
    {
        playlist = await youtube.Playlists.GetAsync(request.PlaylistUrl);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Unable to resolve playlist: {ex.Message}" });
    }

    var videos = new List<PlaylistVideoInfo>();

    await foreach (var video in youtube.Playlists.GetVideosAsync(playlist.Id))
    {
        var manifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
        var muxed = manifest
            .GetMuxedStreams()
            .Where(s => s.VideoQuality is not null)
            .OrderByDescending(s => s.VideoQuality.MaxHeight)
            .GroupBy(s => s.VideoQuality.MaxHeight)
            .Select(g => g.First())
            .ToList();

        var resolutions = muxed
            .Select(s => new StreamOption(
                s.VideoQuality.MaxHeight,
                s.VideoQuality.Label,
                s.Container.Name))
            .ToList();

        videos.Add(new PlaylistVideoInfo(
            video.Id,
            video.Title,
            video.Duration,
            video.Url,
            resolutions));
    }

    var response = new PlaylistInfoResponse(
        playlist.Title,
        playlist.Author?.Title ?? "",
        videos.Count,
        videos.OrderBy(v => v.Title, StringComparer.OrdinalIgnoreCase).ToList());

    return Results.Ok(response);
});

app.MapPost("/api/playlist/download", async Task<IResult> (DownloadRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.PlaylistUrl))
    {
        return Results.BadRequest(new { error = "Provide a playlistUrl." });
    }

    if (request.Selections.Count == 0)
    {
        return Results.BadRequest(new { error = "Select at least one video to download." });
    }

    Playlist playlist;
    try
    {
        playlist = await youtube.Playlists.GetAsync(request.PlaylistUrl);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Unable to resolve playlist: {ex.Message}" });
    }

    var safePlaylistName = SanitizeFileName(string.IsNullOrWhiteSpace(playlist.Title)
        ? "playlist-download"
        : playlist.Title);

    var requireZip = request.ZipBundle || request.IncludeCsv || request.Selections.Count > 1;

    if (!requireZip)
    {
        var selection = request.Selections[0];
        var video = await youtube.Videos.GetAsync(selection.VideoId);
        var stream = await DownloadVideoAsync(youtube, httpClient, selection.VideoId, selection.Resolution);
        var name = SanitizeFileName(video.Title);
        return Results.File(stream.ToArray(), "video/mp4", $"{name}.mp4");
    }

    await using var zipStream = new MemoryStream();
    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
    {
        var csvBuilder = new StringBuilder();
        if (request.IncludeCsv)
        {
            csvBuilder.AppendLine("Title,VideoUrl");
        }

        foreach (var selection in request.Selections)
        {
            var video = await youtube.Videos.GetAsync(selection.VideoId);
            var videoStream = await DownloadVideoAsync(youtube, httpClient, selection.VideoId, selection.Resolution);
            var fileName = SanitizeFileName(video.Title);

            var entry = archive.CreateEntry($"{fileName}.mp4", CompressionLevel.Fastest);
            await using (var entryStream = entry.Open())
            {
                videoStream.Position = 0;
                await videoStream.CopyToAsync(entryStream);
            }

            if (request.IncludeCsv)
            {
                csvBuilder.AppendLine($"{EscapeCsv(video.Title)},{video.Url}");
            }
        }

        if (request.IncludeCsv)
        {
            var csvEntry = archive.CreateEntry("playlist.csv", CompressionLevel.Fastest);
            await using var writer = new StreamWriter(csvEntry.Open(), Encoding.UTF8);
            await writer.WriteAsync(csvBuilder.ToString());
        }
    }

    zipStream.Position = 0;
    return Results.File(zipStream.ToArray(), "application/zip", $"{safePlaylistName}.zip");
});

app.Run();

static async Task<MemoryStream> DownloadVideoAsync(YoutubeClient client, HttpClient httpClient, string videoId, int? preferredHeight)
{
    var manifest = await client.Videos.Streams.GetManifestAsync(videoId);
    var muxed = manifest
        .GetMuxedStreams()
        .Where(s => s.VideoQuality is not null)
        .OrderByDescending(s => s.VideoQuality.MaxHeight)
        .ToList();

    if (muxed.Count == 0)
    {
        throw new InvalidOperationException("No muxed streams available for this video.");
    }

    MuxedStreamInfo stream = muxed.First();
    if (preferredHeight.HasValue)
    {
        stream = muxed.FirstOrDefault(s => s.VideoQuality.MaxHeight == preferredHeight) ?? stream;
    }

    await using var content = await httpClient.GetStreamAsync(stream.Url);
    var buffer = new MemoryStream();
    await content.CopyToAsync(buffer);
    buffer.Position = 0;
    return buffer;
}

static string SanitizeFileName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray());
    return string.IsNullOrWhiteSpace(cleaned) ? "video" : cleaned;
}

static string EscapeCsv(string value)
{
    var escaped = value.Replace("\"", "\"\"");
    var needsQuotes = escaped.Contains('"') || escaped.Contains(',') || escaped.Contains('\n');
    return needsQuotes ? $"\"{escaped}\"" : escaped;
}

public record PlaylistRequest(string PlaylistUrl);
public record DownloadRequest
{
    public string PlaylistUrl { get; set; } = string.Empty;
    public List<VideoSelection> Selections { get; set; } = new();
    public bool ZipBundle { get; set; } = true;
    public bool IncludeCsv { get; set; }
        = false;
}

public record VideoSelection
{
    public string VideoId { get; set; } = string.Empty;
    public int? Resolution { get; set; }
        = null;
}

public record PlaylistInfoResponse(string Title, string Author, int VideoCount, List<PlaylistVideoInfo> Videos);
public record PlaylistVideoInfo(string Id, string Title, TimeSpan? Duration, string Url, List<StreamOption> Resolutions);
public record StreamOption(int Height, string Label, string Container);
