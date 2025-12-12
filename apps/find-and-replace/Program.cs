using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/replace", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with replacement options." });
    }

    var form = await request.ReadFormAsync();
    var pattern = form["pattern"].ToString();
    var replacement = form["replacement"].ToString();
    var mode = form["mode"].ToString();
    var caseSensitive = form["caseSensitive"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);

    if (string.IsNullOrWhiteSpace(pattern))
    {
        return Results.BadRequest(new { error = "A search term or regular expression pattern is required." });
    }

    var useRegex = string.Equals(mode, "regex", StringComparison.OrdinalIgnoreCase);
    var regexOptions = RegexOptions.Multiline;
    if (!caseSensitive)
    {
        regexOptions |= RegexOptions.IgnoreCase;
    }

    Regex? compiledRegex = null;
    if (useRegex)
    {
        try
        {
            compiledRegex = new Regex(pattern, regexOptions);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = "Invalid regular expression pattern.", details = ex.Message });
        }
    }

    var plainComparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    var textInput = form["text"].ToString();
    (string replaced, int matches)? textResult = null;
    if (!string.IsNullOrWhiteSpace(textInput))
    {
        textResult = ReplaceContent(textInput, pattern, replacement, useRegex, compiledRegex, plainComparison);
    }

    var fileResults = new List<object>();
    foreach (var file in form.Files)
    {
        if (file.Length == 0)
        {
            fileResults.Add(new
            {
                file = file.FileName,
                error = "File was empty.",
            });
            continue;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        switch (extension)
        {
            case ".docx":
            {
                try
                {
                    var docxResult = await ReplaceDocxAsync(file, replacement, useRegex, compiledRegex, plainComparison, pattern);
                    fileResults.Add(docxResult);
                }
                catch (Exception ex)
                {
                    fileResults.Add(new
                    {
                        file = file.FileName,
                        error = $"Failed to process .docx: {ex.Message}"
                    });
                }

                break;
            }
            default:
            {
                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var content = await reader.ReadToEndAsync();

                var replaced = ReplaceContent(content, pattern, replacement, useRegex, compiledRegex, plainComparison);
                fileResults.Add(new
                {
                    file = file.FileName,
                    matchCount = replaced.matches,
                    lengthBefore = content.Length,
                    lengthAfter = replaced.replaced.Length,
                    updatedContent = replaced.replaced,
                    contentType = GetContentType(extension)
                });

                break;
            }
        }
    }

    if (textResult is null && fileResults.Count == 0)
    {
        return Results.BadRequest(new { error = "Provide text or at least one file to process." });
    }

    return Results.Ok(new
    {
        mode = useRegex ? "regex" : "text",
        caseSensitive,
        pattern,
        replacement,
        textResult = textResult is null
            ? null
            : new
            {
                matchCount = textResult.Value.matches,
                lengthBefore = textInput.Length,
                lengthAfter = textResult.Value.replaced.Length,
                updatedContent = textResult.Value.replaced
            },
        fileResults
    });
});

app.Run();

static (string replaced, int matches) ReplaceContent(
    string content,
    string pattern,
    string replacement,
    bool useRegex,
    Regex? compiledRegex,
    StringComparison plainComparison)
{
    if (useRegex)
    {
        if (compiledRegex is null)
        {
            return (content, 0);
        }

        var matches = compiledRegex.Matches(content).Count;
        var replaced = compiledRegex.Replace(content, replacement);
        return (replaced, matches);
    }

    return ReplacePlain(content, pattern, replacement, plainComparison);
}

static (string replaced, int matches) ReplacePlain(
    string content,
    string search,
    string replacement,
    StringComparison comparison)
{
    if (string.IsNullOrEmpty(search))
    {
        return (content, 0);
    }

    var builder = new StringBuilder();
    var index = 0;
    var matches = 0;

    while (index < content.Length)
    {
        var next = content.IndexOf(search, index, comparison);
        if (next < 0)
        {
            builder.Append(content.AsSpan(index));
            break;
        }

        builder.Append(content.AsSpan(index, next - index));
        builder.Append(replacement);

        index = next + search.Length;
        matches++;
    }

    return (builder.ToString(), matches);
}

static async Task<object> ReplaceDocxAsync(
    IFormFile file,
    string replacement,
    bool useRegex,
    Regex? compiledRegex,
    StringComparison plainComparison,
    string pattern)
{
    await using var input = new MemoryStream();
    await file.CopyToAsync(input);
    input.Position = 0;

    await using var output = new MemoryStream();
    input.CopyTo(output);
    output.Position = 0;

    using (var wordDoc = WordprocessingDocument.Open(output, true))
    {
        var texts = wordDoc.MainDocumentPart?.Document?.Descendants<Text>() ?? Enumerable.Empty<Text>();
        var totalMatches = 0;
        foreach (var text in texts)
        {
            var (replaced, matches) = ReplaceContent(text.Text, pattern, replacement, useRegex, compiledRegex, plainComparison);
            text.Text = replaced;
            totalMatches += matches;
        }

        wordDoc.MainDocumentPart?.Document?.Save();

        return new
        {
            file = file.FileName,
            matchCount = totalMatches,
            lengthBefore = input.Length,
            lengthAfter = output.Length,
            updatedContentBase64 = Convert.ToBase64String(output.ToArray()),
            contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }
}

static string GetContentType(string extension) => extension switch
{
    ".json" => "application/json",
    ".html" => "text/html",
    ".htm" => "text/html",
    ".xml" => "application/xml",
    ".rtf" => "application/rtf",
    ".md" => "text/markdown",
    ".markdown" => "text/markdown",
    ".csv" => "text/csv",
    ".tsv" => "text/tab-separated-values",
    _ => "text/plain"
};
