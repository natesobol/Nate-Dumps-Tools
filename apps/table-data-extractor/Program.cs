using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
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
        return Results.BadRequest(new { error = "Expected multipart/form-data with a file to extract tables from." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "Upload a .pdf, .docx, .html, or .htm file to extract tables." });
    }

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    var buffer = new MemoryStream();
    await file.CopyToAsync(buffer);
    buffer.Position = 0;

    List<TableResult> tables = extension switch
    {
        ".pdf" => ExtractFromPdf(buffer),
        ".docx" => ExtractFromDocx(buffer),
        ".html" or ".htm" => ExtractFromHtml(buffer),
        _ => []
    };

    foreach (var table in tables)
    {
        NormalizeRows(table.Rows);
    }

    return Results.Ok(new
    {
        file = file.FileName,
        tableCount = tables.Count,
        tables,
        message = tables.Count == 0 ? "No tables were detected in the uploaded file." : null
    });
});

app.MapPost("/api/export", (ExportRequest payload) =>
{
    if (payload.Tables is null || payload.Tables.Count == 0)
    {
        return Results.BadRequest(new { error = "No tables were provided for export." });
    }

    var format = (payload.Format ?? "csv").ToLowerInvariant();
    var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

    if (format == "xlsx")
    {
        var workbookBytes = BuildWorkbook(payload.Tables);
        return Results.File(
            workbookBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"table-export-{timestamp}.xlsx",
            enableRangeProcessing: false
        );
    }

    var csv = BuildCsv(payload.Tables);
    return Results.File(
        Encoding.UTF8.GetBytes(csv),
        "text/csv",
        $"table-export-{timestamp}.csv",
        enableRangeProcessing: false
    );
});

app.Run();

static List<TableResult> ExtractFromPdf(Stream stream)
{
    var results = new List<TableResult>();

    using var document = PdfDocument.Open(stream);

    for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
    {
        var page = document.GetPage(pageNumber);
        var text = ContentOrderTextExtractor.GetText(page);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var segment = new List<string[]>();
        var tableIndex = 1;

        foreach (var rawLine in lines)
        {
            var cells = SplitColumns(rawLine);
            if (cells.Length > 1)
            {
                segment.Add(cells);
                continue;
            }

            if (segment.Count >= 2)
            {
                results.Add(new TableResult
                {
                    Title = $"Page {pageNumber} Table {tableIndex}",
                    Rows = segment.Select(row => row.ToList()).ToList(),
                    Source = "PDF"
                });

                tableIndex++;
            }

            segment.Clear();
        }

        if (segment.Count >= 2)
        {
            results.Add(new TableResult
            {
                Title = $"Page {pageNumber} Table {tableIndex}",
                Rows = segment.Select(row => row.ToList()).ToList(),
                Source = "PDF"
            });
        }
    }

    return results;
}

static List<TableResult> ExtractFromDocx(Stream stream)
{
    var results = new List<TableResult>();
    stream.Position = 0;

    using var document = WordprocessingDocument.Open(stream, false);
    var body = document.MainDocumentPart?.Document.Body;

    if (body is null)
    {
        return results;
    }

    var tables = body.Elements<Table>().ToList();
    var index = 1;

    foreach (var table in tables)
    {
        var rows = new List<List<string>>();

        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>()
                .Select(cell => cell.InnerText?.Trim() ?? string.Empty)
                .ToList();

            if (cells.Count > 0)
            {
                rows.Add(cells);
            }
        }

        if (rows.Count > 0)
        {
            results.Add(new TableResult
            {
                Title = $"Word Table {index}",
                Rows = rows,
                Source = "DOCX"
            });

            index++;
        }
    }

    return results;
}

static List<TableResult> ExtractFromHtml(Stream stream)
{
    var results = new List<TableResult>();
    stream.Position = 0;

    var doc = new HtmlDocument();
    doc.Load(stream);

    var tables = doc.DocumentNode.SelectNodes("//table");
    if (tables is null)
    {
        return results;
    }

    var index = 1;
    foreach (var table in tables)
    {
        var rows = new List<List<string>>();
        var caption = table.SelectSingleNode(".//caption")?.InnerText?.Trim();

        foreach (var row in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var cells = row
                .SelectNodes("./th|./td")
                ?.Select(cell => HtmlEntity.DeEntitize(cell.InnerText).Trim())
                .ToList() ?? [];

            if (cells.Count > 0)
            {
                rows.Add(cells);
            }
        }

        if (rows.Count > 0)
        {
            results.Add(new TableResult
            {
                Title = string.IsNullOrWhiteSpace(caption) ? $"HTML Table {index}" : caption!,
                Rows = rows,
                Source = "HTML"
            });

            index++;
        }
    }

    return results;
}

static string[] SplitColumns(string line)
{
    var cleaned = line.Replace("\r", string.Empty).Trim();

    if (string.IsNullOrWhiteSpace(cleaned))
    {
        return Array.Empty<string>();
    }

    return Regex.Split(cleaned, "\\s{2,}|\t+")
        .Select(cell => cell.Trim())
        .Where(cell => cell.Length > 0)
        .ToArray();
}

static void NormalizeRows(List<List<string>> rows)
{
    if (rows.Count == 0)
    {
        return;
    }

    var maxColumns = rows.Max(r => r.Count);

    foreach (var row in rows)
    {
        while (row.Count < maxColumns)
        {
            row.Add(string.Empty);
        }
    }
}

static byte[] BuildWorkbook(List<TableResult> tables)
{
    using var workbook = new XLWorkbook();
    var index = 1;

    foreach (var table in tables)
    {
        var worksheet = workbook.Worksheets.Add(SanitizeSheetName(table.Title, index));
        var rowNumber = 1;

        foreach (var row in table.Rows)
        {
            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                worksheet.Cell(rowNumber, columnIndex + 1).Value = row[columnIndex];
            }

            rowNumber++;
        }

        index++;
    }

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);
    return stream.ToArray();
}

static string SanitizeSheetName(string title, int fallbackIndex)
{
    var safe = string.IsNullOrWhiteSpace(title) ? $"Table {fallbackIndex}" : title;
    safe = Regex.Replace(safe, "[\\\\/*?\n\r\[\]]", " ").Trim();

    return safe.Length <= 31 ? safe : safe.Substring(0, 31);
}

static string BuildCsv(List<TableResult> tables)
{
    var builder = new StringBuilder();

    for (var i = 0; i < tables.Count; i++)
    {
        var table = tables[i];
        builder.AppendLine($"# Table: {table.Title}");

        foreach (var row in table.Rows)
        {
            var escaped = row.Select(EscapeCsv);
            builder.AppendLine(string.Join(',', escaped));
        }

        if (i < tables.Count - 1)
        {
            builder.AppendLine();
        }
    }

    return builder.ToString();
}

static string EscapeCsv(string value)
{
    var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n');
    var escaped = value.Replace("\"", "\"\"");
    return needsQuotes ? $"\"{escaped}\"" : escaped;
}

public class TableResult
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "Table";

    [JsonPropertyName("rows")]
    public List<List<string>> Rows { get; set; } = [];

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
}

public class ExportRequest
{
    [JsonPropertyName("format")]
    public string? Format { get; set; } = "csv";

    [JsonPropertyName("tables")]
    public List<TableResult> Tables { get; set; } = [];
}
