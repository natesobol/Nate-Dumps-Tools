using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http.Features;
using MimeKit;
using MsgReader.Outlook;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

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

app.MapPost("/api/extract", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with uploads or pasted text." });
    }

    var form = await request.ReadFormAsync();
    var sortMode = string.IsNullOrWhiteSpace(form["sortMode"]) ? "alphabetical" : form["sortMode"].ToString();
    var textInput = form["textInput"].ToString();
    var files = form.Files;

    if (files.Count == 0 && string.IsNullOrWhiteSpace(textInput))
    {
        return Results.BadRequest(new { error = "Provide text or upload at least one .eml, .msg, .txt, or .docx file." });
    }

    var outputs = new List<ExtractedFile>();

    if (!string.IsNullOrWhiteSpace(textInput))
    {
        var phrases = ExtractCapitalizedPhrases(textInput);
        outputs.Add(new ExtractedFile
        {
            FileName = "Pasted text",
            Phrases = phrases
        });
    }

    foreach (var file in files)
    {
        try
        {
            var text = await ExtractTextAsync(file);
            var phrases = ExtractCapitalizedPhrases(text);

            outputs.Add(new ExtractedFile
            {
                FileName = file.FileName,
                Phrases = phrases
            });
        }
        catch (Exception ex)
        {
            outputs.Add(new ExtractedFile
            {
                FileName = file.FileName,
                Error = $"Failed to process file: {ex.Message}"
            });
        }
    }

    var grouped = outputs.OrderBy(o => o.FileName, StringComparer.OrdinalIgnoreCase).ToList();

    if (sortMode.Equals("alphabetical", StringComparison.OrdinalIgnoreCase))
    {
        var merged = grouped
            .Where(o => o.Phrases is not null)
            .SelectMany(o => o.Phrases ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Results.Ok(new
        {
            mode = "alphabetical",
            phrases = merged,
            files = grouped
        });
    }

    return Results.Ok(new
    {
        mode = "byFile",
        files = grouped
    });
});

app.Run();

static async Task<string> ExtractTextAsync(IFormFile file)
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    return extension switch
    {
        ".eml" => await ExtractEmlTextAsync(file),
        ".msg" => await ExtractMsgTextAsync(file),
        ".docx" => await ExtractDocxTextAsync(file),
        _ => await ReadAsTextAsync(file)
    };
}

static async Task<string> ExtractEmlTextAsync(IFormFile file)
{
    await using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Position = 0;

    var message = MimeMessage.Load(stream);
    var body = ExtractBodyText(message.TextBody, message.HtmlBody);

    return string.Join("\n\n", new[] { message.Subject, body }.Where(v => !string.IsNullOrWhiteSpace(v)));
}

static async Task<string> ExtractMsgTextAsync(IFormFile file)
{
    var tempPath = Path.GetTempFileName();

    await using (var fs = File.OpenWrite(tempPath))
    {
        await file.CopyToAsync(fs);
    }

    using var message = new Storage.Message(tempPath, FileAccess.Read);
    var body = ExtractBodyText(message.BodyText, message.BodyRtf);
    File.Delete(tempPath);

    return string.Join("\n\n", new[] { message.Subject, body }.Where(v => !string.IsNullOrWhiteSpace(v)));
}

static async Task<string> ExtractDocxTextAsync(IFormFile file)
{
    await using var uploadStream = file.OpenReadStream();
    using var memory = new MemoryStream();
    await uploadStream.CopyToAsync(memory);
    memory.Position = 0;

    using var document = WordprocessingDocument.Open(memory, false);
    var body = document.MainDocumentPart?.Document.Body;

    if (body is null)
    {
        return string.Empty;
    }

    var builder = new StringBuilder();

    foreach (var text in body.Descendants<Text>())
    {
        builder.AppendLine(text.Text);
    }

    return builder.ToString();
}

static async Task<string> ReadAsTextAsync(IFormFile file)
{
    await using var stream = file.OpenReadStream();
    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
    return await reader.ReadToEndAsync();
}

static List<string> ExtractCapitalizedPhrases(string input)
{
    const string pattern = "\\b(?:[A-Z][a-z]+|[A-Z]{2,})(?:\\s+(?:[A-Z][a-z]+|[A-Z]{2,}|of|and|for|the|in|on|at|to|from|with|without|&|de|di|da|la|le|van|von|der))+";
    var matches = Regex.Matches(input, pattern);
    var phrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var connectorSet = ConnectorWords();

    foreach (Match match in matches)
    {
        var normalized = NormalizeWhitespace(match.Value);
        var cleaned = TrimConnectorEdges(normalized, connectorSet);
        var capitalizedCount = Regex.Matches(cleaned, "\\b(?:[A-Z][a-z]+|[A-Z]{2,})\\b").Count;

        if (capitalizedCount >= 2 && !string.IsNullOrWhiteSpace(cleaned))
        {
            phrases.Add(cleaned);
        }
    }

    return phrases.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
}

static string NormalizeWhitespace(string value)
{
    var condensed = Regex.Replace(value, "\\s+", " ");
    return condensed.Trim().Trim('.', ',', ';', ':', '"', '\'', '!', '?');
}

static string TrimConnectorEdges(string phrase, HashSet<string> connectors)
{
    var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

    while (words.Count > 0 && connectors.Contains(words[0].ToLowerInvariant()))
    {
        words.RemoveAt(0);
    }

    while (words.Count > 0 && connectors.Contains(words[^1].ToLowerInvariant()))
    {
        words.RemoveAt(words.Count - 1);
    }

    return string.Join(' ', words);
}

static HashSet<string> ConnectorWords() => new(StringComparer.OrdinalIgnoreCase)
{
    "of", "and", "for", "the", "in", "on", "at", "to", "from", "with", "without", "&", "de", "di", "da", "la", "le", "van", "von", "der"
};

static string ExtractBodyText(string? textBody, string? htmlBody)
{
    if (!string.IsNullOrWhiteSpace(textBody))
    {
        return textBody.Trim();
    }

    if (string.IsNullOrWhiteSpace(htmlBody))
    {
        return string.Empty;
    }

    var doc = new HtmlDocument();
    doc.LoadHtml(htmlBody);
    var innerText = doc.DocumentNode.InnerText;

    return WebUtility.HtmlDecode(NormalizeWhitespace(innerText));
}

record ExtractedFile
{
    public required string FileName { get; init; }
    public List<string>? Phrases { get; init; }
    public string? Error { get; init; }
}
