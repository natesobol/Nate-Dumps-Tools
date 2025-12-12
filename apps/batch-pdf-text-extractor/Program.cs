using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with one or more PDF files." });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;
    var includeStructure = bool.TryParse(form["structured"], out var structuredFlag) && structuredFlag;
    var preferCleanLayout = !bool.TryParse(form["compact"], out var compactFlag) || !compactFlag;

    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one PDF to extract text." });
    }

    var results = new List<object>();
    foreach (var file in files)
    {
        if (file.Length == 0)
        {
            results.Add(new
            {
                file = file.FileName,
                error = "File was empty."
            });
            continue;
        }

        try
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);

            var extraction = ExtractFromPdf(buffer, includeStructure, preferCleanLayout);
            results.Add(extraction with { file = file.FileName });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                file = file.FileName,
                error = $"Unexpected failure reading PDF: {ex.Message}"
            });
        }
    }

    return Results.Ok(new { files = results });
});

app.Run();

static ExtractionResult ExtractFromPdf(MemoryStream pdfData, bool includeStructure, bool preferCleanLayout)
{
    var attempts = new List<string>();
    var warnings = new List<string>();
    var pageTexts = new List<PageText>();

    var strategies = new List<StrategyPlan>
    {
        new("content-order", preferCleanLayout, useRawLetters: false),
        new("word-join", preferCleanLayout: false, useRawLetters: false),
        new("letters", preferCleanLayout: false, useRawLetters: true)
    };

    foreach (var strategy in strategies)
    {
        pdfData.Position = 0;
        try
        {
            using var document = PdfDocument.Open(pdfData, new ParsingOptions
            {
                ClipPaths = true,
                Letters = true,
                MergeAdjacentCharacters = strategy.PreferCleanLayout
            });

            var extracted = TryExtract(document, strategy.PreferCleanLayout, strategy.UseRawLetters, strategy.Label);
            attempts.Add(extracted.Strategy);

            if (!extracted.Success)
            {
                if (!string.IsNullOrWhiteSpace(extracted.Warning))
                {
                    warnings.Add(extracted.Warning);
                }

                continue;
            }

            pageTexts = extracted.Pages;
            if (!string.IsNullOrWhiteSpace(extracted.Warning))
            {
                warnings.Add(extracted.Warning);
            }

            break;
        }
        catch (Exception ex)
        {
            attempts.Add($"{strategy.Label} (failed: {ex.Message})");
            warnings.Add($"{strategy.Label} failed: {ex.Message}");
        }
    }

    if (pageTexts.Count == 0)
    {
        return new ExtractionResult
        {
            file = string.Empty,
            pageCount = 0,
            attempts,
            warnings,
            error = "Unable to extract any text from this PDF. It may be image-only or encrypted.",
            combinedText = string.Empty,
            pages = includeStructure ? new List<PageText>() : null
        };
    }

    var combinedBuilder = new StringBuilder();
    foreach (var page in pageTexts)
    {
        combinedBuilder.AppendLine(page.Header);
        combinedBuilder.AppendLine(page.Text);
        combinedBuilder.AppendLine();
    }

    return new ExtractionResult
    {
        file = string.Empty,
        pageCount = pageTexts.Count,
        attempts,
        warnings,
        combinedText = combinedBuilder.ToString().TrimEnd(),
        pages = includeStructure ? pageTexts : null
    };
}

static (bool Success, List<PageText> Pages, string Strategy, string? Warning) TryExtract(
    PdfDocument document,
    bool preferCleanLayout,
    bool useRawLetters,
    string label)
{
    var pages = new List<PageText>();
    var warning = default(string?);

    foreach (var page in document.GetPages())
    {
        string text;
        if (useRawLetters)
        {
            text = string.Concat(page.Letters.Select(letter => letter.Value));
            warning = "Used letter concatenation fallback. Text layout may be jumbled.";
        }
        else
        {
            text = preferCleanLayout
                ? ContentOrderTextExtractor.GetText(page)
                : string.Join(" ", page.GetWords().Select(w => w.Text));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return (false, new List<PageText>(), useRawLetters ? "letters" : preferCleanLayout ? "content-order" : "word-join", "Page contained no extractable text.");
        }

        pages.Add(new PageText
        {
            Number = page.Number,
            Header = $"Page {page.Number}",
            Text = text.TrimEnd()
        });
    }

    var strategyName = string.IsNullOrWhiteSpace(label)
        ? useRawLetters ? "letters" : preferCleanLayout ? "content-order" : "word-join"
        : label;

    return (true, pages, strategyName, warning);
}

record StrategyPlan(string Label, bool PreferCleanLayout, bool UseRawLetters);

record ExtractionResult
{
    public string file { get; init; } = string.Empty;
    public int pageCount { get; init; }
    public List<string> attempts { get; init; } = new();
    public List<string> warnings { get; init; } = new();
    public string? error { get; init; }
    public string combinedText { get; init; } = string.Empty;
    public List<PageText>? pages { get; init; }
}

record PageText
{
    public int Number { get; init; }
    public string Header { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}
