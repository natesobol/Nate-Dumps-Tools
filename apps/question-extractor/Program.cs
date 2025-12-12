using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http.Features;
using UglyToad.PdfPig;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 75; // 75 MB
});

builder.Services.AddResponseCompression();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract-questions", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with files." });
    }

    var form = await request.ReadFormAsync();
    var includeContext = string.Equals(form["includeContext"], "true", StringComparison.OrdinalIgnoreCase);

    if (form.Files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .pdf, .txt, .docx, or .csv file." });
    }

    var responses = new List<FileExtractionResult>();

    foreach (var file in form.Files)
    {
        try
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (file.Length == 0)
            {
                responses.Add(new FileExtractionResult
                {
                    FileName = file.FileName,
                    Error = "File was empty."
                });

                continue;
            }

            string content;

            switch (extension)
            {
                case ".pdf":
                    content = await ReadPdfAsync(file);
                    break;
                case ".docx":
                    content = await ReadDocxAsync(file);
                    break;
                case ".txt":
                case ".csv":
                    content = await ReadTextAsync(file);
                    break;
                default:
                    responses.Add(new FileExtractionResult
                    {
                        FileName = file.FileName,
                        Error = "Unsupported file type."
                    });
                    continue;
            }

            var questions = ExtractQuestions(content, includeContext);
            responses.Add(new FileExtractionResult
            {
                FileName = file.FileName,
                Questions = questions
            });
        }
        catch (Exception ex)
        {
            responses.Add(new FileExtractionResult
            {
                FileName = file.FileName,
                Error = $"Failed to process file: {ex.Message}"
            });
        }
    }

    var totalQuestions = responses.Sum(r => r.Questions?.Count ?? 0);

    return Results.Ok(new
    {
        files = responses,
        totalQuestions
    });
});

app.Run();

static async Task<string> ReadPdfAsync(IFormFile file)
{
    await using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Position = 0;

    using var document = PdfDocument.Open(stream);
    var builder = new StringBuilder();
    foreach (var page in document.GetPages())
    {
        builder.AppendLine(page.Text);
    }

    return builder.ToString();
}

static async Task<string> ReadDocxAsync(IFormFile file)
{
    await using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Position = 0;

    using var document = WordprocessingDocument.Open(stream, false);
    var body = document.MainDocumentPart?.Document.Body;

    if (body is null)
    {
        return string.Empty;
    }

    var builder = new StringBuilder();
    foreach (var paragraph in body.Elements<Paragraph>())
    {
        var text = paragraph.InnerText;
        if (!string.IsNullOrWhiteSpace(text))
        {
            builder.AppendLine(text);
        }
    }

    return builder.ToString();
}

static async Task<string> ReadTextAsync(IFormFile file)
{
    await using var stream = file.OpenReadStream();
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
    return await reader.ReadToEndAsync();
}

static List<QuestionResult> ExtractQuestions(string content, bool includeContext)
{
    var results = new List<QuestionResult>();

    if (string.IsNullOrWhiteSpace(content))
    {
        return results;
    }

    var normalized = Regex.Replace(content, "\r", string.Empty);

    foreach (Match match in Regex.Matches(normalized, "\\?", RegexOptions.Singleline))
    {
        var question = SliceQuestion(normalized, match.Index);

        if (question is null)
        {
            continue;
        }

        var cleaned = question.Value.Trim();
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length < 2)
        {
            continue;
        }

        if (!cleaned.EndsWith("?"))
        {
            continue;
        }

        var context = includeContext ? ExtractContext(normalized, question.Start, question.Length) : null;

        results.Add(new QuestionResult
        {
            Question = cleaned,
            Index = question.Start,
            Context = context
        });
    }

    // Remove duplicates while preserving order
    var distinct = new List<QuestionResult>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var item in results)
    {
        if (seen.Add(item.Question))
        {
            distinct.Add(item);
        }
    }

    return distinct;
}

static (string Value, int Start, int Length)? SliceQuestion(string text, int questionMarkIndex)
{
    var start = questionMarkIndex;
    while (start > 0)
    {
        var c = text[start - 1];
        if (c is '.' or '!' or '?' or '\n' or '\r')
        {
            break;
        }
        start--;
    }

    var end = questionMarkIndex;
    while (end + 1 < text.Length && char.IsWhiteSpace(text[end + 1]))
    {
        end++;
    }

    var length = end - start + 1;
    if (length <= 0 || start < 0 || start + length > text.Length)
    {
        return null;
    }

    return (text.Substring(start, length), start, length);
}

static string ExtractContext(string text, int start, int length, int radius = 180)
{
    var contextStart = Math.Max(0, start - radius);
    var contextEnd = Math.Min(text.Length, start + length + radius);
    var snippet = text.Substring(contextStart, contextEnd - contextStart).Trim();
    return Regex.Replace(snippet, "\\s+", " ").Trim();
}

sealed class FileExtractionResult
{
    public string FileName { get; set; } = string.Empty;
    public List<QuestionResult>? Questions { get; set; }
    public string? Error { get; set; }
}

sealed class QuestionResult
{
    public string Question { get; set; } = string.Empty;
    public int Index { get; set; }
    public string? Context { get; set; }
}
