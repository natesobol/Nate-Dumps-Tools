using System.Diagnostics;
using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with a presentation file." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    var mode = (form["mode"].ToString() ?? "outline").ToLowerInvariant();
    var export = (form["export"].ToString() ?? "json").ToLowerInvariant();

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "Upload a presentation file to extract headings." });
    }

    var allowed = new[] { ".pptx", ".key", ".pdf", ".odp" };
    var extension = Path.GetExtension(file.FileName);
    if (string.IsNullOrWhiteSpace(extension) || !allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { error = "Only .pptx, .key, .pdf, and .odp files are supported." });
    }

    var tempRoot = Path.Combine(Path.GetTempPath(), $"heading-extractor-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);

    try
    {
        var inputPath = Path.Combine(tempRoot, Path.GetFileName(file.FileName));
        await using (var stream = File.Create(inputPath))
        {
            await file.CopyToAsync(stream);
        }

        var outlines = await ExtractOutlinesAsync(inputPath, extension);
        if (outlines.Count == 0)
        {
            return Results.BadRequest(new { error = "No headings or subheadings were detected in the uploaded file." });
        }

        return export switch
        {
            "txt" => Results.File(Encoding.UTF8.GetBytes(BuildText(outlines, mode)), "text/plain", $"{Path.GetFileNameWithoutExtension(file.FileName)}-headings.txt"),
            "csv" => Results.File(Encoding.UTF8.GetBytes(BuildCsv(outlines)), "text/csv", $"{Path.GetFileNameWithoutExtension(file.FileName)}-headings.csv"),
            "docx" => Results.File(BuildDocx(outlines, mode), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{Path.GetFileNameWithoutExtension(file.FileName)}-headings.docx"),
            _ => Results.Ok(new { mode, export = "json", slides = outlines })
        };
    }
    catch (Win32Exception)
    {
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    finally
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
            // ignore cleanup issues
        }
    }
});

app.Run();

static async Task<List<SlideOutline>> ExtractOutlinesAsync(string path, string extension)
{
    if (extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase))
    {
        return ExtractFromPptx(path);
    }

    return await ExtractWithLibreOfficeAsync(path);
}

static List<SlideOutline> ExtractFromPptx(string path)
{
    using var presentation = PresentationDocument.Open(path, false);
    var slideIds = presentation.PresentationPart?.Presentation.SlideIdList?.Elements<SlideId>() ?? Enumerable.Empty<SlideId>();
    var outlines = new List<SlideOutline>();
    var index = 1;

    foreach (var slideId in slideIds)
    {
        var slidePart = presentation.PresentationPart?.GetPartById(slideId.RelationshipId!) as SlidePart;
        if (slidePart is null)
        {
            continue;
        }

        var title = GetTitle(slidePart);
        var subheadings = GetSubheadings(slidePart);

        if (string.IsNullOrWhiteSpace(title) && subheadings.Count == 0)
        {
            index++;
            continue;
        }

        outlines.Add(new SlideOutline(index, title ?? $"Slide {index}", subheadings));
        index++;
    }

    return outlines;
}

static string? GetTitle(SlidePart slidePart)
{
    foreach (var shape in slidePart.Slide.Descendants<DocumentFormat.OpenXml.Presentation.Shape>())
    {
        var placeholder = shape.NonVisualShapeProperties?
            .ApplicationNonVisualDrawingProperties?
            .GetFirstChild<PlaceholderShape>();

        if (placeholder is null || placeholder.Type is null || placeholder.Type.HasValue &&
            (placeholder.Type == PlaceholderValues.Title || placeholder.Type == PlaceholderValues.CenteredTitle))
        {
            var text = GetShapeText(shape);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }
    }

    return slidePart.Slide.Descendants<DocumentFormat.OpenXml.Presentation.Shape>()
        .Select(GetShapeText)
        .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))?
        .Trim();
}

