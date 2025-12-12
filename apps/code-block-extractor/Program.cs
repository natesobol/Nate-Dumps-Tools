using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using HtmlAgilityPack;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var includeInline = bool.TryParse(form["includeInline"].ToString(), out var includeInlineParsed) && includeInlineParsed;
    var consolidate = bool.TryParse(form["consolidate"].ToString(), out var consolidateParsed) && consolidateParsed;

    if (form.Files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .md, .txt, .docx, .html, or .pdf file." });
    }

    var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".docx", ".html", ".htm", ".pdf"
    };

    var extractor = new CodeBlockExtractor();
    var fileResults = new List<object>();
    var totalBlocks = 0;
    var consolidatedBuilder = new StringBuilder();

    foreach (var file in form.Files)
    {
        var extension = Path.GetExtension(file.FileName);

        if (!allowedExtensions.Contains(extension))
        {
            fileResults.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                blocks = Array.Empty<object>(),
                error = "Unsupported file type."
            });
            continue;
        }

        if (file.Length == 0)
        {
            fileResults.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                blocks = Array.Empty<object>(),
                error = "File was empty."
            });
            continue;
        }

        try
        {
            var textContent = await ReadContentAsync(file, extension);
            var blocks = extractor.Extract(textContent, extension, includeInline);
            totalBlocks += blocks.Count;

            if (consolidate && blocks.Count > 0)
            {
                consolidatedBuilder.AppendLine($"# Source: {file.FileName}");
                foreach (var block in blocks)
                {
                    consolidatedBuilder.AppendLine($"Language: {block.Language ?? "unknown"}");
                    consolidatedBuilder.AppendLine(block.Content.TrimEnd());
                    consolidatedBuilder.AppendLine();
                }
            }

            fileResults.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                blocks = blocks.Select((b, index) => new
                {
                    index = index + 1,
                    language = b.Language,
                    type = b.Type,
                    length = b.Content.Length,
                    preview = b.Content.Length > 320 ? b.Content[..320] + "…" : b.Content,
                    content = b.Content
                }),
                error = (string?)null
            });
        }
        catch (Exception ex)
        {
            fileResults.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                blocks = Array.Empty<object>(),
                error = ex.Message
            });
        }
    }

    return Results.Ok(new
    {
        filesProcessed = fileResults.Count,
        totalBlocks,
        consolidated = consolidate ? consolidatedBuilder.ToString() : null,
        results = fileResults
    });
});

app.Run();

static async Task<string> ReadContentAsync(IFormFile file, string extension)
{
    switch (extension.ToLowerInvariant())
    {
        case ".md":
        case ".markdown":
        case ".txt":
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                return await reader.ReadToEndAsync();
            }
        case ".html":
        case ".htm":
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                return await reader.ReadToEndAsync();
            }
        case ".docx":
            using (var memory = new MemoryStream())
            {
                await file.CopyToAsync(memory);
                memory.Position = 0;

                using var doc = WordprocessingDocument.Open(memory, false);
                var text = new StringBuilder();

                foreach (var element in doc.MainDocumentPart?.Document.Body?.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>() ?? Enumerable.Empty<DocumentFormat.OpenXml.Wordprocessing.Text>())
                {
                    text.AppendLine(element.Text);
                }

                return text.ToString();
            }
        case ".pdf":
            using (var memory = new MemoryStream())
            {
                await file.CopyToAsync(memory);
                memory.Position = 0;

                using var pdf = PdfDocument.Open(memory);
                var sb = new StringBuilder();

                foreach (Page page in pdf.GetPages())
                {
                    sb.AppendLine(page.Text);
                }

                return sb.ToString();
            }
        default:
            throw new InvalidOperationException("Unsupported file type.");
    }
}

public class CodeBlockExtractor
{
    private static readonly Regex FencedRegex = new(
        @"(?ms)(?<fence>```+|~~~+)[ \t]*(?<lang>[^\r\n]*)\r?\n(?<code>.*?)(?:\r?\n\k<fence>|\k<fence>)",
        RegexOptions.Compiled
    );

    private static readonly Regex InlineRegex = new(
        "`([^`\n]+)`",
        RegexOptions.Compiled
    );

