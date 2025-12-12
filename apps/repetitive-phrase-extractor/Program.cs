using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using HtmlAgilityPack;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/analyze", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    if (form.Files.Count == 0 && string.IsNullOrWhiteSpace(form["text"]))
    {
        return Results.BadRequest(new { error = "Upload at least one file or provide inline text." });
    }

    var items = new List<object>();
    var totalRepetitions = 0;

    if (!string.IsNullOrWhiteSpace(form["text"]))
    {
        var inlineText = form["text"].ToString();
        var analysis = AnalyzeContent(inlineText);
        totalRepetitions += analysis.TotalRepeated;

        items.Add(new
        {
            source = "Inline text",
            kind = "text",
            analysis
        });
    }

    foreach (var file in form.Files)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        var item = new
        {
            source = file.FileName,
            kind = extension.TrimStart('.'),
            analysis = (AnalysisResult?)null,
            error = (string?)null
        };

        try
        {
            string content = extension switch
            {
                ".txt" => await ReadPlainTextAsync(file),
                ".docx" => await ReadDocxAsync(file),
                ".rtf" => await ReadRtfAsync(file),
                ".html" or ".htm" => await ReadHtmlAsync(file),
                _ => throw new InvalidOperationException("Unsupported file type.")
            };

            var analysis = AnalyzeContent(content);
            totalRepetitions += analysis.TotalRepeated;

            item = new
            {
                item.source,
                item.kind,
                analysis,
                error = (string?)null
            };
        }
        catch (Exception ex)
        {
            item = new
            {
                item.source,
                item.kind,
                item.analysis,
                error = ex.Message
            };
        }

        items.Add(item);
    }

    return Results.Ok(new
    {
        totalRepetitions,
        results = items
    });
});

app.Run();

static AnalysisResult AnalyzeContent(string content)
{
    var normalized = NormalizeWhitespace(content);
    var sentenceMap = new Dictionary<string, OccurrenceInfo>(StringComparer.OrdinalIgnoreCase);
    var phraseMap = new Dictionary<string, OccurrenceInfo>(StringComparer.OrdinalIgnoreCase);

    var lines = normalized.Split('\n');
    for (var i = 0; i < lines.Length; i++)
    {
        var lineNumber = i + 1;
        var line = lines[i];
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        foreach (var sentence in SplitIntoSentences(line))
        {
            var wordCount = CountWords(sentence);
            if (wordCount < 5)
            {
                continue;
            }

            var sentenceKey = NormalizeForKey(sentence);
            AddOccurrence(sentenceMap, sentenceKey, sentence, lineNumber);

            var tokens = Tokenize(sentence);
            for (var size = 3; size <= Math.Min(8, tokens.Count); size++)
            {
                for (var start = 0; start <= tokens.Count - size; start++)
                {
                    var slice = tokens.Skip(start).Take(size).ToList();
                    if (slice.Count < size)
                    {
                        continue;
                    }

                    var phraseText = string.Join(' ', slice);
                    if (phraseText.Length < 12)
                    {
                        continue;
                    }

                    var phraseKey = NormalizeForKey(phraseText);
                    AddOccurrence(phraseMap, phraseKey, phraseText, lineNumber);
                }
            }
        }
    }

    var repeatedSentences = sentenceMap.Values
        .Where(x => x.LineNumbers.Count > 1)
        .OrderByDescending(x => x.LineNumbers.Count)
        .ThenBy(x => x.Text)
        .Select(x => new Repetition(x.Text, x.LineNumbers.Count, x.LineNumbers.OrderBy(n => n).ToList()))
        .ToList();

    var repeatedPhrases = phraseMap.Values
        .Where(x => x.LineNumbers.Count > 1)
        .OrderByDescending(x => x.LineNumbers.Count)
        .ThenBy(x => x.Text)
        .Select(x => new Repetition(x.Text, x.LineNumbers.Count, x.LineNumbers.OrderBy(n => n).ToList()))
        .ToList();

    return new AnalysisResult(repeatedSentences, repeatedPhrases);
}

static string NormalizeWhitespace(string content)
{
    var cleaned = Regex.Replace(content, "\\r\\n", "\n");
    cleaned = Regex.Replace(cleaned, "[\\t ]+", " ");
    return cleaned.Trim();
}

static IEnumerable<string> SplitIntoSentences(string input)
{
    var parts = Regex.Split(input, "(?<=[\\.\\!\\?;:])\\s+");
    foreach (var part in parts)
    {
        var sentence = part.Trim();
        if (!string.IsNullOrWhiteSpace(sentence))
        {
            yield return sentence;
        }
    }
}

static List<string> Tokenize(string sentence)
{
    return Regex.Matches(sentence, "[\\p{L}0-9']+")
        .Select(m => m.Value)
        .ToList();
}

static int CountWords(string sentence) => Tokenize(sentence).Count;

static string NormalizeForKey(string text)
{
    var collapsed = Regex.Replace(text.Trim(), "\\s+", " ");
    return collapsed.ToLowerInvariant();
}

static void AddOccurrence(Dictionary<string, OccurrenceInfo> map, string key, string original, int lineNumber)
{
    if (!map.TryGetValue(key, out var info))
    {
        info = new OccurrenceInfo(original.Trim());
        map[key] = info;
    }

    info.LineNumbers.Add(lineNumber);
}

static async Task<string> ReadPlainTextAsync(IFormFile file)
{
    using var reader = new StreamReader(file.OpenReadStream());
    return await reader.ReadToEndAsync();
}

static async Task<string> ReadDocxAsync(IFormFile file)
{
    await using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Seek(0, SeekOrigin.Begin);

    using var wordDoc = WordprocessingDocument.Open(stream, false);
    var body = wordDoc.MainDocumentPart?.Document.Body;
    return body?.InnerText ?? string.Empty;
}

static async Task<string> ReadRtfAsync(IFormFile file)
{
    await using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Seek(0, SeekOrigin.Begin);

    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
    var rtfContent = await reader.ReadToEndAsync();
    var html = RtfPipe.Rtf.ToHtml(rtfContent);
    return ExtractTextFromHtml(html);
}

static async Task<string> ReadHtmlAsync(IFormFile file)
{
    await using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Seek(0, SeekOrigin.Begin);

    using var reader = new StreamReader(stream);
    var htmlContent = await reader.ReadToEndAsync();
    return ExtractTextFromHtml(htmlContent);
}

static string ExtractTextFromHtml(string html)
{
    var doc = new HtmlDocument();
    doc.LoadHtml(html);
    return doc.DocumentNode.InnerText ?? string.Empty;
}

record AnalysisResult(List<Repetition> Sentences, List<Repetition> Phrases)
{
    public int TotalRepeated => Sentences.Count + Phrases.Count;
}

record Repetition(string Text, int Count, List<int> Lines);

class OccurrenceInfo
{
    public OccurrenceInfo(string text)
    {
        Text = text;
    }

    public string Text { get; }
    public HashSet<int> LineNumbers { get; } = new();
}
