using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("instagram", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(20);
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/extract", async Task<IResult> (ExtractionRequest request, IHttpClientFactory httpClientFactory) =>
{
    if (string.IsNullOrWhiteSpace(request.ProfileUrl))
    {
        return Results.BadRequest(new { error = "Please provide an Instagram profile URL." });
    }

    var username = InstagramParsing.ExtractUsername(request.ProfileUrl);

    if (string.IsNullOrWhiteSpace(username))
    {
        return Results.BadRequest(new { error = "That does not look like a valid Instagram profile URL." });
    }

    var client = httpClientFactory.CreateClient("instagram");
    var profileUrl = $"https://www.instagram.com/{username}/";

    string profileContent;
    try
    {
        profileContent = await InstagramParsing.FetchContentAsync(client, profileUrl);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem($"Unable to load the profile page: {ex.Message}", statusCode: (int?)ex.StatusCode ?? (int)HttpStatusCode.BadGateway);
    }

    var shortcodes = InstagramParsing.ParseShortcodes(profileContent).Distinct().ToList();

    if (shortcodes.Count == 0)
    {
        return Results.NotFound(new { error = "No posts were found on that profile. The account may be private or blocked." });
    }

    var posts = new List<PostResult>();
    foreach (var code in shortcodes)
    {
        var postUrl = $"https://www.instagram.com/p/{code}/";
        string? description = null;

        if (request.IncludeDescriptions)
        {
            description = await InstagramParsing.TryFetchDescriptionAsync(client, code);
        }

        posts.Add(new PostResult(postUrl, description));
    }

    var ordered = request.NewestFirst
        ? posts
        : posts.AsEnumerable().Reverse().ToList();

    return Results.Ok(new ExtractionResponse(ordered.Count, ordered));
});

app.Run();

record ExtractionRequest(string ProfileUrl, bool IncludeDescriptions, bool NewestFirst = true);

record PostResult(string Url, string? Description);

record ExtractionResponse(int Count, List<PostResult> Posts);

static class InstagramParsing
{
    private static readonly Regex UsernameRegex = new(
        @"instagram\.com/(?<username>[A-Za-z0-9_.]+)/?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ShortcodeRegex = new(
        @"\"shortcode\"\s*:\s*\"(?<code>[A-Za-z0-9_-]{5,})\"",
        RegexOptions.Compiled);

    private static readonly Regex CaptionRegex = new(
        @"edge_media_to_caption\"?\s*:\s*\{\s*\"edges\"\s*:\s*\[\s*\{\s*\"node\"\s*:\s*\{\s*\"text\"\s*:\s*\"(?<text>.*?)\"",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex OgDescriptionRegex = new(
        @"<meta[^>]+property=\\?\"og:description\\?\"[^>]+content=\\?\"(?<content>[^\"]+?)\\?\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? ExtractUsername(string url)
    {
        var match = UsernameRegex.Match(url);
        return match.Success ? match.Groups["username"].Value : null;
    }

    public static IEnumerable<string> ParseShortcodes(string content)
    {
        return ShortcodeRegex.Matches(content).Select(m => m.Groups["code"].Value);
    }

    public static async Task<string> FetchContentAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public static async Task<string?> TryFetchDescriptionAsync(HttpClient client, string shortcode)
    {
        var postUrl = $"https://www.instagram.com/p/{shortcode}/";

        try
        {
            var content = await FetchContentAsync(client, postUrl);
            var caption = TryParseCaption(content);

            if (!string.IsNullOrWhiteSpace(caption))
            {
                return caption;
            }

            var og = TryParseOgDescription(content);
            return og;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryParseCaption(string content)
    {
        var match = CaptionRegex.Match(content);
        if (!match.Success)
        {
            return null;
        }

        var raw = match.Groups["text"].Value;
        return DecodeJsonString(raw);
    }

    private static string? TryParseOgDescription(string content)
    {
        var match = OgDescriptionRegex.Match(content);
        if (!match.Success)
        {
            return null;
        }

        var raw = WebUtility.HtmlDecode(match.Groups["content"].Value);
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private static string DecodeJsonString(string raw)
    {
        try
        {
            var wrapped = $"\"{raw.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
            return JsonSerializer.Deserialize<string>(wrapped) ?? raw;
        }
        catch
        {
            return raw;
        }
    }
}
