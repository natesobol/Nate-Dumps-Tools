using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

var builder = WebApplication.CreateBuilder(args);

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
        return Results.BadRequest(new { error = "Expected multipart/form-data with options and files." });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;

    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one document to analyze." });
    }

    var includeInline = GetFlag(form, "includeInline", true);
    var includeBibliography = GetFlag(form, "includeBibliography", true);
    var export = form["export"].ToString();

    var results = new List<object>();

    foreach (var file in files)
    {
        await using var stream = file.OpenReadStream();
        var text = await ExtractTextAsync(file.FileName, stream);

        var inlineCitations = includeInline
            ? ExtractInlineCitations(text)
            : new List<string>();

        var references = includeBibliography
            ? ExtractReferencesSection(text)
            : new List<string>();

        var csv = BuildCsv(inlineCitations, references);
        var bibTeX = BuildBibTex(references);

        results.Add(new
        {
            file = file.FileName,
            inlineCount = inlineCitations.Count,
            bibliographyCount = references.Count,
            inlineCitations,
            bibliography = references,
            csv = export is "csv" or "all" ? Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)) : null,
            bibTeX = export is "bib" or "all" ? bibTeX : null
        });
    }

    return Results.Ok(new { includeInline, includeBibliography, files = results });
});

app.Run();

static bool GetFlag(IFormCollection form, string key, bool defaultValue)
{
    var value = form[key].ToString();
    return string.IsNullOrEmpty(value) ? defaultValue : string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}

static async Task<string> ExtractTextAsync(string fileName, Stream stream)
{
    var extension = Path.GetExtension(fileName).ToLowerInvariant();

    return extension switch
    {
        ".txt" => await ReadAllTextAsync(stream),
        ".rtf" => await ReadRtfAsync(stream),
        ".docx" => await ReadDocxAsync(stream),
        ".pdf" => await ReadPdfAsync(stream),
        _ => throw new InvalidOperationException($"Unsupported file type: {extension}")
    };
}

static async Task<string> ReadAllTextAsync(Stream stream)
{
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
    return await reader.ReadToEndAsync();
}

static async Task<string> ReadRtfAsync(Stream stream)
{
    var content = await ReadAllTextAsync(stream);
    // A lightweight RTF cleaner to strip control words and braces.
    var withoutEscapes = Regex.Replace(content, @"\\'[0-9a-fA-F]{2}", " ");
    var withoutControls = Regex.Replace(withoutEscapes, @"\\[a-zA-Z]+-?\d* ?", string.Empty);
    var withoutGroups = Regex.Replace(withoutControls, "[{}]", string.Empty);
    return Regex.Replace(withoutGroups, "\\~|\\-", " ", RegexOptions.Compiled);
}

static async Task<string> ReadDocxAsync(Stream stream)
{
    using var memory = new MemoryStream();
    await stream.CopyToAsync(memory);
    memory.Position = 0;

    using var doc = WordprocessingDocument.Open(memory, false);
    var body = doc.MainDocumentPart?.Document.Body;
    if (body == null)
    {
        return string.Empty;
    }

    var sb = new StringBuilder();
    foreach (var para in body.Descendants<Paragraph>())
    {
        sb.AppendLine(GetParagraphText(para));
    }

    return sb.ToString();
}

static string GetParagraphText(Paragraph paragraph)
{
    var sb = new StringBuilder();
    foreach (var text in paragraph.Descendants<Text>())
    {
        sb.Append(text.Text);
    }

    return sb.ToString();
}

static async Task<string> ReadPdfAsync(Stream stream)
{
    using var memory = new MemoryStream();
    await stream.CopyToAsync(memory);
    memory.Position = 0;

    var builder = new StringBuilder();
    using var document = PdfDocument.Open(memory);
    foreach (Page page in document.GetPages())
    {
        builder.AppendLine(page.Text);
    }

    return builder.ToString();
}

static List<string> ExtractInlineCitations(string text)
{
    var patterns = new[]
    {
        @"\([A-Z][A-Za-z]+,\s?\d{4}[a-z]?\)", // APA single author
        @"\([A-Z][A-Za-z]+\s+&\s+[A-Z][A-Za-z]+,\s?\d{4}[a-z]?\)", // APA two authors
        @"\([A-Z][A-Za-z]+\s+et al\.,\s?\d{4}[a-z]?\)", // APA et al.
        @"\([A-Z][A-Za-z]+\s+[A-Z][A-Za-z]+(?:\s+[A-Z][A-Za-z]+)?,\s?\d{4}[a-z]?\)", // APA multi-author
        @"\([A-Z][A-Za-z]+\s+\d{1,4}\)", // MLA parenthetical
        @"\[[0-9]{1,3}\]" // IEEE numeric
    };

    var matches = new HashSet<string>(StringComparer.Ordinal);
    foreach (var pattern in patterns)
    {
        foreach (Match match in Regex.Matches(text, pattern))
        {
            var value = match.Value.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                matches.Add(value);
            }
        }
    }

    return matches.OrderBy(c => c, StringComparer.Ordinal).ToList();
}

static List<string> ExtractReferencesSection(string text)
{
    var lines = text.Split('\n');
    var headings = new[] { "references", "bibliography", "works cited" };
    var startIndex = -1;

    for (var i = 0; i < lines.Length; i++)
    {
        var trimmed = lines[i].Trim();
        if (headings.Any(h => string.Equals(trimmed, h, StringComparison.OrdinalIgnoreCase)))
        {
            startIndex = i + 1;
            break;
        }
    }

    if (startIndex == -1)
    {
        return new List<string>();
    }

    var references = new List<string>();
    var emptyRun = 0;

    for (var i = startIndex; i < lines.Length; i++)
    {
        var line = lines[i].Trim();
        if (string.IsNullOrWhiteSpace(line))
        {
            emptyRun++;
            if (emptyRun >= 2)
            {
                break;
            }

            continue;
        }

        emptyRun = 0;
        references.Add(line);
    }

    return references;
}

static string BuildCsv(IEnumerable<string> inlineCitations, IEnumerable<string> bibliography)
{
    var builder = new StringBuilder();
    builder.AppendLine("type,value");

    foreach (var citation in inlineCitations)
    {
        builder.AppendLine($"inline,\"{EscapeForCsv(citation)}\"");
    }

    foreach (var reference in bibliography)
    {
        builder.AppendLine($"reference,\"{EscapeForCsv(reference)}\"");
    }

    return builder.ToString();
}

static string EscapeForCsv(string value)
{
    return value.Replace("\"", "\"\"");
}

static string BuildBibTex(IReadOnlyList<string> references)
{
    var sb = new StringBuilder();
    for (var i = 0; i < references.Count; i++)
    {
        var key = $"ref{i + 1}";
        sb.AppendLine($"@article{{{key},");
        sb.AppendLine($"  title = {{{references[i]}}},");
        sb.AppendLine("  author = {Unknown},");
        sb.AppendLine("  year = {----}");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    return sb.ToString().TrimEnd();
}
