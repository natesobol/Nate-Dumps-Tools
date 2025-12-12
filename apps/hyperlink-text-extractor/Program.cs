using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http.Features;
using UglyToad.PdfPig;
using static RegexPatterns;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 50; // 50 MB
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddResponseCompression();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with file uploads." });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;
    var filter = form["filter"].ToString()?.ToLowerInvariant();
    var domain = form["domain"].ToString();

    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .docx, .pdf, .html, .md, or .txt file." });
    }

    var linkResults = new List<LinkResult>();
    var fileSummaries = new List<FileSummary>();

    foreach (var file in files)
    {
        try
        {
            var links = await ExtractLinksAsync(file);
            linkResults.AddRange(links);
            fileSummaries.Add(new FileSummary(file.FileName, links.Count));
        }
        catch (Exception ex)
        {
            fileSummaries.Add(new FileSummary(file.FileName, 0, ex.Message));
        }
    }

    var normalizedDomain = NormalizeHost(domain);

    var classified = linkResults
        .Select(link => link with { IsInternal = IsInternal(link.Url, normalizedDomain) })
        .ToList();

    var filtered = classified.Where(link => ShouldInclude(link.IsInternal, filter)).ToList();

    var totals = new
    {
        all = filtered.Count,
        internalCount = filtered.Count(l => l.IsInternal),
        externalCount = filtered.Count(l => !l.IsInternal)
    };

    return Results.Ok(new
    {
        links = filtered,
        totals,
        files = fileSummaries
    });
});

app.Run();

static bool ShouldInclude(bool isInternal, string? filter)
{
    return filter switch
    {
        "internal" => isInternal,
        "external" => !isInternal,
        _ => true
    };
}

static async Task<List<LinkResult>> ExtractLinksAsync(IFormFile file)
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    await using var memory = new MemoryStream();
    await file.CopyToAsync(memory);
    memory.Position = 0;

    return extension switch
    {
        ".docx" => ExtractFromDocx(memory, file.FileName),
        ".pdf" => ExtractFromPdf(memory, file.FileName),
        ".html" or ".htm" => await ExtractFromHtmlAsync(memory, file.FileName),
        ".md" or ".markdown" => await ExtractFromMarkdownAsync(memory, file.FileName),
        ".txt" => await ExtractFromTextAsync(memory, file.FileName),
        _ => throw new InvalidOperationException("Unsupported file type. Upload .docx, .pdf, .html, .md, or .txt files.")
    };
}

static List<LinkResult> ExtractFromDocx(Stream stream, string fileName)
{
    using var document = WordprocessingDocument.Open(stream, false);
    var mainPart = document.MainDocumentPart;
    var relationships = mainPart?.HyperlinkRelationships.ToDictionary(r => r.Id, r => r.Uri.ToString())
                       ?? new Dictionary<string, string>();

    var links = new List<LinkResult>();

    if (mainPart?.Document.Body is null)
    {
        return links;
    }

    foreach (var hyperlink in mainPart.Document.Body.Descendants<Hyperlink>())
    {
        var text = string.Join("", hyperlink.Descendants<Text>().Select(t => t.Text)).Trim();
        var url = hyperlink.Id != null && relationships.TryGetValue(hyperlink.Id, out var linkUrl)
            ? linkUrl
            : hyperlink.Anchor ?? string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            continue;
        }

        links.Add(new LinkResult(text, url, fileName, IsInternal(url, null)));
    }

    return links;
}

static List<LinkResult> ExtractFromPdf(Stream stream, string fileName)
{
    var results = new List<LinkResult>();

    using var pdf = PdfDocument.Open(stream);
    var builder = new StringBuilder();

    foreach (var page in pdf.GetPages())
    {
        builder.AppendLine(page.Text);
    }

    foreach (Match match in UrlRegex.Matches(builder.ToString()))
    {
        var url = match.Value.Trim();
        results.Add(new LinkResult(url, url, fileName, IsInternal(url, null)));
    }

    return results;
}

static async Task<List<LinkResult>> ExtractFromHtmlAsync(Stream stream, string fileName)
{
    using var reader = new StreamReader(stream);
    var html = await reader.ReadToEndAsync();
    var document = new HtmlDocument();
    document.LoadHtml(html);

    var links = new List<LinkResult>();
    foreach (var anchor in document.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
    {
        var text = HtmlEntity.DeEntitize(anchor.InnerText).Trim();
        var url = anchor.GetAttributeValue("href", string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(url))
        {
            continue;
        }

        links.Add(new LinkResult(text, url, fileName, IsInternal(url, null)));
    }

    return links;
}

static async Task<List<LinkResult>> ExtractFromMarkdownAsync(Stream stream, string fileName)
{
    using var reader = new StreamReader(stream);
    var content = await reader.ReadToEndAsync();
    var links = new List<LinkResult>();

    foreach (Match match in MarkdownLinkRegex.Matches(content))
    {
        var text = match.Groups["text"].Value.Trim();
        var url = match.Groups["url"].Value.Trim();
        links.Add(new LinkResult(text, url, fileName, IsInternal(url, null)));
    }

    foreach (Match match in UrlRegex.Matches(content))
    {
        var url = match.Value.Trim();
        if (!links.Any(l => l.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
        {
            links.Add(new LinkResult(url, url, fileName, IsInternal(url, null)));
        }
    }

    return links;
}

static async Task<List<LinkResult>> ExtractFromTextAsync(Stream stream, string fileName)
{
    using var reader = new StreamReader(stream);
    var content = await reader.ReadToEndAsync();

    var links = new List<LinkResult>();
    foreach (Match match in UrlRegex.Matches(content))
    {
        var url = match.Value.Trim();
        links.Add(new LinkResult(url, url, fileName, IsInternal(url, null)));
    }

    return links;
}

static string? NormalizeHost(string? domain)
{
    if (string.IsNullOrWhiteSpace(domain))
    {
        return null;
    }

    if (!domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
        !domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        domain = "https://" + domain;
    }

    return Uri.TryCreate(domain, UriKind.Absolute, out var uri)
        ? uri.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase)
        : null;
}

static bool IsInternal(string url, string? domain)
{
    if (string.IsNullOrWhiteSpace(url))
    {
        return false;
    }

    if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
    {
        if (!uri.IsAbsoluteUri)
        {
            return true;
        }

        if (domain is not null)
        {
            var host = uri.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase);
            return string.Equals(host, domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);
        }
    }

    return false;
}

record LinkResult(string Text, string Url, string SourceFile, bool IsInternal);

record FileSummary(string FileName, int Count, string? Error = null);

static partial class RegexPatterns
{
    public static readonly Regex UrlRegex = GetUrlRegex();
    public static readonly Regex MarkdownLinkRegex = GetMarkdownRegex();

    [GeneratedRegex(@"https?://[^\s)]+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GetUrlRegex();

    [GeneratedRegex(@"\[(?<text>[^\]]+)\]\((?<url>[^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex GetMarkdownRegex();
}
