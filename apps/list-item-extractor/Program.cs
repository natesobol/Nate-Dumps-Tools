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

app.MapPost("/api/list-item-extractor", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with file uploads." });
    }

    var form = await request.ReadFormAsync();
    if (form.Files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .docx, .pdf, .txt, or .md file." });
    }

    var responses = new List<ExtractedFile>();

    foreach (var file in form.Files)
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

    if (extension is ".txt" || extension is ".md")
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
        Error = "Unsupported file type. Upload .docx, .pdf, .txt, or .md files."
    };
}

static List<ListItemResult> ExtractFromWord(WordprocessingDocument document)
{
    var items = new List<ListItemResult>();
    var body = document.MainDocumentPart?.Document.Body;

    if (body is null)
    {
        return items;
    }

    var currentHeading = string.Empty;

    foreach (var paragraph in body.Elements<Paragraph>())
    {
        var text = paragraph.InnerText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            continue;
        }

        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (!string.IsNullOrWhiteSpace(styleId) && styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
        {
            currentHeading = text;
            continue;
        }

        var numbering = paragraph.ParagraphProperties?.NumberingProperties;
        if (numbering is null)
        {
            continue;
        }

        var level = (int?)numbering.NumberingLevelReference?.Val?.Value ?? 0;

        items.Add(new ListItemResult
        {
            Item = text,
            ListLevel = level,
            ParentHeading = currentHeading
        });
    }

    return items;
}

static List<ListItemResult> ExtractFromPlainText(string content)
{
    var results = new List<ListItemResult>();
    var heading = string.Empty;
    var indentStack = new List<int>();

    foreach (var rawLine in content.Split('\n'))
    {
        var line = rawLine.Replace("\r", string.Empty);

        var headingMatch = Regex.Match(line, "^\\s{0,3}(#{1,6})\\s+(?<heading>.+)$");
        if (headingMatch.Success)
        {
            heading = headingMatch.Groups["heading"].Value.Trim();
            continue;
        }

        var match = Regex.Match(line, "^(?<indent>[\\s]*)(?<marker>(?:[-*+•])|(?:\\d+[.)]))\\s+(?<text>.+)$");
        if (!match.Success)
        {
            continue;
        }

        var indent = match.Groups["indent"].Value.Replace("\t", "    ");
        var indentLength = indent.Length;

        if (indentStack.Count == 0)
        {
            indentStack.Add(indentLength);
        }
        else if (indentLength > indentStack.Last())
        {
            indentStack.Add(indentLength);
        }
        else
        {
            while (indentStack.Count > 1 && indentLength < indentStack.Last())
            {
                indentStack.RemoveAt(indentStack.Count - 1);
            }

            if (indentStack.Count == 0 || indentLength < indentStack.Last())
            {
                indentStack.Clear();
                indentStack.Add(indentLength);
            }
        }

        var level = Math.Max(0, indentStack.Count - 1);
        var text = match.Groups["text"].Value.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            continue;
        }

        results.Add(new ListItemResult
        {
            Item = text,
            ListLevel = level,
            ParentHeading = heading
        });
    }

    return results;
}

class ExtractedFile
{
    public string FileName { get; set; } = string.Empty;
    public List<ListItemResult>? Items { get; set; }
    public string? Error { get; set; }
}

class ListItemResult
{
    public string Item { get; set; } = string.Empty;
    public int ListLevel { get; set; }
    public string ParentHeading { get; set; } = string.Empty;
}
