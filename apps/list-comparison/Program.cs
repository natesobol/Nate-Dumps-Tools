using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/compare", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with two lists or uploads." });
    }

    var form = await request.ReadFormAsync();
    var ignoreCase = TryParseBool(form["ignoreCase"], true);
    var trimEntries = TryParseBool(form["trimEntries"], true);
    var skipEmpty = TryParseBool(form["skipEmpty"], true);

    var contentA = await ReadListContentAsync(form["listA"], form.Files.GetFile("fileA"));
    var contentB = await ReadListContentAsync(form["listB"], form.Files.GetFile("fileB"));

    if (string.IsNullOrWhiteSpace(contentA) || string.IsNullOrWhiteSpace(contentB))
    {
        return Results.BadRequest(new { error = "Provide text for both lists or upload supported documents for List A and List B." });
    }

    var listA = Normalize(contentA, trimEntries, skipEmpty);
    var listB = Normalize(contentB, trimEntries, skipEmpty);
    var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    var overlaps = listA.Intersect(listB, comparer).Distinct(comparer).OrderBy(x => x, comparer).ToList();
    var onlyA = listA.Except(listB, comparer).Distinct(comparer).OrderBy(x => x, comparer).ToList();
    var onlyB = listB.Except(listA, comparer).Distinct(comparer).OrderBy(x => x, comparer).ToList();

    return Results.Ok(new
    {
        overlaps,
        onlyA,
        onlyB,
        counts = new
        {
            listA = listA.Count,
            listB = listB.Count,
            overlap = overlaps.Count,
            uniqueA = onlyA.Count,
            uniqueB = onlyB.Count
        }
    });
});

app.Run();

static bool TryParseBool(string? value, bool fallback)
{
    return bool.TryParse(value, out var parsed) ? parsed : fallback;
}

static List<string> Normalize(string input, bool trimEntries, bool skipEmpty)
{
    var lines = Regex.Split(input, "\r?\n");
    var items = new List<string>();

    foreach (var raw in lines)
    {
        var value = trimEntries ? raw.Trim() : raw;

        if (skipEmpty && string.IsNullOrWhiteSpace(value))
        {
            continue;
        }

        items.Add(value);
    }

    return items;
}

static async Task<string?> ReadListContentAsync(string? textValue, IFormFile? file)
{
    if (file is not null && file.Length > 0)
    {
        return await ExtractTextAsync(file);
    }

    if (!string.IsNullOrWhiteSpace(textValue))
    {
        return textValue;
    }

    return null;
}

static async Task<string> ExtractTextAsync(IFormFile file)
{
    await using var stream = file.OpenReadStream();
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    return extension switch
    {
        ".docx" => await ExtractDocxTextAsync(stream),
        ".html" or ".htm" => await ExtractHtmlTextAsync(stream),
        _ => await ReadAsTextAsync(stream)
    };
}

static async Task<string> ExtractDocxTextAsync(Stream stream)
{
    using var memory = new MemoryStream();
    await stream.CopyToAsync(memory);
    memory.Position = 0;

    using var document = WordprocessingDocument.Open(memory, false);
    var body = document.MainDocumentPart?.Document.Body;

    if (body is null)
    {
        return string.Empty;
    }

    var text = new StringBuilder();

    foreach (var element in body.Descendants<Text>())
    {
        text.AppendLine(element.Text);
    }

    return text.ToString();
}

static async Task<string> ExtractHtmlTextAsync(Stream stream)
{
    using var reader = new StreamReader(stream);
    var html = await reader.ReadToEndAsync();

    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    return doc.DocumentNode.InnerText;
}

static async Task<string> ReadAsTextAsync(Stream stream)
{
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
}