static List<string> GetSubheadings(SlidePart slidePart)
{
    var results = new List<string>();

    foreach (var shape in slidePart.Slide.Descendants<DocumentFormat.OpenXml.Presentation.Shape>())
    {
        var placeholder = shape.NonVisualShapeProperties?
            .ApplicationNonVisualDrawingProperties?
            .GetFirstChild<PlaceholderShape>();

        if (placeholder?.Type is not null && placeholder.Type != PlaceholderValues.Body && placeholder.Type != PlaceholderValues.SubTitle)
        {
            continue;
        }

        foreach (var paragraph in shape.TextBody?.Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>() ?? Enumerable.Empty<DocumentFormat.OpenXml.Drawing.Paragraph>())
        {
            var text = string.Concat(paragraph.Descendants<DocumentFormat.OpenXml.Drawing.Text>().Select(t => t.Text)).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var outlineLevel = paragraph.ParagraphProperties?.OutlineLevel?.Value ?? 0;
            if (outlineLevel <= 1)
            {
                results.Add(text);
            }
        }
    }

    return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

static string GetShapeText(DocumentFormat.OpenXml.Presentation.Shape shape)
{
    var text = shape.TextBody?.InnerText ?? string.Empty;
    return text.Replace("\r", " ").Replace("\n", " ").Trim();
}

static async Task<List<SlideOutline>> ExtractWithLibreOfficeAsync(string path)
{
    var outputDir = Path.Combine(Path.GetTempPath(), $"html-headings-{Guid.NewGuid():N}");
    Directory.CreateDirectory(outputDir);

    try
    {
        await ConvertWithLibreOffice(path, outputDir, "html");
        var htmlFiles = Directory.GetFiles(outputDir, "*.htm*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (htmlFiles.Count == 0)
        {
            return new List<SlideOutline>();
        }

        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var outlines = new List<SlideOutline>();
        var slideNumber = 1;

        foreach (var htmlPath in htmlFiles)
        {
            var content = await File.ReadAllTextAsync(htmlPath);
            var document = await context.OpenAsync(req => req.Content(content));
            var headings = ExtractHeadingsFromHtml(document);

            if (headings.Count == 0)
            {
                slideNumber++;
                continue;
            }

            var title = headings.First();
            var subs = headings.Skip(1).ToList();
            outlines.Add(new SlideOutline(slideNumber, title, subs));
            slideNumber++;
        }

        return outlines;
    }
    finally
    {
        try
        {
            Directory.Delete(outputDir, recursive: true);
        }
        catch
        {
            // ignore cleanup
        }
    }
}

static List<string> ExtractHeadingsFromHtml(IDocument document)
{
    var candidates = new List<string>();
    var headingTags = document.All.Where(e => e.LocalName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6");
    candidates.AddRange(headingTags.Select(h => h.TextContent.Trim()));

    var boldParagraphs = document.All
        .Where(e => e.LocalName == "p" && e.QuerySelector("strong, b") is not null)
        .Select(p => p.TextContent.Trim());

    candidates.AddRange(boldParagraphs);

    var listHeaders = document.QuerySelectorAll("li strong, li b").Select(li => li.TextContent.Trim());
    candidates.AddRange(listHeaders);

    return candidates
        .Where(c => !string.IsNullOrWhiteSpace(c))
        .Select(c => c.Replace("\u00A0", " "))
        .Select(c => c.Replace("\t", " ").Replace("\n", " "))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

static async Task ConvertWithLibreOffice(string inputPath, string outputDir, string target)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "soffice",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    startInfo.ArgumentList.Add("--headless");
    startInfo.ArgumentList.Add("--nologo");
    startInfo.ArgumentList.Add("--nodefault");
    startInfo.ArgumentList.Add("--nofirststartwizard");
    startInfo.ArgumentList.Add("--norestore");
    startInfo.ArgumentList.Add("--convert-to");
    startInfo.ArgumentList.Add(target);
    startInfo.ArgumentList.Add("--outdir");
    startInfo.ArgumentList.Add(outputDir);
    startInfo.ArgumentList.Add(inputPath);

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("LibreOffice failed to start.");
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        var stderr = await process.StandardError.ReadToEndAsync();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        throw new InvalidOperationException($"LibreOffice conversion failed. Exit code {process.ExitCode}. {stderr} {stdout}");
    }
}

static string BuildText(IEnumerable<SlideOutline> outlines, string mode)
{
    var builder = new StringBuilder();

    foreach (var outline in outlines)
    {
        builder.AppendLine($"Slide {outline.SlideNumber}: {outline.Title}");

        if (mode == "summary")
        {
            continue;
        }

        foreach (var sub in outline.Subheadings)
        {
            builder.AppendLine($"  - {sub}");
        }

        builder.AppendLine();
    }

    if (mode == "summary")
    {
        var summary = outlines
            .SelectMany(o => new[] { o.Title }.Concat(o.Subheadings))
            .Where(t => !string.IsNullOrWhiteSpace(t));

        builder.AppendLine("Key points:");
        foreach (var point in summary)
        {
            builder.AppendLine($"- {point}");
        }
    }

    return builder.ToString();
}

static string BuildCsv(IEnumerable<SlideOutline> outlines)
{
    var builder = new StringBuilder();
    builder.AppendLine("Slide,Title,Heading");

    foreach (var outline in outlines)
    {
        if (outline.Subheadings.Count == 0)
        {
            builder.AppendLine($"{outline.SlideNumber},\"{Escape(outline.Title)}\",");
            continue;
        }

        foreach (var sub in outline.Subheadings)
        {
            builder.AppendLine($"{outline.SlideNumber},\"{Escape(outline.Title)}\",\"{Escape(sub)}\"");
        }
    }

    return builder.ToString();
}

static byte[] BuildDocx(IEnumerable<SlideOutline> outlines, string mode)
{
    using var stream = new MemoryStream();
    using var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
    var mainPart = document.AddMainDocumentPart();
    mainPart.Document = new Document();
    AddBulletNumbering(mainPart);
    var body = mainPart.Document.AppendChild(new Body());

    foreach (var outline in outlines)
    {
        body.Append(CreateHeadingParagraph($"Slide {outline.SlideNumber}: {outline.Title}", bold: true));

        if (mode == "summary")
        {
            continue;
        }

        foreach (var sub in outline.Subheadings)
        {
            body.Append(CreateBulletParagraph(sub));
        }
    }

    if (mode == "summary")
    {
        body.Append(CreateHeadingParagraph("Key points", bold: true));
        foreach (var point in outlines.SelectMany(o => new[] { o.Title }.Concat(o.Subheadings)))
        {
            if (string.IsNullOrWhiteSpace(point))
            {
                continue;
            }

            body.Append(CreateBulletParagraph(point));
        }
    }

    document.Close();
    return stream.ToArray();
}

static Paragraph CreateHeadingParagraph(string text, bool bold = false)
{
    var runProperties = new RunProperties();
    if (bold)
    {
        runProperties.Append(new Bold());
    }

    var run = new Run(runProperties, new Text(text));
    var paragraphProperties = new ParagraphProperties(new SpacingBetweenLines { After = "120" });
    return new Paragraph(paragraphProperties, run);
}

static Paragraph CreateBulletParagraph(string text)
{
    var paragraph = new Paragraph();
    var properties = new ParagraphProperties();
    var numberingProperties = new NumberingProperties(
        new NumberingLevelReference { Val = 0 },
        new NumberingId { Val = 1 });
    properties.Append(numberingProperties);
    paragraph.Append(properties);
    paragraph.Append(new Run(new Text(text)));
    return paragraph;
}

static string Escape(string value) => value.Replace("\"", "\"\"");

static void AddBulletNumbering(MainDocumentPart mainPart)
{
    var numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();

    var abstractNum = new AbstractNum(new Level(
        new NumberingFormat { Val = NumberFormatValues.Bullet },
        new LevelText { Val = "•" },
        new LevelJustification { Val = LevelJustificationValues.Left },
        new ParagraphProperties(new Indentation { Left = "720", Hanging = "360" })
    )
    { LevelIndex = 0 })
    { AbstractNumberId = 1 };

    var numberingInstance = new NumberingInstance(new AbstractNumId { Val = 1 })
    { NumberID = 1 };

    numberingPart.Numbering = new Numbering(abstractNum, numberingInstance);
}

record SlideOutline(int SlideNumber, string Title, List<string> Subheadings);
