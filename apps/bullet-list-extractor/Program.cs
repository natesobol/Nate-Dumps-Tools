using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http.Features;
using UglyToad.PdfPig;

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

app.MapPost("/api/extract-lists", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with file uploads." });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;

    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .docx, .pdf, or .txt file." });
    }

    var responses = new List<ExtractedFile>();

    foreach (var file in files)
    {
        try
        {
            responses.Add(await ProcessFileAsync(file));
        }
        catch (Exception ex)
        {
            responses.Add(new ExtractedFile
            {
                FileName = file.FileName,
                Error = $"Failed to process file: {ex.Message}"
            });
        }
    }

    var totalItems = responses.Sum(r => r.Items?.Count ?? 0);

    return Results.Ok(new
    {
        files = responses,
        totalItems
    });
});

app.Run();

static async Task<ExtractedFile> ProcessFileAsync(IFormFile file)
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (extension is ".docx")
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        using var doc = WordprocessingDocument.Open(stream, false);
        var items = ExtractFromWord(doc);

        return new ExtractedFile { FileName = file.FileName, Items = items };
    }

    if (extension is ".pdf")
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        using var pdf = PdfDocument.Open(stream);
        var builder = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        var items = ExtractFromPlainText(builder.ToString());
        return new ExtractedFile { FileName = file.FileName, Items = items };
    }

    if (extension is ".txt")
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        var items = ExtractFromPlainText(content);
        return new ExtractedFile { FileName = file.FileName, Items = items };
    }

    return new ExtractedFile
    {
        FileName = file.FileName,
        Error = "Unsupported file type. Upload .docx, .pdf, or .txt files."
    };
}

static List<ListItem> ExtractFromWord(WordprocessingDocument document)
{
    var items = new List<ListItem>();
    var numberingPart = document.MainDocumentPart?.NumberingDefinitionsPart?.Numbering;

    if (numberingPart is null)
    {
        return items;
    }

    var counters = new Dictionary<(int NumId, int Level), int>();

    foreach (var paragraph in document.MainDocumentPart!.Document.Body!.Elements<Paragraph>())
    {
        var numbering = paragraph.ParagraphProperties?.NumberingProperties;
        if (numbering is null)
        {
            continue;
        }

        var text = paragraph.InnerText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            continue;
        }

        var level = (int?)numbering.NumberingLevelReference?.Val?.Value ?? 0;
        var numId = (int?)numbering.NumberingId?.Val?.Value ?? 0;

        var marker = ResolveMarker(numberingPart, numId, level, counters);

        items.Add(new ListItem
        {
            Marker = marker,
            Text = text,
            Level = level
        });
    }

    return items;
}

static string ResolveMarker(Numbering numberingPart, int numId, int level, Dictionary<(int NumId, int Level), int> counters)
{
    var abstractLevel = FindLevel(numberingPart, numId, level);

    if (abstractLevel is null)
    {
        return "•";
    }

    var format = abstractLevel.NumberingFormat?.Val?.Value;

    if (format == NumberFormatValues.Bullet)
    {
        var levelText = abstractLevel.LevelText?.Val?.Value;
        if (!string.IsNullOrWhiteSpace(levelText))
        {
            var replaced = levelText.Replace($"%{level + 1}", "•").Trim();
            return string.IsNullOrWhiteSpace(replaced) ? "•" : replaced;
        }

        var bullet = abstractLevel.LevelSymbol?.Val?.Value;
        return string.IsNullOrWhiteSpace(bullet) ? "•" : bullet!;
    }

    var start = (int?)abstractLevel.StartNumberingValue?.Val?.Value ?? 1;
    var key = (numId, level);
    if (!counters.TryGetValue(key, out var current))
    {
        current = start - 1;
    }

    current++;
    counters[key] = current;

    var formatted = FormatNumber(format, current);
    var levelTextTemplate = abstractLevel.LevelText?.Val?.Value ?? $"%{level + 1}.";

    var marker = levelTextTemplate.Replace($"%{level + 1}", formatted);
    return string.IsNullOrWhiteSpace(marker) ? formatted : marker.Trim();
}

static Level? FindLevel(Numbering numberingPart, int numId, int level)
{
    var numInstance = numberingPart.Elements<NumberingInstance>()
        .FirstOrDefault(n => (int?)n.NumberID?.Value == numId);

    var abstractId = (int?)numInstance?.AbstractNumId?.Val?.Value;
    if (abstractId is null)
    {
        return null;
    }

    var abstractNum = numberingPart.Elements<AbstractNum>()
        .FirstOrDefault(n => (int?)n.AbstractNumberId?.Value == abstractId);

    return abstractNum?.Elements<Level>()
        .FirstOrDefault(l => (int?)l.LevelIndex?.Value == level);
}

static string FormatNumber(NumberFormatValues? format, int number)
{
    return format switch
    {
        NumberFormatValues.LowerLetter => ToAlphabet(number, false),
        NumberFormatValues.UpperLetter => ToAlphabet(number, true),
        NumberFormatValues.LowerRoman => ToRoman(number).ToLowerInvariant(),
        NumberFormatValues.UpperRoman => ToRoman(number),
        _ => number.ToString()
    };
}

static string ToAlphabet(int number, bool upper)
{
    number--;
    var result = new StringBuilder();

    while (number >= 0)
    {
        var remainder = number % 26;
        var letter = (char)(remainder + (upper ? 'A' : 'a'));
        result.Insert(0, letter);
        number = (number / 26) - 1;
    }

    return result.ToString();
}

static string ToRoman(int number)
{
    var map = new (int Value, string Numeral)[]
    {
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
        (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
        (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
    };

    var result = new StringBuilder();
    foreach (var entry in map)
    {
        while (number >= entry.Value)
        {
            result.Append(entry.Numeral);
            number -= entry.Value;
        }
    }

    return result.ToString();
}

static List<ListItem> ExtractFromPlainText(string content)
{
    var items = new List<ListItem>();

    if (string.IsNullOrWhiteSpace(content))
    {
        return items;
    }

    var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    var pattern = new Regex(
        "^(?<indent>\\s*)(?<marker>(?:[\\u2022\\u25E6\\u2043\\u2219•\\-*]+|\\d+[.)]|\\d+\\)|[a-zA-Z][.)]))\\s+(?<text>.+)$",
        RegexOptions.Compiled);

    foreach (var raw in lines)
    {
        var line = raw.TrimEnd();
        var match = pattern.Match(line);
        if (!match.Success)
        {
            continue;
        }

        var indent = match.Groups["indent"].Value;
        var marker = match.Groups["marker"].Value.Trim();
        var text = match.Groups["text"].Value.Trim();
        var level = Math.Min(5, indent.Length / 2);

        items.Add(new ListItem
        {
            Marker = marker,
            Text = text,
            Level = level
        });
    }

    return items;
}

record ExtractedFile
{
    public string FileName { get; init; } = string.Empty;
    public List<ListItem>? Items { get; init; }
    public string? Error { get; init; }
}

record ListItem
{
    public string Marker { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public int Level { get; init; }
}