    public List<CodeBlock> Extract(string content, string extension, bool includeInline)
    {
        var blocks = new List<CodeBlock>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var consumedRanges = new List<(int start, int length)>();

        if (IsHtml(extension))
        {
            foreach (var htmlBlock in ExtractFromHtml(content))
            {
                if (seen.Add(htmlBlock.Content))
                {
                    blocks.Add(htmlBlock);
                }
            }
        }

        foreach (Match match in FencedRegex.Matches(content))
        {
            if (!match.Success)
            {
                continue;
            }

            var code = match.Groups["code"].Value.Trim('\r', '\n');
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var langHint = match.Groups["lang"].Value?.Trim();
            var language = DetectLanguage(code, langHint, extension);
            var block = new CodeBlock(code, language, langHint, "fenced");
            if (seen.Add(block.Content))
            {
                blocks.Add(block);
            }
            consumedRanges.Add((match.Index, match.Length));
        }

        var fencedSpans = consumedRanges.OrderBy(r => r.start).ToList();

        if (includeInline)
        {
            foreach (Match match in InlineRegex.Matches(content))
            {
                if (!match.Success)
                {
                    continue;
                }

                if (IsInsideSpan(match.Index, fencedSpans))
                {
                    continue;
                }

                var inline = match.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(inline))
                {
                    continue;
                }

                var language = DetectLanguage(inline, null, extension);
                var block = new CodeBlock(inline, language, null, "inline");
                if (seen.Add(block.Content))
                {
                    blocks.Add(block);
                }
            }
        }

        var indentedBlocks = ExtractIndentedBlocks(content);
        foreach (var block in indentedBlocks)
        {
            if (!blocks.Any(b => string.Equals(b.Content, block, StringComparison.Ordinal)))
            {
                var language = DetectLanguage(block, null, extension);
                if (seen.Add(block))
                {
                    blocks.Add(new CodeBlock(block, language, null, "indented"));
                }
            }
        }

        return blocks
            .OrderByDescending(b => b.Type == "fenced")
            .ThenBy(b => b.Type != "fenced" && b.Type != "inline")
            .ThenBy(b => b.Content.Length)
            .ToList();
    }

    private static List<string> ExtractIndentedBlocks(string content)
    {
        var results = new List<string>();
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var current = new List<string>();

        foreach (var raw in lines)
        {
            var line = raw ?? string.Empty;
            if (line.StartsWith("    ") || line.StartsWith("\t"))
            {
                current.Add(line.TrimEnd());
                continue;
            }

            if (current.Count > 0)
            {
                results.Add(string.Join('\n', current).Trim('\n'));
                current.Clear();
            }
        }

        if (current.Count > 0)
        {
            results.Add(string.Join('\n', current).Trim('\n'));
        }

        return results;
    }

    private static bool IsInsideSpan(int index, List<(int start, int length)> spans)
    {
        foreach (var (start, length) in spans)
        {
            if (index >= start && index < start + length)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHtml(string extension) => extension.Equals(".html", StringComparison.OrdinalIgnoreCase) || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<CodeBlock> ExtractFromHtml(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var nodes = document.DocumentNode.SelectNodes("//pre|//code|//samp|//tt");
        if (nodes == null)
        {
            yield break;
        }

        foreach (var node in nodes)
        {
            var content = node.InnerText?.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var classes = node.GetClasses();
            var langClass = classes.FirstOrDefault(c => c.StartsWith("language-", StringComparison.OrdinalIgnoreCase));
            var language = langClass?.Substring("language-".Length);

            yield return new CodeBlock(content, language, language, "html");
        }
    }

    private static string? DetectLanguage(string code, string? hint, string extension)
    {
        if (!string.IsNullOrWhiteSpace(hint))
        {
            return hint.Trim();
        }

        var normalized = extension.TrimStart('.').ToLowerInvariant();
        switch (normalized)
        {
            case "cs":
            case "csx":
            case "csproj":
                return "csharp";
            case "js":
            case "jsx":
            case "mjs":
                return "javascript";
            case "ts":
            case "tsx":
                return "typescript";
            case "py":
                return "python";
            case "html":
            case "htm":
                return "html";
            case "md":
            case "markdown":
                break;
            default:
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
                break;
        }

        if (code.Contains("using System", StringComparison.Ordinal) || code.Contains("namespace ", StringComparison.Ordinal))
        {
            return "csharp";
        }

        if (code.Contains("function ", StringComparison.Ordinal) || code.Contains("console.log", StringComparison.Ordinal))
        {
            return "javascript";
        }

        if (Regex.IsMatch(code, @"\bdef\s+[a-zA-Z_]", RegexOptions.IgnoreCase))
        {
            return "python";
        }

        if (code.TrimStart().StartsWith("<", StringComparison.Ordinal))
        {
            return "html";
        }

        return null;
    }
}

public record CodeBlock(string Content, string? Language, string? Hint, string Type);
