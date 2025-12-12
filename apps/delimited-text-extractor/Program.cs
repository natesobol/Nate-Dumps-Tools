using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

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

app.MapPost("/api/extract", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    if (form.Files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .txt, .docx, .pdf, or .csv file." });
    }

    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".docx", ".pdf", ".csv"
    };

    var regex = new Regex(
        "\\((?<paren>[^()]*)\\)|\\[(?<bracket>[^\\[\\]]*)\\]|\\{(?<brace>[^{}]*)\\}|\"(?<double>(?:[^\"\\\\]|\\\\.)*)\"|'(?<single>(?:[^'\\\\]|\\\\.)*)'",
        RegexOptions.Compiled
    );

    var results = new List<object>();
    var totalMatches = 0;

    foreach (var file in form.Files)
    {
        var extension = Path.GetExtension(file.FileName);

        if (!allowed.Contains(extension))
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = Array.Empty<object>(),
                grouped = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase),
                error = "Unsupported file type."
            });
            continue;
        }

        if (file.Length == 0)
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = Array.Empty<object>(),
                grouped = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase),
                error = "File was empty."
            });
            continue;
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var text = await ExtractTextAsync(memoryStream, extension);
            var extracted = ExtractSegments(text, regex);
            totalMatches += extracted.Count;

            var grouped = extracted
                .GroupBy(x => x.Type)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<string>)g.Select(x => x.Value).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = extracted.Select(x => new { type = x.Type, value = x.Value }).ToList(),
                grouped,
                error = (string?)null
            });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = Array.Empty<object>(),
                grouped = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase),
                error = ex.Message
            });
        }
    }

    return Results.Ok(new
    {
        filesProcessed = results.Count,
        totalMatches,
        results
    });
});

app.Run();

static async Task<string> ExtractTextAsync(Stream stream, string extension)
{
    switch (extension.ToLowerInvariant())
    {
        case ".txt":
        case ".csv":
            stream.Position = 0;
            using (var reader = new StreamReader(stream, leaveOpen: true))
            {
                return await reader.ReadToEndAsync();
            }
        case ".docx":
            stream.Position = 0;
            using (var wordDoc = WordprocessingDocument.Open(stream, false))
            {
                return wordDoc.MainDocumentPart?.Document.Body?.InnerText ?? string.Empty;
            }
        case ".pdf":
            stream.Position = 0;
            using (var document = PdfDocument.Open(stream))
            {
                var text = new List<string>();
                foreach (Page page in document.GetPages())
                {
                    text.Add(page.Text);
                }
                return string.Join("\n", text);
            }
        default:
            throw new InvalidOperationException($"Unsupported file type: {extension}");
    }
}

static List<ExtractedSegment> ExtractSegments(string content, Regex regex)
{
    var segments = new List<ExtractedSegment>();

    foreach (Match match in regex.Matches(content))
    {
        var type = match.Groups["paren"].Success ? "parentheses"
            : match.Groups["bracket"].Success ? "brackets"
            : match.Groups["brace"].Success ? "braces"
            : match.Groups["double"].Success ? "doubleQuotes"
            : match.Groups["single"].Success ? "singleQuotes"
            : "unknown";

        var value = match.Groups["paren"].Success ? match.Groups["paren"].Value
            : match.Groups["bracket"].Success ? match.Groups["bracket"].Value
            : match.Groups["brace"].Success ? match.Groups["brace"].Value
            : match.Groups["double"].Success ? match.Groups["double"].Value
            : match.Groups["single"].Value;

        if (!string.IsNullOrEmpty(value))
        {
            segments.Add(new ExtractedSegment(type, value.Trim()));
        }
    }

    return segments;
}

record ExtractedSegment(string Type, string Value);
