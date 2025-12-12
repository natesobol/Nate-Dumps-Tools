using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/extract", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with one or more files." });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;

    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "No files were uploaded." });
    }

    var enableThreading = bool.TryParse(form["threading"].FirstOrDefault(), out var parsedThreading) && parsedThreading;

    var comments = new List<CommentFinding>();
    var errors = new List<object>();

    foreach (var file in files)
    {
        try
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            List<CommentFinding> extracted = extension switch
            {
                ".docx" => await ExtractDocxAsync(file),
                ".pdf" => await ExtractPdfAsync(file),
                ".rtf" => await ExtractRtfAsync(file),
                _ => throw new InvalidOperationException($"Unsupported file type: {extension}")
            };

            comments.AddRange(extracted);
        }
        catch (Exception ex)
        {
            errors.Add(new { file = file.FileName, message = ex.Message });
        }
    }

    var response = new
    {
        total = comments.Count,
        files = files.Select(f => f.FileName).ToList(),
        threads = enableThreading ? BuildThreads(comments) : null,
        comments,
        errors
    };

    return Results.Ok(response);
});

app.Run();

static async Task<List<CommentFinding>> ExtractDocxAsync(IFormFile file)
{
    using var memory = new MemoryStream();
    await file.CopyToAsync(memory);
    memory.Position = 0;

    var results = new List<CommentFinding>();

    using var document = WordprocessingDocument.Open(memory, false);
    var commentsPart = document.MainDocumentPart?.WordprocessingCommentsPart;
    var commentElements = commentsPart?.Comments?.Elements<Comment>() ?? Enumerable.Empty<Comment>();
    var locations = BuildDocxLocationLookup(document);

    foreach (var comment in commentElements)
    {
        var text = comment.InnerText.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            continue;
        }

        var paraId = comment.GetAttribute("paraId", "http://schemas.microsoft.com/office/word/2012/wordml").Value;
        var parentParaId = comment.GetAttribute("paraIdParent", "http://schemas.microsoft.com/office/word/2012/wordml").Value;

        var threadId = !string.IsNullOrWhiteSpace(parentParaId)
            ? parentParaId
            : !string.IsNullOrWhiteSpace(paraId) ? paraId : comment.Id?.Value;

        var location = comment.Id != null && locations.TryGetValue(comment.Id.Value, out var value)
            ? value
            : "Unknown";

        results.Add(new CommentFinding(
            File: file.FileName,
            CommentText: text,
            Author: string.IsNullOrWhiteSpace(comment.Author) ? comment.Initials : comment.Author,
            Location: location,
            PageLabel: null,
            Source: "DOCX",
            ThreadId: threadId,
            ParentThreadId: string.IsNullOrWhiteSpace(parentParaId) ? null : parentParaId
        ));
    }

    return results;
}

static Dictionary<string, string> BuildDocxLocationLookup(WordprocessingDocument document)
{
    var lookup = new Dictionary<string, string>();
    var paragraphs = document.MainDocumentPart?.Document.Body?.Descendants<Paragraph>().ToList() ?? new();

    for (var i = 0; i < paragraphs.Count; i++)
    {
        var paragraph = paragraphs[i];
        var ids = paragraph.Descendants<CommentRangeStart>().Select(c => c.Id?.Value)
            .Concat(paragraph.Descendants<CommentReference>().Select(c => c.Id?.Value))
            .Where(id => !string.IsNullOrWhiteSpace(id));

        foreach (var id in ids)
        {
            lookup[id!] = $"Paragraph {i + 1}";
        }
    }

    return lookup;
}

static async Task<List<CommentFinding>> ExtractPdfAsync(IFormFile file)
{
    using var memory = new MemoryStream();
    await file.CopyToAsync(memory);
    memory.Position = 0;

    var results = new List<CommentFinding>();

    using var document = PdfDocument.Open(memory);
    foreach (Page page in document.GetPages())
    {
        var annotations = page.ExperimentalAccess.GetAnnotations();
        if (annotations == null)
        {
            continue;
        }

        foreach (Annotation annotation in annotations)
        {
            var text = annotation.Contents;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var author = annotation.Title;
            var pageLabel = $"Page {page.Number}";

            results.Add(new CommentFinding(
                File: file.FileName,
                CommentText: text.Trim(),
                Author: string.IsNullOrWhiteSpace(author) ? null : author,
                Location: pageLabel,
                PageLabel: pageLabel,
                Source: "PDF",
                ThreadId: annotation.InReplyTo ?? annotation.Name,
                ParentThreadId: annotation.InReplyTo
            ));
        }
    }

    return results;
}

static async Task<List<CommentFinding>> ExtractRtfAsync(IFormFile file)
{
    using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
    var content = await reader.ReadToEndAsync();

    var results = new List<CommentFinding>();
    var index = 0;
    var counter = 1;

    while (index < content.Length)
    {
        var start = content.IndexOf("{\\*\\comment", index, StringComparison.OrdinalIgnoreCase);
        if (start == -1)
        {
            break;
        }

        var block = ExtractRtfGroup(content, start);
        if (string.IsNullOrEmpty(block))
        {
            break;
        }

        var cleaned = CleanRtf(block);
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            results.Add(new CommentFinding(
                File: file.FileName,
                CommentText: cleaned,
                Author: null,
                Location: $"Approximate comment {counter}",
                PageLabel: null,
                Source: "RTF",
                ThreadId: null,
                ParentThreadId: null
            ));
        }

        index = start + block.Length;
        counter++;
    }

    return results;
}

static string? ExtractRtfGroup(string content, int startIndex)
{
    var depth = 0;
    for (var i = startIndex; i < content.Length; i++)
    {
        if (content[i] == '{') depth++;
        else if (content[i] == '}')
        {
            depth--;
            if (depth == 0)
            {
                return content.Substring(startIndex, i - startIndex + 1);
            }
        }
    }

    return null;
}

static string CleanRtf(string rtf)
{
    var withoutHeader = rtf.Replace("{\\*\\comment", string.Empty, StringComparison.OrdinalIgnoreCase);
    withoutHeader = withoutHeader.Trim('{', '}', ' ');

    var decodedHex = Regex.Replace(withoutHeader, @"\\'([0-9a-fA-F]{2})", match =>
    {
        var hex = match.Groups[1].Value;
        return ((char)Convert.ToByte(hex, 16)).ToString();
    });

    var withoutControls = Regex.Replace(decodedHex, @"\\[A-Za-z]+-?\d* ?", " ");
    var plain = Regex.Replace(withoutControls, "[{}]", " ");
    plain = Regex.Replace(plain, "\\\\", "\\");
    plain = Regex.Replace(plain, "\s+", " ");

    return plain.Trim();
}

static List<CommentThread> BuildThreads(IEnumerable<CommentFinding> comments)
{
    return comments
        .GroupBy(c => c.ThreadId ?? $"{c.File}:{c.Location}")
        .Select(group => new CommentThread(group.Key, group.ToList()))
        .ToList();
}

public record CommentFinding(
    string File,
    string CommentText,
    string? Author,
    string Location,
    string? PageLabel,
    string Source,
    string? ThreadId,
    string? ParentThreadId);

public record CommentThread(string ThreadId, List<CommentFinding> Comments);
