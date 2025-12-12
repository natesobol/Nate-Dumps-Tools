using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using UglyToad.PdfPig;

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

app.MapPost("/api/extract", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with one or more files." });
    }

    var form = await request.ReadFormAsync();
    var onlyWithCaption = bool.TryParse(form["onlyWithCaption"], out var filterFlag) && filterFlag;

    if (form.Files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .html, .md, .docx, or .pdf file." });
    }

    var allowed = new HashSet<string>(new[] { ".html", ".htm", ".md", ".markdown", ".docx", ".pdf" }, StringComparer.OrdinalIgnoreCase);
    var results = new List<FileExtractionResult>();

    foreach (var file in form.Files)
    {
        var ext = Path.GetExtension(file.FileName);
        if (!allowed.Contains(ext))
        {
            results.Add(new FileExtractionResult(file.FileName, ext.TrimStart('.'), Array.Empty<ImageCaptionResult>(), "Unsupported file type."));
            continue;
        }

        if (file.Length == 0)
        {
            results.Add(new FileExtractionResult(file.FileName, ext.TrimStart('.'), Array.Empty<ImageCaptionResult>(), "File was empty."));
            continue;
        }

        try
        {
            await using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);

            var entries = ext.ToLowerInvariant() switch
            {
                ".html" or ".htm" => ExtractFromHtml(buffer),
                ".md" or ".markdown" => ExtractFromMarkdown(buffer),
                ".docx" => ExtractFromDocx(buffer),
                ".pdf" => ExtractFromPdf(buffer),
                _ => new List<ImageCaptionResult>()
            };

            if (onlyWithCaption)
            {
                entries = entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Caption) || !string.IsNullOrWhiteSpace(e.AltText))
                    .ToList();
            }

            results.Add(new FileExtractionResult(file.FileName, ext.TrimStart('.'), entries, null));
        }
        catch (Exception ex)
        {
            results.Add(new FileExtractionResult(file.FileName, ext.TrimStart('.'), Array.Empty<ImageCaptionResult>(), ex.Message));
        }
    }

    return Results.Ok(new
    {
        filesProcessed = results.Count,
        totalCaptions = results.Sum(r => r.Entries.Count),
        results
    });
});

app.Run();

static List<ImageCaptionResult> ExtractFromHtml(MemoryStream buffer)
{
    buffer.Position = 0;
    var doc = new HtmlDocument();
    doc.Load(buffer, Encoding.UTF8);

    var matches = new List<ImageCaptionResult>();
    var images = doc.DocumentNode.SelectNodes("//img") ?? new HtmlNodeCollection(null);

    foreach (var img in images)
    {
        var src = img.GetAttributeValue("src", string.Empty).Trim();
        var alt = img.GetAttributeValue("alt", string.Empty).Trim();

        var caption = FindFigureCaption(img)?.Trim();
        if (string.IsNullOrWhiteSpace(caption))
        {
            var titleAttr = img.GetAttributeValue("title", string.Empty).Trim();
            caption = titleAttr;
        }

        matches.Add(new ImageCaptionResult(src, caption ?? string.Empty, alt, "HTML <img>"));
    }

    return Deduplicate(matches);
}

static string? FindFigureCaption(HtmlNode imageNode)
{
    var figure = imageNode.Ancestors("figure").FirstOrDefault();
    if (figure == null)
    {
        return null;
    }

    var figcaption = figure.Descendants("figcaption").FirstOrDefault();
    return figcaption?.InnerText;
}

static List<ImageCaptionResult> ExtractFromMarkdown(MemoryStream buffer)
{
    buffer.Position = 0;
    using var reader = new StreamReader(buffer, Encoding.UTF8, leaveOpen: true);
    var content = reader.ReadToEnd();

    var referenceDefs = new Dictionary<string, (string Url, string? Title)>(StringComparer.OrdinalIgnoreCase);
    var defRegex = new Regex("^\\s*\\[(?<id>[^\\]]+)\\]:\\s*(?<url>\\S+)(?:\\s+\"(?<title>[^\"]*)\")?", RegexOptions.Multiline);
    foreach (Match match in defRegex.Matches(content))
    {
        referenceDefs[match.Groups["id"].Value] = (match.Groups["url"].Value, match.Groups["title"].Value);
    }

    var inlineRegex = new Regex("!\\[(?<alt>[^\\]]*)\\]\\((?<url>[^)\\s]+)(?:\\s+\"(?<title>[^\"]*)\")?\\)");
    var referenceRegex = new Regex("!\\[(?<alt>[^\\]]*)\\]\\[(?<ref>[^\\]]+)\\]");

    var matches = new List<ImageCaptionResult>();

    foreach (Match match in inlineRegex.Matches(content))
    {
        var src = match.Groups["url"].Value;
        var alt = match.Groups["alt"].Value;
        var title = match.Groups["title"].Value;

        matches.Add(new ImageCaptionResult(src, title ?? alt, alt, "Markdown inline"));
    }

    foreach (Match match in referenceRegex.Matches(content))
    {
        var key = match.Groups["ref"].Value;
        if (!referenceDefs.TryGetValue(key, out var target))
        {
            continue;
        }

        var alt = match.Groups["alt"].Value;
        var caption = string.IsNullOrWhiteSpace(target.Title) ? alt : target.Title;
        matches.Add(new ImageCaptionResult(target.Url, caption, alt, "Markdown reference"));
    }

    return Deduplicate(matches);
}

