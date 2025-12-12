using Microsoft.Extensions.FileProviders;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

static void RegisterSharedAssets(WebApplication app, string folderPath, string requestPath)
{
    if (!Directory.Exists(folderPath))
    {
        return;
    }

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(folderPath),
        RequestPath = requestPath
    });
}

app.UseDefaultFiles();
app.UseStaticFiles();

var repositoryRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", ".."));

RegisterSharedAssets(app, Path.Combine(repositoryRoot, "css"), "/css");
RegisterSharedAssets(app, Path.Combine(repositoryRoot, "js"), "/js");
RegisterSharedAssets(app, Path.Combine(repositoryRoot, "public"), "/public");

var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".js", ".py", ".java", ".c", ".cpp", ".html", ".ts"
};

app.MapPost("/api/extract", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with at least one source file." });
    }

    var form = await request.ReadFormAsync();
    var filterValue = form["filter"].ToString()?.ToLowerInvariant();
    var filter = filterValue switch
    {
        "todo" => CommentFilter.TodosFixmes,
        "doc" => CommentFilter.DocComments,
        _ => CommentFilter.All
    };

    var files = form.Files;
    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "No files were uploaded." });
    }

    var allHits = new List<CommentHit>();
    var errors = new List<object>();

    foreach (var file in files)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !supportedExtensions.Contains(extension))
        {
            errors.Add(new { file = file.FileName, message = "Unsupported file type." });
            continue;
        }

        if (file.Length == 0)
        {
            errors.Add(new { file = file.FileName, message = "File was empty." });
            continue;
        }

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        var syntax = CommentSyntax.ForExtension(extension);
        var hits = CommentExtractor.Extract(content, file.FileName, syntax, filter);
        allHits.AddRange(hits);
    }

    if (allHits.Count == 0 && errors.Count > 0)
    {
        return Results.BadRequest(new { error = "No comments found.", details = errors });
    }

    return Results.Ok(new
    {
        filesProcessed = files.Count,
        totalComments = allHits.Count,
        comments = allHits
            .OrderBy(hit => hit.File)
            .ThenBy(hit => hit.LineNumber)
            .ToArray(),
        errors
    });
});

app.Run();

record CommentHit(string File, int LineNumber, string Text, string Category);

enum CommentFilter
{
    All,
    TodosFixmes,
    DocComments
}

record CommentSyntax(
    string? LineComment,
    IReadOnlyList<BlockComment> BlockComments,
    IReadOnlyList<string> DocLinePrefixes)
{
    public static CommentSyntax ForExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".js" or ".ts" or ".java" or ".c" or ".cpp" => new CommentSyntax(
                "//",
                new[]
                {
                    new BlockComment("/*", "*/", isDocStarter: false),
                    new BlockComment("/**", "*/", isDocStarter: true)
                },
                new[] { "///" }
            ),
            ".py" => new CommentSyntax(
                "#",
                new[]
                {
                    new BlockComment("\"\"\"", "\"\"\"", isDocStarter: true),
                    new BlockComment("'''", "'''", isDocStarter: true)
                },
                new[] { "##" }
            ),
            ".html" => new CommentSyntax(
                null,
                new[] { new BlockComment("<!--", "-->", isDocStarter: false) },
                Array.Empty<string>()
            ),
            _ => new CommentSyntax(null, Array.Empty<BlockComment>(), Array.Empty<string>())
        };
    }
}

record BlockComment(string Start, string End, bool IsDocStarter);

