using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/rename", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with rename options and files." });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;

    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one file to rename." });
    }

    var prefix = form["prefix"].ToString();
    var suffix = form["suffix"].ToString();

    var search = form["search"].ToString();
    var replacement = form["replacement"].ToString();
    var useReplacement = string.Equals(form["useReplacement"], "true", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(search);
    var caseSensitive = string.Equals(form["caseSensitive"], "true", StringComparison.OrdinalIgnoreCase);

    var numberingEnabled = string.Equals(form["numberingEnabled"], "true", StringComparison.OrdinalIgnoreCase);
    var numberingPosition = form["numberingPosition"].ToString();
    if (numberingPosition is not ("prefix" or "suffix"))
    {
        numberingPosition = "suffix";
    }

    var numberingStart = ParseOrDefault(form["numberingStart"], 1);
    var numberingPad = Math.Clamp(ParseOrDefault(form["numberingPad"], 2), 1, 6);

    var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    var results = new List<object>();
    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    await using var zipStream = new MemoryStream();
    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
    {
        var counter = numberingStart;
        foreach (var file in files)
        {
            var originalName = file.FileName;
            var extension = Path.GetExtension(originalName);
            var baseName = Path.GetFileNameWithoutExtension(originalName);

            var transformedName = useReplacement
                ? ReplaceOccurrences(baseName, search, replacement, comparison)
                : baseName;

            if (numberingEnabled)
            {
                var padded = counter.ToString().PadLeft(numberingPad, '0');
                transformedName = numberingPosition == "prefix"
                    ? $"{padded}-{transformedName}"
                    : $"{transformedName}-{padded}";
                counter++;
            }

            transformedName = $"{prefix}{transformedName}{suffix}".Trim();
            if (string.IsNullOrWhiteSpace(transformedName))
            {
                transformedName = "renamed-file";
            }

            var candidate = transformedName + extension;
            var dedupeSuffix = 1;
            while (usedNames.Contains(candidate))
            {
                dedupeSuffix++;
                candidate = $"{transformedName}-{dedupeSuffix}{extension}";
            }

            usedNames.Add(candidate);

            var entry = archive.CreateEntry(candidate, CompressionLevel.Fastest);
            await using (var entryStream = entry.Open())
            await using (var fileStream = file.OpenReadStream())
            {
                await fileStream.CopyToAsync(entryStream);
            }

            results.Add(new
            {
                original = originalName,
                renamed = candidate,
                replaced = useReplacement,
                numberingApplied = numberingEnabled,
                duplicateResolved = dedupeSuffix > 1
            });
        }
    }

    zipStream.Position = 0;
    var zipBase64 = Convert.ToBase64String(zipStream.ToArray());

    return Results.Ok(new
    {
        prefix,
        suffix,
        useReplacement,
        search,
        replacement,
        numbering = numberingEnabled
            ? new
            {
                position = numberingPosition,
                start = numberingStart,
                pad = numberingPad
            }
            : null,
        renamed = results,
        zipBase64
    });
});

app.Run();

static int ParseOrDefault(string input, int fallback)
{
    if (int.TryParse(input, out var value))
    {
        return value;
    }

    return fallback;
}

static string ReplaceOccurrences(string source, string search, string replacement, StringComparison comparison)
{
    if (string.IsNullOrEmpty(search))
    {
        return source;
    }

    var builder = new StringBuilder();
    var index = 0;

    while (index < source.Length)
    {
        var next = source.IndexOf(search, index, comparison);
        if (next < 0)
        {
            builder.Append(source.AsSpan(index));
            break;
        }

        builder.Append(source.AsSpan(index, next - index));
        builder.Append(replacement);
        index = next + search.Length;
    }

    return builder.ToString();
}
