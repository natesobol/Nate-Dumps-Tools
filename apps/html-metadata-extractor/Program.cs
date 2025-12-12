using System.Text;
using HtmlAgilityPack;

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

app.MapPost("/api/extract", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with one or more HTML uploads." });
    }

    var form = await request.ReadFormAsync();
    var includeKeywords = TryParseBool(form["includeKeywords"], false);
    var includeOpenGraph = TryParseBool(form["includeOpenGraph"], false);

    if (form.Files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .html, .htm, or .xml file." });
    }

    var results = new List<object>();

    foreach (var file in form.Files)
    {
        if (file.Length == 0)
        {
            results.Add(new
            {
                file = file.FileName,
                error = "File was empty."
            });
            continue;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".html" and not ".htm" and not ".xml")
        {
            results.Add(new
            {
                file = file.FileName,
                error = "Unsupported file type. Please upload .html, .htm, or .xml files."
            });
            continue;
        }

        try
        {
            var content = await ReadTextAsync(file);
            var metadata = ExtractMetadata(content, includeKeywords, includeOpenGraph);

            results.Add(new
            {
                file = file.FileName,
                metadata.Title,
                metadata.Description,
                metadata.Canonical,
                metadata.Language,
                Keywords = includeKeywords ? metadata.Keywords : null,
                OpenGraph = includeOpenGraph ? metadata.OpenGraph : null
            });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                file = file.FileName,
                error = $"Failed to read file: {ex.Message}"
            });
        }
    }

    var extracted = results.Count(entry => entry.GetType().GetProperty("error") is null);

    return Results.Ok(new
    {
        extracted,
        includeKeywords,
        includeOpenGraph,
        results
    });
});

app.Run();

static bool TryParseBool(string? value, bool fallback)
{
    return bool.TryParse(value, out var parsed) ? parsed : fallback;
}

static async Task<string> ReadTextAsync(IFormFile file)
{
    await using var stream = file.OpenReadStream();
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
    return await reader.ReadToEndAsync();
}

static Metadata ExtractMetadata(string html, bool includeKeywords, bool includeOpenGraph)
{
    var document = new HtmlDocument();
    document.LoadHtml(html);

    var head = document.DocumentNode.SelectSingleNode("//head") ?? document.DocumentNode;

    var htmlNode = document.DocumentNode.SelectSingleNode("//html");

    var metadata = new Metadata
    {
        Title = Normalize(HtmlEntity.DeEntitize(head.SelectSingleNode(".//title")?.InnerText.Trim() ?? string.Empty)),
        Description = Normalize(HtmlEntity.DeEntitize(GetMetaContent(head, "name", "description") ?? string.Empty)),
        Canonical = Normalize(head.SelectSingleNode(".//link[translate(@rel,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='canonical']")?.GetAttributeValue("href", null)),
        Language = Normalize(htmlNode?.GetAttributeValue("lang", null) ?? GetMetaContent(head, "http-equiv", "content-language"))
    };

    if (includeKeywords)
    {
        metadata.Keywords = Normalize(HtmlEntity.DeEntitize(GetMetaContent(head, "name", "keywords") ?? string.Empty));
    }

    if (includeOpenGraph)
    {
        var openGraph = new OpenGraphMetadata
        {
            Title = Normalize(HtmlEntity.DeEntitize(GetMetaContent(head, "property", "og:title") ?? string.Empty)),
            Description = Normalize(HtmlEntity.DeEntitize(GetMetaContent(head, "property", "og:description") ?? string.Empty)),
            Url = Normalize(GetMetaContent(head, "property", "og:url")),
            Image = Normalize(GetMetaContent(head, "property", "og:image")),
            Type = Normalize(GetMetaContent(head, "property", "og:type"))
        };

        if (!string.IsNullOrWhiteSpace(openGraph.Title) ||
            !string.IsNullOrWhiteSpace(openGraph.Description) ||
            !string.IsNullOrWhiteSpace(openGraph.Url) ||
            !string.IsNullOrWhiteSpace(openGraph.Image) ||
            !string.IsNullOrWhiteSpace(openGraph.Type))
        {
            metadata.OpenGraph = openGraph;
        }
    }

    return metadata;
}

static string? GetMetaContent(HtmlNode head, string attributeName, string match)
{
    var node = head.SelectSingleNode($".//meta[translate(@{attributeName},'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='{match.ToLowerInvariant()}']");
    return node?.GetAttributeValue("content", null);
}

static string? Normalize(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

record Metadata
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Keywords { get; init; }
    public string? Canonical { get; init; }
    public string? Language { get; init; }
    public OpenGraphMetadata? OpenGraph { get; init; }
}

record OpenGraphMetadata
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Url { get; init; }
    public string? Image { get; init; }
    public string? Type { get; init; }
}
