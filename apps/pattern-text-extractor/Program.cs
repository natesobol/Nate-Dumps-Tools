using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using HtmlAgilityPack;

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
        return Results.BadRequest(new { error = "Expected multipart/form-data with files and a pattern." });
    }

    var form = await request.ReadFormAsync();
    var rawPattern = form["pattern"].FirstOrDefault()?.Trim();

    if (string.IsNullOrWhiteSpace(rawPattern))
    {
        return Results.BadRequest(new { error = "Provide a regex or keyword pattern." });
    }

    var useRegex = bool.TryParse(form["useRegex"], out var regexFlag) && regexFlag;
    var caseSensitive = bool.TryParse(form["caseSensitive"], out var caseFlag) && caseFlag;

    var options = RegexOptions.Compiled | RegexOptions.Multiline;
    if (!caseSensitive)
    {
        options |= RegexOptions.IgnoreCase;
    }

    var finalPattern = BuildPattern(rawPattern, useRegex);
    Regex compiled;

    try
    {
        compiled = new Regex(finalPattern, options);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Invalid pattern: {ex.Message}" });
    }

    if (form.Files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .txt, .csv, .html, or .docx file." });
    }

    var results = new List<FileResult>();

    foreach (var file in form.Files)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        try
        {
            var content = await ReadContentAsync(file, extension);
            var matches = ExtractMatches(content, compiled);

            results.Add(new FileResult
            {
                File = file.FileName,
                Extension = extension.TrimStart('.'),
                MatchCount = matches.Count,
                Matches = matches
            });
        }
        catch (Exception ex)
        {
            results.Add(new FileResult
            {
                File = file.FileName,
                Extension = extension.TrimStart('.'),
                MatchCount = 0,
                Matches = new List<MatchResult>(),
                Error = ex.Message
            });
        }
    }

    var totalMatches = results.Sum(r => r.MatchCount);

    return Results.Ok(new
    {
        pattern = rawPattern,
        regex = useRegex,
        caseSensitive,
        totalMatches,
        files = results
    });
});

app.Run();

static string BuildPattern(string rawPattern, bool useRegex)
{
    if (useRegex)
    {
        return rawPattern;
    }

    var keywords = rawPattern
        .Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(k => k.Trim())
        .Where(k => !string.IsNullOrWhiteSpace(k))
        .ToArray();

    if (keywords.Length == 0)
    {
        throw new InvalidOperationException("No keywords provided.");
    }

    return string.Join("|", keywords.Select(Regex.Escape));
}

static async Task<string> ReadContentAsync(IFormFile file, string extension)
{
    switch (extension)
    {
        case ".txt":
        case ".csv":
        case ".html":
        case ".htm":
            return await ReadTextBasedFileAsync(file, extension);
        case ".docx":
            return await ReadDocxAsync(file);
        default:
            throw new InvalidOperationException("Unsupported file type. Upload .txt, .csv, .html, or .docx files.");
    }
}

static async Task<string> ReadTextBasedFileAsync(IFormFile file, string extension)
{
    using var reader = new StreamReader(file.OpenReadStream());
    var content = await reader.ReadToEndAsync();

    if (extension is ".html" or ".htm")
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var removableNodes = doc.DocumentNode.SelectNodes("//script|//style");
        if (removableNodes is not null)
        {
            foreach (var node in removableNodes)
            {
                node.Remove();
            }
        }

        var text = doc.DocumentNode.InnerText;
        return HtmlEntity.DeEntitize(text);
    }

    return content;
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

static List<MatchResult> ExtractMatches(string content, Regex pattern)
{
    var matches = new List<MatchResult>();
    var normalized = content.Replace("\r\n", "\n");
    var lines = normalized.Split('\n');

    for (var i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        var lineMatches = pattern.Matches(line);

        foreach (Match match in lineMatches)
        {
            var context = line.Trim();
            if (context.Length > 260)
            {
                context = context[..260] + "…";
            }

            matches.Add(new MatchResult
            {
                Value = match.Value,
                LineNumber = i + 1,
                Context = context
            });
        }
    }

    return matches;
}

record MatchResult
{
    public string Value { get; init; } = string.Empty;
    public int LineNumber { get; init; }
    public string Context { get; init; } = string.Empty;
}

record FileResult
{
    public string File { get; init; } = string.Empty;
    public string Extension { get; init; } = string.Empty;
    public int MatchCount { get; init; }
    public List<MatchResult> Matches { get; init; } = new();
    public string? Error { get; init; }
}
