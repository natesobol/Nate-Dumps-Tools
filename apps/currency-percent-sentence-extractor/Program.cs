using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ExcelDataReader;
using Microsoft.AspNetCore.Http.Features;
using UglyToad.PdfPig;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 50; // 50 MB
});

builder.Services.AddResponseCompression();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract-currency-sentences", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with file uploads." });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;

    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .txt, .csv, .xlsx, .docx, or .pdf file." });
    }

    double? minValue = null;
    var minValueRaw = form["minValue"].ToString();
    if (!string.IsNullOrWhiteSpace(minValueRaw) &&
        !double.TryParse(minValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
    {
        return Results.BadRequest(new { error = "Minimum value must be a valid number (use '.' for decimals)." });
    }
    else if (!string.IsNullOrWhiteSpace(minValueRaw))
    {
        minValue = parsed;
    }

    var responses = new List<FileResult>();

    foreach (var file in files)
    {
        try
        {
            var segments = await ExtractSegmentsAsync(file);
            var matches = FilterSegments(segments, minValue);

            responses.Add(new FileResult
            {
                FileName = file.FileName,
                Matches = matches,
                MatchCount = matches.Count
            });
        }
        catch (NotSupportedException nse)
        {
            responses.Add(new FileResult
            {
                FileName = file.FileName,
                Error = nse.Message
            });
        }
        catch (Exception ex)
        {
            responses.Add(new FileResult
            {
                FileName = file.FileName,
                Error = $"Failed to process file: {ex.Message}"
            });
        }
    }

    var flattened = responses
        .Where(r => r.Matches is not null)
        .SelectMany(r => r.Matches!.Select(m => new FlatMatch
        {
            File = r.FileName,
            Line = m.LineNumber,
            Text = m.Text,
            Values = m.Values
        }))
        .ToList();

    var csv = BuildCsv(flattened);
    var text = BuildText(flattened);

    return Results.Ok(new
    {
        files = responses,
        totalMatches = flattened.Count,
        rangeApplied = minValue is not null,
        minValue,
        csv,
        text
    });
});

app.Run();

static async Task<List<Segment>> ExtractSegmentsAsync(IFormFile file)
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    return extension switch
    {
        ".txt" => await ReadLinesAsync(file),
        ".csv" => await ReadLinesAsync(file),
        ".docx" => await ReadDocxAsync(file),
        ".pdf" => await ReadPdfAsync(file),
        ".xlsx" => await ReadSpreadsheetAsync(file),
        _ => throw new NotSupportedException($"Unsupported file type: {extension}. Use .txt, .csv, .xlsx, .docx, or .pdf.")
    };
}

static async Task<List<Segment>> ReadLinesAsync(IFormFile file)
{
    var segments = new List<Segment>();
    using var reader = new StreamReader(file.OpenReadStream());
    var lineNumber = 1;
    while (!reader.EndOfStream)
    {
        var line = await reader.ReadLineAsync() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(line))
        {
            segments.Add(new Segment(lineNumber, line.Trim()));
        }
        lineNumber++;
    }

    return segments;
}

static async Task<List<Segment>> ReadDocxAsync(IFormFile file)
{
    var segments = new List<Segment>();
    await using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    ms.Position = 0;

    using var wordDoc = WordprocessingDocument.Open(ms, false);
    var body = wordDoc.MainDocumentPart?.Document.Body;
    if (body is null)
    {
        return segments;
    }

    var line = 1;
    foreach (var paragraph in body.Descendants<Paragraph>())
    {
        var text = paragraph.InnerText;
        if (!string.IsNullOrWhiteSpace(text))
        {
            segments.Add(new Segment(line, text.Trim()));
            line++;
        }
    }

    return segments;
}

static async Task<List<Segment>> ReadPdfAsync(IFormFile file)
{
    var segments = new List<Segment>();
    await using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    ms.Position = 0;

    using var pdf = PdfDocument.Open(ms);
    var lineNumber = 1;
    foreach (var page in pdf.GetPages())
    {
        var lines = page.Text.Split('\n');
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                segments.Add(new Segment(lineNumber, line.Trim()));
                lineNumber++;
            }
        }
    }

    return segments;
}

static async Task<List<Segment>> ReadSpreadsheetAsync(IFormFile file)
{
    var segments = new List<Segment>();
    await using var stream = file.OpenReadStream();
    using var reader = ExcelReaderFactory.CreateReader(stream);
    var lineNumber = 1;

    do
    {
        while (reader.Read())
        {
            var cells = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.GetValue(i)?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    cells.Add(value.Trim());
                }
            }

            if (cells.Count > 0)
            {
                var rowText = string.Join(" | ", cells);
                segments.Add(new Segment(lineNumber, rowText));
            }

            lineNumber++;
        }
    } while (reader.NextResult());

    return segments;
}

