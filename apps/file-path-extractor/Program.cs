using System.Text.RegularExpressions;

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

app.MapPost("/api/scan", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    if (form.Files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .js, .py, .json, .yaml/.yml, .md/.markdown, or .txt file." });
    }

    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".py", ".json", ".yaml", ".yml", ".md", ".markdown", ".txt"
    };

    var pathRegex = new Regex(
        @"(?<path>(?:[A-Za-z]:[\\/]|~?/|/|\.\./|\.\\/)?(?:[\\w@%+=:,.-]+[\\/\\\\])*[\\w@%+=:,.-]+\.[A-Za-z0-9]{1,10})",
        RegexOptions.Compiled
    );

    var results = new List<object>();
    var totalMatches = 0;

    foreach (var file in form.Files)
    {
        var extension = Path.GetExtension(file.FileName);

        if (!allowed.Contains(extension))
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = Array.Empty<object>(),
                error = "Unsupported file type."
            });
            continue;
        }

        if (file.Length == 0)
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = Array.Empty<object>(),
                error = "File was empty."
            });
            continue;
        }

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();
            var matches = ExtractPaths(content, pathRegex);

            totalMatches += matches.Count;

            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = matches
                    .OrderBy(m => m.Line)
                    .ThenBy(m => m.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(m => new { path = m.Path, line = m.Line, snippet = m.Snippet }),
                error = (string?)null
            });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = Array.Empty<object>(),
                error = ex.Message
            });
        }
    }

    return Results.Ok(new
    {
        filesProcessed = results.Count,
        totalMatches,
        results
    });
});

app.Run();

static List<PathMatch> ExtractPaths(string content, Regex regex)
{
    var matches = new List<PathMatch>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

    for (var i = 0; i < lines.Length; i++)
    {
        var lineNumber = i + 1;
        var line = lines[i];

        foreach (Match match in regex.Matches(line))
        {
            var path = match.Groups["path"].Value;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var cleaned = path.Trim().TrimEnd(';', ',', ')', ']', '}', '\'', '"', '`');
            var key = $"{cleaned}::{lineNumber}";

            if (seen.Add(key))
            {
                matches.Add(new PathMatch(cleaned, lineNumber, line.Trim()));
            }
        }
    }

    return matches;
}

record PathMatch(string Path, int Line, string Snippet);
