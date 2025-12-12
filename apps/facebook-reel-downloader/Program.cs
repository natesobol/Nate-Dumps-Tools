using System.Net;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract", async Task<IResult> (FacebookExtractRequest request) =>
{
    if (!IsValidFacebookUrl(request.Url))
    {
        return Results.BadRequest(new { error = "Please provide a full Facebook video or Reel URL." });
    }

    try
    {
        using var httpClient = CreateHttpClient(request.Cookie);
        var pageResponse = await httpClient.GetAsync(request.Url);

        if (!pageResponse.IsSuccessStatusCode)
        {
            return Results.BadRequest(new
            {
                error = $"Facebook returned {(int)pageResponse.StatusCode} when fetching the page.",
                status = pageResponse.StatusCode.ToString()
            });
        }

        var html = await pageResponse.Content.ReadAsStringAsync();
        var variants = ExtractVariants(html).ToList();

        if (variants.Count == 0)
        {
            return Results.BadRequest(new
            {
                error = "Could not find downloadable video sources on the provided URL. Ensure the link is public or include a valid session cookie for public group posts."
            });
        }

        foreach (var variant in variants)
        {
            variant.ContentLength = await TryGetContentLength(httpClient, variant.Url);
        }

        return Results.Ok(new
        {
            request.Url,
            variants = variants.Select(v => new
            {
                v.Quality,
                v.Url,
                sizeBytes = v.ContentLength,
                sizeLabel = v.ContentLength.HasValue ? FormatBytes(v.ContentLength.Value) : null
            })
        });
    }
    catch (HttpRequestException httpEx)
    {
        return Results.BadRequest(new { error = "Network error while requesting Facebook.", details = httpEx.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem("Unexpected error extracting video info.", detail: ex.Message);
    }
});

app.MapPost("/api/download", async Task<IResult> (FacebookDownloadRequest request) =>
{
    if (!Uri.TryCreate(request.VideoUrl, UriKind.Absolute, out var videoUri))
    {
        return Results.BadRequest(new { error = "A valid video URL is required." });
    }

    var safeName = SanitizeFileName(string.IsNullOrWhiteSpace(request.FileName)
        ? $"facebook-video-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.mp4"
        : request.FileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
            ? request.FileName
            : request.FileName + ".mp4");

    try
    {
        using var httpClient = CreateHttpClient(request.Cookie);
        var response = await httpClient.GetAsync(videoUri, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            return Results.BadRequest(new
            {
                error = $"Failed to download media. Facebook returned {(int)response.StatusCode}.",
                status = response.StatusCode.ToString()
            });
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "video/mp4";
        var stream = await response.Content.ReadAsStreamAsync();

        return Results.Stream(stream, contentType, fileDownloadName: safeName);
    }
    catch (HttpRequestException httpEx)
    {
        return Results.BadRequest(new { error = "Network error while downloading the video.", details = httpEx.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem("Unexpected error while downloading the video.", detail: ex.Message);
    }
});

app.Run();

static HttpClient CreateHttpClient(string? cookieHeader)
{
    var handler = new SocketsHttpHandler
    {
        CookieContainer = new CookieContainer(),
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true
    };

    if (!string.IsNullOrWhiteSpace(cookieHeader))
    {
        ApplyCookies(handler.CookieContainer, cookieHeader);
    }

    var client = new HttpClient(handler);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

    return client;
}

static void ApplyCookies(CookieContainer container, string cookieHeader)
{
    var entries = cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries);
    foreach (var entry in entries)
    {
        var trimmed = entry.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || !trimmed.Contains('='))
        {
            continue;
        }

        try
        {
            container.SetCookies(new Uri("https://facebook.com"), trimmed);
            container.SetCookies(new Uri("https://www.facebook.com"), trimmed);
        }
        catch
        {
            // Ignore malformed cookies and continue
        }
    }
}

static bool IsValidFacebookUrl(string? url)
{
    if (string.IsNullOrWhiteSpace(url))
    {
        return false;
    }

    return Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Host.Contains("facebook.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Contains("fb.watch", StringComparison.OrdinalIgnoreCase));
}

static IEnumerable<VideoVariant> ExtractVariants(string html)
{
    var variants = new List<VideoVariant>();

    var patterns = new List<(string Quality, string[] Keys)>
    {
        ("HD", new[] { "playable_url_quality_hd", "hd_src", "hd_src_no_ratelimit" }),
        ("SD", new[] { "playable_url", "sd_src", "sd_src_no_ratelimit" })
    };

    foreach (var (quality, keys) in patterns)
    {
        var url = FindFirstUrl(html, keys);
        if (!string.IsNullOrWhiteSpace(url) && variants.All(v => v.Url != url))
        {
            variants.Add(new VideoVariant(quality, url));
        }
    }

    return variants;
}

static string? FindFirstUrl(string html, string[] keys)
{
    foreach (var key in keys)
    {
        var pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*\"(?<url>[^\\\"]+)\"";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return DecodeFacebookUrl(match.Groups["url"].Value);
        }
    }

    return null;
}

static string DecodeFacebookUrl(string raw)
{
    var unescaped = Regex.Unescape(raw);
    unescaped = unescaped.Replace("\\/", "/", StringComparison.Ordinal);
    unescaped = unescaped.Replace("\\u0025", "%", StringComparison.OrdinalIgnoreCase);

    return WebUtility.UrlDecode(unescaped);
}

static async Task<long?> TryGetContentLength(HttpClient client, string url)
{
    try
    {
        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
        if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength.HasValue)
        {
            return response.Content.Headers.ContentLength.Value;
        }
    }
    catch
    {
        // Ignore failures, length is optional metadata
    }

    return null;
}

static string FormatBytes(long bytes)
{
    string[] units = ["B", "KB", "MB", "GB"];
    double size = bytes;
    var index = 0;

    while (size >= 1024 && index < units.Length - 1)
    {
        size /= 1024;
        index++;
    }

    return $"{size:0.##} {units[index]}";
}

static string SanitizeFileName(string fileName)
{
    foreach (var invalidChar in Path.GetInvalidFileNameChars())
    {
        fileName = fileName.Replace(invalidChar, '_');
    }

    return fileName;
}

record FacebookExtractRequest(string Url, string? Cookie);

record FacebookDownloadRequest(string VideoUrl, string? FileName, string? Cookie);

class VideoVariant
{
    public VideoVariant(string quality, string url)
    {
        Quality = quality;
        Url = url;
    }

    public string Quality { get; }

    public string Url { get; }

    public long? ContentLength { get; set; }
}
