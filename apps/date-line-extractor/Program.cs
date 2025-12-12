using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/extract", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with one or more files." });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;

    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "No files were uploaded." });
    }

    var groupBy = (form["groupBy"].FirstOrDefault() ?? "none").ToLowerInvariant();
    if (groupBy != "none" && groupBy != "day" && groupBy != "month" && groupBy != "year")
    {
        return Results.BadRequest(new { error = "Invalid groupBy value. Choose none, day, month, or year." });
    }

    var entries = new List<DateEntry>();
    var errors = new List<object>();

    foreach (var file in files)
    {
        try
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var content = extension switch
            {
                ".txt" or ".csv" or ".log" => await ReadTextAsync(file),
                ".docx" => await ReadDocxAsync(file),
                ".pdf" => await ReadPdfAsync(file),
                _ => throw new InvalidOperationException($"Unsupported file type: {extension}. Upload .txt, .docx, .pdf, .csv, or .log files."),
            };

            entries.AddRange(ExtractDateEntries(content, file.FileName));
        }
        catch (Exception ex)
        {
            errors.Add(new { file = file.FileName, message = ex.Message });
        }
    }

    var grouped = BuildGroups(entries, groupBy);

    return Results.Ok(new
    {
        count = entries.Count,
        groupBy,
        entries,
        groups = grouped,
        errors,
    });
});

app.Run();

static async Task<string> ReadTextAsync(IFormFile file)
{
    using var stream = file.OpenReadStream();
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
    return await reader.ReadToEndAsync();
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

static IEnumerable<DateEntry> ExtractDateEntries(string content, string fileName)
{
    var segments = SplitEntries(content);
    var entries = new List<DateEntry>();

    foreach (var segment in segments)
    {
        var matches = FindDateMatches(segment).ToList();
        if (matches.Count == 0)
        {
            continue;
        }

        var normalized = NormalizeDate(matches) ?? NormalizeDate(new[] { segment });
        var dateKey = normalized.HasValue
            ? normalized.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

        entries.Add(new DateEntry
        {
            File = fileName,
            Entry = segment,
            Matches = matches,
            Timestamp = matches.First(),
            NormalizedTimestamp = normalized?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateKey = dateKey,
        });
    }

    return entries;
}

static IEnumerable<string> SplitEntries(string text)
{
    var normalized = text.Replace("\r\n", "\n");
    var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var sentenceSplit = new Regex(@"(?<=[\.!?])\s+(?=[A-Z0-9])", RegexOptions.Compiled);

    foreach (var line in lines)
    {
        var parts = sentenceSplit.Split(line);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                yield return trimmed;
            }
        }
    }
}

static IEnumerable<string> FindDateMatches(string input)
{
    var patterns = new List<Regex>
    {
        new(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", RegexOptions.Compiled),
        new(@"\b\d{4}-\d{1,2}-\d{1,2}\b", RegexOptions.Compiled),
        new(@"\b\d{1,2}\s+(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec|January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{2,4}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec|January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},\s*\d{2,4}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b\d{1,2}:\d{2}(?::\d{2})?\s?(?:AM|PM|am|pm)?\b", RegexOptions.Compiled),
    };

    var hits = new HashSet<string>();
    foreach (var regex in patterns)
    {
        foreach (Match match in regex.Matches(input))
        {
            if (match.Success)
            {
                hits.Add(match.Value.Trim());
            }
        }
    }

    return hits;
}

static DateTime? NormalizeDate(IEnumerable<string> values)
{
    var formats = new[]
    {
        "M/d/yyyy", "M/d/yy", "MM/dd/yyyy", "M/d/yyyy h:mm tt", "M/d/yyyy hh:mm tt", "M/d/yyyy HH:mm",
        "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm",
        "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy", "d MMMM yyyy", "dd MMM yyyy HH:mm", "d MMM yyyy HH:mm",
        "dd MMM yyyy h:mm tt", "d MMM yyyy h:mm tt", "dd MMMM yyyy h:mm tt", "d MMMM yyyy h:mm tt",
        "MMMM d, yyyy", "MMM d, yyyy", "MMMM d yyyy", "MMM d yyyy",
        "h:mm tt", "hh:mm tt", "H:mm", "HH:mm", "HH:mm:ss"
    };

    foreach (var value in values)
    {
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exact))
        {
            return exact;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            return parsed;
        }
    }

    return null;
}

static List<GroupedEntries> BuildGroups(IEnumerable<DateEntry> entries, string groupBy)
{
    return groupBy switch
    {
        "day" => entries.GroupBy(e => e.DateKey ?? "Unknown")
            .Select(g => new GroupedEntries { Key = g.Key, Entries = g.ToList() })
            .OrderBy(g => g.Key)
            .ToList(),
        "month" => entries.GroupBy(e => ToMonthKey(e.DateKey))
            .Select(g => new GroupedEntries { Key = g.Key, Entries = g.ToList() })
            .OrderBy(g => g.Key)
            .ToList(),
        "year" => entries.GroupBy(e => ToYearKey(e.DateKey))
            .Select(g => new GroupedEntries { Key = g.Key, Entries = g.ToList() })
            .OrderBy(g => g.Key)
            .ToList(),
        _ => new List<GroupedEntries>(),
    };
}

static string ToMonthKey(string? date)
{
    if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
    {
        return parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    return "Unknown";
}

static string ToYearKey(string? date)
{
    if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
    {
        return parsed.Year.ToString(CultureInfo.InvariantCulture);
    }

    return "Unknown";
}

record DateEntry
{
    public string File { get; init; } = string.Empty;
    public string Entry { get; init; } = string.Empty;
    public List<string> Matches { get; init; } = new();
    public string Timestamp { get; init; } = string.Empty;
    public string? NormalizedTimestamp { get; init; }
    public string? DateKey { get; init; }
}

record GroupedEntries
{
    public string Key { get; init; } = string.Empty;
    public List<DateEntry> Entries { get; init; } = new();
}