static List<ImageCaptionResult> ExtractFromDocx(MemoryStream buffer)
{
    buffer.Position = 0;
    using var copy = new MemoryStream(buffer.ToArray());
    using var document = WordprocessingDocument.Open(copy, false);

    var mainPart = document.MainDocumentPart;
    if (mainPart == null)
    {
        return new List<ImageCaptionResult>();
    }

    var imageMap = mainPart.ImageParts.ToDictionary(p => mainPart.GetIdOfPart(p), p => p.Uri.ToString());
    var paragraphs = mainPart.Document?.Body?.Descendants<Paragraph>().ToList() ?? new List<Paragraph>();
    var matches = new List<ImageCaptionResult>();

    for (var i = 0; i < paragraphs.Count; i++)
    {
        var paragraph = paragraphs[i];
        var drawings = paragraph.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().ToList();

        foreach (var drawing in drawings)
        {
            var embedId = drawing.Embed?.Value ?? string.Empty;
            imageMap.TryGetValue(embedId, out var reference);

            var altText = GetAltFromDrawing(drawing);
            var caption = GetParagraphText(paragraph).Trim();
            if (string.IsNullOrWhiteSpace(caption) && i + 1 < paragraphs.Count)
            {
                caption = GetParagraphText(paragraphs[i + 1]).Trim();
            }

            matches.Add(new ImageCaptionResult(reference ?? embedId, caption, altText, "DOCX image"));
        }
    }

    return Deduplicate(matches);
}

static string GetAltFromDrawing(DocumentFormat.OpenXml.Drawing.Blip blip)
{
    var nvProps = blip.Parent?.Ancestors<DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties>().FirstOrDefault();
    if (nvProps != null)
    {
        if (!string.IsNullOrWhiteSpace(nvProps.Description))
        {
            return nvProps.Description;
        }

        if (!string.IsNullOrWhiteSpace(nvProps.Title))
        {
            return nvProps.Title;
        }
    }

    return string.Empty;
}

static string GetParagraphText(Paragraph paragraph)
{
    var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
    return text ?? string.Empty;
}

static List<ImageCaptionResult> ExtractFromPdf(MemoryStream buffer)
{
    var results = new List<ImageCaptionResult>();

    try
    {
        buffer.Position = 0;
        using var pdf = PdfDocument.Open(buffer);
        var index = 1;
        foreach (var page in pdf.GetPages())
        {
            var raw = page.ExperimentalAccess?.GetContentBytes();
            var pageText = raw != null ? Encoding.UTF8.GetString(raw) : string.Empty;

            var altRegex = new Regex(@"/Alt\s*\((?<text>[^)]*)\)");
            var captionRegex = new Regex(@"/Caption\s*\((?<text>[^)]*)\)");

            var altMatches = altRegex.Matches(pageText);
            var captionMatches = captionRegex.Matches(pageText);

            for (var i = 0; i < Math.Max(altMatches.Count, captionMatches.Count); i++)
            {
                var alt = i < altMatches.Count ? altMatches[i].Groups["text"].Value : string.Empty;
                var caption = i < captionMatches.Count ? captionMatches[i].Groups["text"].Value : string.Empty;
                results.Add(new ImageCaptionResult($"page-{page.Number}-image-{index++}", caption, alt, "PDF object"));
            }
        }
    }
    catch
    {
        // fall back to scanning raw bytes if structured access fails
    }

    if (results.Count > 0)
    {
        return Deduplicate(results);
    }

    var fallbackAlt = new Regex(@"/Alt\s*\((?<text>[^)]*)\)");
    var fallbackCaption = new Regex(@"/Caption\s*\((?<text>[^)]*)\)");

    var rawText = Encoding.UTF8.GetString(buffer.ToArray());
    var alts = fallbackAlt.Matches(rawText);
    var captions = fallbackCaption.Matches(rawText);

    var total = Math.Max(alts.Count, captions.Count);
    for (var i = 0; i < total; i++)
    {
        var alt = i < alts.Count ? alts[i].Groups["text"].Value : string.Empty;
        var caption = i < captions.Count ? captions[i].Groups["text"].Value : string.Empty;
        results.Add(new ImageCaptionResult($"object-{i + 1}", caption, alt, "PDF raw scan"));
    }

    return Deduplicate(results);
}

static List<ImageCaptionResult> Deduplicate(List<ImageCaptionResult> entries)
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var unique = new List<ImageCaptionResult>();

    foreach (var entry in entries)
    {
        var key = $"{entry.Reference}::{entry.Caption}::{entry.AltText}";
        if (seen.Add(key))
        {
            unique.Add(entry);
        }
    }

    return unique;
}

record ImageCaptionResult(string Reference, string Caption, string AltText, string Source);
record FileExtractionResult(string File, string Type, IReadOnlyCollection<ImageCaptionResult> Entries, string? Error);