static List<SegmentMatch> FilterSegments(IEnumerable<Segment> segments, double? minValue)
{
    var matches = new List<SegmentMatch>();
    foreach (var segment in segments)
    {
        var detected = DetectValues(segment.Text);
        var hasSymbol = detected.Count > 0 || SymbolOnlyRegex.IsMatch(segment.Text);

        if (!hasSymbol)
        {
            continue;
        }

        var meetsRange = minValue is null || detected.Any(v => v.NumericValue is not null && v.NumericValue >= minValue);
        var include = detected.Count > 0 ? meetsRange : minValue is null;

        if (include)
        {
            matches.Add(new SegmentMatch
            {
                LineNumber = segment.LineNumber,
                Text = segment.Text,
                Values = detected.Select(v => v.Raw).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            });
        }
    }

    return matches;
}

static List<DetectedValue> DetectValues(string text)
{
    var results = new List<DetectedValue>();

    foreach (Match match in CurrencyRegex.Matches(text))
    {
        var raw = match.Value.Trim();
        var numberPart = match.Groups[2].Value;
        results.Add(new DetectedValue(raw, ParseNumber(numberPart)));
    }

    foreach (Match match in PercentRegex.Matches(text))
    {
        var raw = match.Value.Trim();
        var numberPart = match.Groups[1].Value;
        results.Add(new DetectedValue(raw, ParseNumber(numberPart)));
    }

    return results;
}

static double? ParseNumber(string input)
{
    var cleaned = input.Replace(",", string.Empty);
    if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
    {
        return value;
    }

    return null;
}

static string BuildCsv(IEnumerable<FlatMatch> matches)
{
    var sb = new StringBuilder();
    sb.AppendLine("File,Line,Text,Values");

    foreach (var match in matches)
    {
        sb.AppendLine(string.Join(',',
            EscapeCsv(match.File),
            match.Line,
            EscapeCsv(match.Text),
            EscapeCsv(string.Join("; ", match.Values))));
    }

    return sb.ToString();
}

static string BuildText(IEnumerable<FlatMatch> matches)
{
    var sb = new StringBuilder();
    foreach (var match in matches)
    {
        var values = match.Values.Count > 0 ? $" [{string.Join(", ", match.Values)}]" : string.Empty;
        sb.AppendLine($"{match.File} (line {match.Line}): {match.Text}{values}");
    }

    return sb.ToString();
}

static string EscapeCsv(string input)
{
    var needsQuotes = input.Contains('"') || input.Contains(',') || input.Contains('\n');
    var escaped = input.Replace("\"", "\"\"");
    return needsQuotes ? $"\"{escaped}\"" : escaped;
}

internal record Segment(int LineNumber, string Text);

internal record DetectedValue(string Raw, double? NumericValue);

internal record SegmentMatch
{
    public int LineNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public List<string> Values { get; init; } = new();
}

internal record FileResult
{
    public string FileName { get; init; } = string.Empty;
    public int MatchCount { get; init; }
    public List<SegmentMatch>? Matches { get; init; }
    public string? Error { get; init; }
}

internal record FlatMatch
{
    public string File { get; init; } = string.Empty;
    public int Line { get; init; }
    public string Text { get; init; } = string.Empty;
    public List<string> Values { get; init; } = new();
}

internal static class Patterns
{
    public const string CurrencySymbols = "\\$€£¥₹₽₩₺฿¢";
}

internal static partial class CurrencyPatterns
{
    [GeneratedRegex(@"([" + Patterns.CurrencySymbols + @"])[\s\u00A0]*([+-]?\d[\d,]*(?:\.\d+)?)", RegexOptions.Multiline)]
    public static partial Regex CurrencyRegex();

    [GeneratedRegex(@"([+-]?\d[\d,]*(?:\.\d+)?)\s*%", RegexOptions.Multiline)]
    public static partial Regex PercentRegex();

    [GeneratedRegex("[" + Patterns.CurrencySymbols + @"%]")]
    public static partial Regex SymbolOnlyRegex();
}

internal static Regex CurrencyRegex => CurrencyPatterns.CurrencyRegex();
internal static Regex PercentRegex => CurrencyPatterns.PercentRegex();
internal static Regex SymbolOnlyRegex => CurrencyPatterns.SymbolOnlyRegex();
