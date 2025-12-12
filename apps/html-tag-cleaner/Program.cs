using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/clean", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with HTML text or uploads." });
    }

    var form = await request.ReadFormAsync();
    var allowedTags = ParseAllowlist(form["allowedTags"].ToString());
    var collapseWhitespace = TryParseBool(form["collapseWhitespace"], true);

    var htmlInput = form["html"].ToString();
    var results = new List<object>();

    if (!string.IsNullOrWhiteSpace(htmlInput))
    {
        var cleaned = CleanHtml(htmlInput, allowedTags, collapseWhitespace);
        results.Add(new
        {
            source = "text",
            lengthBefore = htmlInput.Length,
            lengthAfter = cleaned.Length,
            cleaned,
            allowedTags
        });
    }

    foreach (var file in form.Files)
    {
        if (file.Length == 0)
        {
            results.Add(new
            {
                source = file.FileName,
                error = "File was empty."
            });
            continue;
        }

        try
        {
            var content = await ReadTextAsync(file);
            var cleaned = CleanHtml(content, allowedTags, collapseWhitespace);
            results.Add(new
            {
                source = file.FileName,
                lengthBefore = content.Length,
                lengthAfter = cleaned.Length,
                cleaned,
                allowedTags
            });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                source = file.FileName,
                error = $"Failed to read file: {ex.Message}"
            });
        }
    }

    if (results.Count == 0)
    {
        return Results.BadRequest(new { error = "Provide HTML in the text area or upload at least one file." });
    }

    return Results.Ok(new
    {
        mode = allowedTags.Count > 0 ? "allowlist" : "strip-all",
        allowed = allowedTags,
        collapseWhitespace,
        results
    });
});

app.Run();

static HashSet<string> ParseAllowlist(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    var tags = raw
        .Split(new[] { ',', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(t => t.TrimStart('<').TrimEnd('>').ToLowerInvariant());

    return new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
}

static bool TryParseBool(string? value, bool fallback)
{
    return bool.TryParse(value, out var parsed) ? parsed : fallback;
}

static string CleanHtml(string html, HashSet<string> allowedTags, bool collapseWhitespace)
{
    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    if (allowedTags.Count == 0)
    {
        var text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText);
        return collapseWhitespace ? NormalizePlainText(text) : text;
    }

    var builder = new StringBuilder();
    foreach (var child in doc.DocumentNode.ChildNodes)
    {
        AppendAllowed(child, allowedTags, builder);
    }

    var cleaned = builder.ToString();
    return collapseWhitespace ? CollapseWhitespace(cleaned) : cleaned;
}

static void AppendAllowed(HtmlNode node, HashSet<string> allowedTags, StringBuilder builder)
{
    switch (node.NodeType)
    {
        case HtmlNodeType.Text:
            builder.Append(HtmlEntity.DeEntitize(node.InnerText));
            break;
        case HtmlNodeType.Element:
            var name = node.Name.ToLowerInvariant();
            if (allowedTags.Contains(name))
            {
                builder.Append('<').Append(name).Append('>');
                foreach (var child in node.ChildNodes)
                {
                    AppendAllowed(child, allowedTags, builder);
                }
                builder.Append("</").Append(name).Append('>');
            }
            else
            {
                foreach (var child in node.ChildNodes)
                {
                    AppendAllowed(child, allowedTags, builder);
                }
            }
            break;
    }
}

static string NormalizePlainText(string value)
{
    var trimmed = HtmlEntity.DeEntitize(value);
    var condensed = Regex.Replace(trimmed, "\s+", " ");
    return condensed.Trim();
}

static string CollapseWhitespace(string value)
{
    var condensed = Regex.Replace(value, "[\t ]+", " ");
    condensed = Regex.Replace(condensed, "\n{3,}", "\n\n");
    return condensed.Trim();
}

static async Task<string> ReadTextAsync(IFormFile file)
{
    await using var stream = file.OpenReadStream();
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
    return await reader.ReadToEndAsync();
}