static class CommentExtractor
{
    public static IReadOnlyList<CommentHit> Extract(string content, string fileName, CommentSyntax syntax, CommentFilter filter)
    {
        var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var results = new List<CommentHit>();

        string? activeBlockEnd = null;
        bool blockIsDoc = false;
        int lineIndex = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine;
            int cursor = 0;
            while (cursor < line.Length)
            {
                if (activeBlockEnd != null)
                {
                    var endIndex = line.IndexOf(activeBlockEnd, cursor, StringComparison.Ordinal);
                    var commentPortion = endIndex >= 0
                        ? line[cursor..endIndex]
                        : line[cursor..];

                    AddIfIncluded(results, fileName, lineIndex + 1, commentPortion, blockIsDoc, filter);

                    if (endIndex >= 0)
                    {
                        cursor = endIndex + activeBlockEnd.Length;
                        activeBlockEnd = null;
                        blockIsDoc = false;
                        continue;
                    }

                    break;
                }

                var nextLineComment = syntax.LineComment != null
                    ? line.IndexOf(syntax.LineComment, cursor, StringComparison.Ordinal)
                    : -1;

                var nextBlock = FindNextBlockStart(line, cursor, syntax.BlockComments);

                if (nextLineComment == -1 && nextBlock.Index == -1)
                {
                    break;
                }

                if (nextLineComment != -1 && (nextBlock.Index == -1 || nextLineComment < nextBlock.Index))
                {
                    var body = line[(nextLineComment + syntax.LineComment!.Length)..];
                    var isDoc = IsDocLine(body, syntax.DocLinePrefixes);
                    AddIfIncluded(results, fileName, lineIndex + 1, body, isDoc, filter);
                    break;
                }

                if (nextBlock.Index >= 0)
                {
                    var start = nextBlock.Block;
                    cursor = nextBlock.Index + start.Start.Length;
                    activeBlockEnd = start.End;
                    blockIsDoc = start.IsDocStarter;

                    var endIndex = line.IndexOf(start.End, cursor, StringComparison.Ordinal);
                    if (endIndex >= 0)
                    {
                        var body = line[cursor..endIndex];
                        AddIfIncluded(results, fileName, lineIndex + 1, body, blockIsDoc, filter);
                        cursor = endIndex + start.End.Length;
                        activeBlockEnd = null;
                        blockIsDoc = false;
                    }
                    else
                    {
                        var body = line[cursor..];
                        AddIfIncluded(results, fileName, lineIndex + 1, body, blockIsDoc, filter);
                        cursor = line.Length;
                    }
                }
            }

            lineIndex++;
        }

        return results;
    }

    private static (int Index, BlockComment Block) FindNextBlockStart(string line, int startIndex, IReadOnlyList<BlockComment> blocks)
    {
        var bestIndex = -1;
        BlockComment? chosen = null;

        foreach (var block in blocks)
        {
            var index = line.IndexOf(block.Start, startIndex, StringComparison.Ordinal);
            if (index >= 0 && (bestIndex == -1 || index < bestIndex))
            {
                bestIndex = index;
                chosen = block;
            }
        }

        return chosen is null ? (-1, default(BlockComment)) : (bestIndex, chosen.Value);
    }

    private static void AddIfIncluded(List<CommentHit> results, string fileName, int lineNumber, string rawText, bool isDoc, CommentFilter filter)
    {
        var cleaned = CleanComment(rawText);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        var containsTodo = cleaned.Contains("TODO", StringComparison.OrdinalIgnoreCase) ||
                           cleaned.Contains("FIXME", StringComparison.OrdinalIgnoreCase);

        bool include = filter switch
        {
            CommentFilter.All => true,
            CommentFilter.TodosFixmes => containsTodo,
            CommentFilter.DocComments => isDoc,
            _ => true
        };

        if (!include)
        {
            return;
        }

        var category = DetermineCategory(cleaned, isDoc, containsTodo);
        results.Add(new CommentHit(fileName, lineNumber, cleaned, category));
    }

    private static string CleanComment(string raw)
    {
        var text = raw.Replace("\t", " ").Trim();
        text = Regex.Replace(text, "^\\**\\s*", string.Empty);
        text = Regex.Replace(text, "\\s*$", string.Empty);
        return text;
    }

    private static bool IsDocLine(string text, IReadOnlyList<string> docPrefixes)
    {
        var trimmed = text.TrimStart();
        foreach (var prefix in docPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string DetermineCategory(string text, bool isDoc, bool containsTodo)
    {
        if (containsTodo)
        {
            return "todo";
        }

        if (isDoc)
        {
            return "doc";
        }

        return "comment";
    }
}
