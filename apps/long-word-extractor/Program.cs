using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using UglyToad.PdfPig;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
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
        return Results.BadRequest(new { error = "Expected multipart/form-data." });
    }

    var form = await request.ReadFormAsync();
    var threshold = int.TryParse(form["threshold"], out var parsed) && parsed > 0 ? parsed : 10;

    if (form.Files.Count == 0 && string.IsNullOrWhiteSpace(form["text"]))
    {
        return Results.BadRequest(new { error = "Upload at least one file or provide inline text." });
    }

    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".docx", ".pdf", ".md", ".markdown", ".html", ".htm"
    };

    var aggregateCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var results = new List<object>();

    if (!string.IsNullOrWhiteSpace(form["text"]))
    {
        var inlineText = form["text"].ToString();
        var counts = CountLongWords(inlineText, threshold);
        MergeCounts(aggregateCounts, counts);

        results.Add(new
        {
            source = "Inline text",
            kind = "text",
            totalWords = counts.Values.Sum(),
            uniqueWords = counts.Count,
            words = OrderCounts(counts),
            error = (string?)null
        });
    }

    foreach (var file in form.Files)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!allowed.Contains(extension))
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                totalWords = 0,
                uniqueWords = 0,
                words = Array.Empty<object>(),
                error = "Unsupported file type. Upload .txt, .docx, .pdf, .md/.markdown, or .html/.htm."
            });
            continue;
        }

        try
        {
            var content = extension.ToLowerInvariant() switch
            {
                ".txt" or ".md" or ".markdown" => await ReadTextAsync(file),
                ".docx" => await ReadDocxAsync(file),
                ".pdf" => await ReadPdfAsync(file),
                ".html" or ".htm" => await ReadHtmlAsync(file),
                _ => throw new InvalidOperationException("Unsupported file type.")
            };

            var counts = CountLongWords(content, threshold);
            MergeCounts(aggregateCounts, counts);

            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                totalWords = counts.Values.Sum(),
                uniqueWords = counts.Count,
                words = OrderCounts(counts),
                error = (string?)null
            });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                totalWords = 0,
                uniqueWords = 0,
                words = Array.Empty<object>(),
                error = ex.Message
            });
        }
    }

    var orderedAggregate = OrderCounts(aggregateCounts);

    return Results.Ok(new
    {
        threshold,
        totalUniqueWords = aggregateCounts.Count,
        totalOccurrences = aggregateCounts.Values.Sum(),
        aggregate = orderedAggregate,
        results
    });
});

app.Run();

static Dictionary<string, int> CountLongWords(string content, int threshold)
{
    var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(content))
    {
        return counts;
    }

    var normalized = NormalizeWhitespace(content);
    var pattern = new Regex($"\\b[\\p{{L}}\\p{{N}}'\-]{{{threshold},}}\\b", RegexOptions.Compiled);

    foreach (Match match in pattern.Matches(normalized))
    {
        var raw = match.Value.Trim('''', '-');
        if (raw.Length < threshold)
        {
            continue;
        }

        if (counts.TryGetValue(raw, out var current))
        {
            counts[raw] = current + 1;
        }
        else
        {
            counts[raw] = 1;
        }
    }

    return counts;
}

static void MergeCounts(Dictionary<string, int> aggregate, Dictionary<string, int> addition)
{
    foreach (var pair in addition)
    {
        if (aggregate.TryGetValue(pair.Key, out var current))
        {
            aggregate[pair.Key] = current + pair.Value;
        }
        else
        {
            aggregate[pair.Key] = pair.Value;
        }
    }
}

static IEnumerable<object> OrderCounts(Dictionary<string, int> counts)
{
    return counts
        .OrderByDescending(x => x.Value)
        .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
        .Select(x => new { word = x.Key, count = x.Value });
}

static string NormalizeWhitespace(string input)
{
    var condensed = Regex.Replace(input, "\r\n", "\n");
    condensed = Regex.Replace(condensed, "[\t ]+", " ");
    return condensed.Trim();
}

static async Task<string> ReadTextAsync(IFormFile file)
{
    using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
    var content = await reader.ReadToEndAsync();

    // Strip common Markdown decorations to avoid inflating word length counts.
    content = Regex.Replace(content, @"[`*_>#\[\]{}()!\-]", " ");
    return content;
}

static async Task<string> ReadDocxAsync(IFormFile file)
{
    await using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    ms.Position = 0;

    using var doc = WordprocessingDocument.Open(ms, false);
    var body = doc.MainDocumentPart?.Document.Body;
    if (body is null)
    {
        return string.Empty;
    }

    var builder = new StringBuilder();
    foreach (var text in body.Descendants<Text>())
    {
        builder.Append(text.Text);
        builder.Append(' ');
    }

    return builder.ToString();
}

static async Task<string> ReadPdfAsync(IFormFile file)
{
    await using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    ms.Position = 0;

    using var pdf = PdfDocument.Open(ms);
    var builder = new StringBuilder();
    foreach (var page in pdf.GetPages())
    {
        builder.AppendLine(page.Text);
    }

    return builder.ToString();
}

static async Task<string> ReadHtmlAsync(IFormFile file)
{
    await using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    ms.Position = 0;

    var doc = new HtmlDocument();
    doc.Load(ms, Encoding.UTF8);

    var text = doc.DocumentNode.InnerText ?? string.Empty;
    text = Regex.Replace(text, "\\s+", " ");
    return text;
}
