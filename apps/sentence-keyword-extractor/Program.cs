using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http.Features;
using RtfPipe;
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

app.MapPost("/api/extract-sentences", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with file uploads." });
    }

    var form = await request.ReadFormAsync();
    var keywordsRaw = form["keywords"].ToString();
    var keywords = keywordsRaw
        .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(k => k.Trim())
        .Where(k => !string.IsNullOrWhiteSpace(k))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (keywords.Count == 0)
    {
        return Results.BadRequest(new { error = "Enter at least one keyword to search for." });
    }

    var files = form.Files;

    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .txt, .docx, .rtf, .md, or .pdf file." });
    }

    var responses = new List<FileResult>();

    foreach (var file in files)
    {
        try
        {
            var text = await ExtractTextAsync(file);
            var sentences = ExtractSentences(text, keywords);

            responses.Add(new FileResult
            {
                FileName = file.FileName,
                Matches = sentences,
                MatchCount = sentences.Count
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
        .SelectMany(r => r.Matches!.Select(m => new FlatSentence
        {
            File = r.FileName,
            Sentence = m.Sentence,
            MatchedKeywords = m.MatchedKeywords
        }))
        .ToList();

    var csv = BuildCsv(flattened);

    return Results.Ok(new
    {
        files = responses,
        totalMatches = flattened.Count,
        csv
    });
});

app.Run();

static async Task<string> ExtractTextAsync(IFormFile file)
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    switch (extension)
    {
        case ".txt":
        case ".md":
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                return await reader.ReadToEndAsync();
            }
        case ".rtf":
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                var rtf = await reader.ReadToEndAsync();
                return Rtf.ToPlainText(rtf);
            }
        case ".docx":
            await using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                ms.Position = 0;

                using var wordDoc = WordprocessingDocument.Open(ms, false);
                var body = wordDoc.MainDocumentPart?.Document.Body;
                if (body is null)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder();
                foreach (var paragraph in body.Descendants<Paragraph>())
                {
                    var text = paragraph.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }

                return sb.ToString();
            }
        case ".pdf":
            await using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                ms.Position = 0;

                using var pdf = PdfDocument.Open(ms);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    sb.AppendLine(page.Text);
                }

                return sb.ToString();
            }
        default:
            throw new NotSupportedException($"Unsupported file type: {extension}. Use .txt, .docx, .rtf, .md, or .pdf.");
    }
}

static List<SentenceMatch> ExtractSentences(string text, List<string> keywords)
{
    var normalizedKeywords = keywords
        .Where(k => !string.IsNullOrWhiteSpace(k))
        .Select(k => k.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (normalizedKeywords.Count == 0)
    {
        return new List<SentenceMatch>();
    }

    var cleanedText = Regex.Replace(text, "\n+", " ");
    var segments = Regex.Split(cleanedText, @"(?<=[\.!?])\s+");

    var matches = new List<SentenceMatch>();

    foreach (var segment in segments)
    {
        var sentence = segment.Trim();
        if (string.IsNullOrWhiteSpace(sentence))
        {
            continue;
        }

        var matched = normalizedKeywords
            .Where(k => sentence.Contains(k, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matched.Count > 0)
        {
            matches.Add(new SentenceMatch
            {
                Sentence = sentence,
                MatchedKeywords = matched
            });
        }
    }

    return matches;
}

static string BuildCsv(IEnumerable<FlatSentence> sentences)
{
    var sb = new StringBuilder();
    sb.AppendLine("File,Sentence,MatchedKeywords");

    foreach (var sentence in sentences)
    {
        sb.AppendLine(string.Join(',',
            EscapeCsv(sentence.File),
            EscapeCsv(sentence.Sentence),
            EscapeCsv(string.Join("; ", sentence.MatchedKeywords))));
    }

    return sb.ToString();
}

static string EscapeCsv(string input)
{
    var needsQuotes = input.Contains('"') || input.Contains(',') || input.Contains('\n');
    var escaped = input.Replace("\"", "\"\"");
    return needsQuotes ? $"\"{escaped}\"" : escaped;
}

public record SentenceMatch
{
    public string Sentence { get; init; } = string.Empty;
    public List<string> MatchedKeywords { get; init; } = new();
}

public record FileResult
{
    public string FileName { get; init; } = string.Empty;
    public int MatchCount { get; init; }
    public List<SentenceMatch>? Matches { get; init; }
    public string? Error { get; init; }
}

public record FlatSentence
{
    public string File { get; init; } = string.Empty;
    public string Sentence { get; init; } = string.Empty;
    public List<string> MatchedKeywords { get; init; } = new();
}
