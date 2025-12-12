using System.Drawing;
using System.Text;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 50; // 50 MB
});

builder.Services.AddResponseCompression();

var app = builder.Build();

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract-highlighted-rows", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with an Excel file." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "Upload a non-empty .xlsx or .xlsm file." });
    }

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (extension is not ".xlsx" and not ".xlsm")
    {
        return Results.BadRequest(new { error = "Only .xlsx and .xlsm files are supported." });
    }

    var filterColor = NormalizeColor(form["filterColor"].FirstOrDefault());
    var exportFormatting = form["exportFormatting"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    var outputFormat = (form["outputFormat"].FirstOrDefault() ?? "xlsx").ToLowerInvariant();

    await using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Position = 0;

    try
    {
        using var package = new ExcelPackage(stream);
        var rows = ExtractRows(package, filterColor);

        if (rows.Count == 0)
        {
            return Results.BadRequest(new { error = "No highlighted or color-coded rows matched your filters." });
        }

        return outputFormat switch
        {
            "csv" => CreateCsvResult(rows),
            _ => CreateWorkbookResult(rows, exportFormatting)
        };
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Failed to process file: {ex.Message}" });
    }
});

app.Run();

static string? NormalizeColor(string? color)
{
    if (string.IsNullOrWhiteSpace(color))
    {
        return null;
    }

    color = color.Trim();
    if (color.StartsWith("#"))
    {
        color = color[1..];
    }

    if (color.Length == 3)
    {
        color = string.Concat(color.Select(c => new string(c, 2)));
    }

    if (color.Length == 6)
    {
        return $"#{color.ToUpperInvariant()}";
    }

    return null;
}

static List<ExtractedRow> ExtractRows(ExcelPackage package, string? filterColor)
{
    var extracted = new List<ExtractedRow>();

    foreach (var worksheet in package.Workbook.Worksheets)
    {
        if (worksheet.Dimension is null)
        {
            continue;
        }

        var conditionalColors = BuildConditionalColorMap(worksheet);

        for (var row = worksheet.Dimension.Start.Row; row <= worksheet.Dimension.End.Row; row++)
        {
            var fillColors = new string?[worksheet.Dimension.End.Column];
            var matches = false;

            for (var col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var cell = worksheet.Cells[row, col];
                var color = GetHex(cell.Style.Fill.BackgroundColor);

                if (conditionalColors.TryGetValue((row, col), out var ruleColor) && string.IsNullOrEmpty(color))
                {
                    color = ruleColor;
                }

                if (!string.IsNullOrEmpty(color))
                {
                    fillColors[col - 1] = color;
                    if (filterColor is null || NormalizeColor(color) == filterColor)
                    {
                        matches = true;
                    }
                }
            }

            if (!matches)
            {
                continue;
            }

            var values = new string[worksheet.Dimension.End.Column];
            for (var col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                values[col - 1] = worksheet.Cells[row, col].Text;
            }

            extracted.Add(new ExtractedRow
            {
                SheetName = worksheet.Name,
                SourceRow = row,
                Values = values,
                FillColors = fillColors
            });
        }
    }

    return extracted;
}

static Dictionary<(int Row, int Col), string> BuildConditionalColorMap(ExcelWorksheet worksheet)
{
    var colors = new Dictionary<(int Row, int Col), string>();

    foreach (var rule in worksheet.ConditionalFormatting)
    {
        var hex = GetHex(rule.Style.Fill.BackgroundColor);
        if (hex is null)
        {
            continue;
        }

        foreach (var address in rule.Address.Addresses)
        {
            foreach (var cell in worksheet.Cells[address.Address])
            {
                colors[(cell.Start.Row, cell.Start.Column)] = hex;
            }
        }
    }

    return colors;
}

static string? GetHex(ExcelColor color)
{
    if (!string.IsNullOrWhiteSpace(color.Rgb))
    {
        var rgb = color.Rgb.Length == 8 ? color.Rgb[2..] : color.Rgb;
        if (rgb.Length == 6)
        {
            return $"#{rgb.ToUpperInvariant()}";
        }
    }

    return null;
}

static IResult CreateWorkbookResult(List<ExtractedRow> rows, bool exportFormatting)
{
    using var package = new ExcelPackage();
    var grouped = rows.GroupBy(r => r.SheetName);

    foreach (var group in grouped)
    {
        var sheet = package.Workbook.Worksheets.Add(group.Key);
        var targetRow = 1;
        var maxColumns = group.Max(r => r.Values.Length);

        foreach (var row in group)
        {
            for (var col = 1; col <= maxColumns; col++)
            {
                var value = col - 1 < row.Values.Length ? row.Values[col - 1] : string.Empty;
                var outputCell = sheet.Cells[targetRow, col];
                outputCell.Value = value;

                if (exportFormatting && col - 1 < row.FillColors.Length && row.FillColors[col - 1] is string hex)
                {
                    ApplyFill(outputCell, hex);
                }
            }

            targetRow++;
        }

        sheet.Cells[sheet.Dimension!.Address].AutoFitColumns();
    }

    var fileBytes = package.GetAsByteArray();
    return Results.File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "highlighted-rows.xlsx");
}

static void ApplyFill(ExcelRange cell, string hex)
{
    try
    {
        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
        cell.Style.Fill.BackgroundColor.SetColor(ColorTranslator.FromHtml(hex));
    }
    catch
    {
        // ignore color application failures
    }
}

static IResult CreateCsvResult(List<ExtractedRow> rows)
{
    var maxColumns = rows.Max(r => r.Values.Length);
    var builder = new StringBuilder();

    var headers = new List<string> { "Sheet", "Row" };
    headers.AddRange(Enumerable.Range(1, maxColumns).Select(i => $"Column{i}"));
    builder.AppendLine(string.Join(',', headers));

    foreach (var row in rows)
    {
        var values = new List<string>
        {
            Quote(row.SheetName),
            row.SourceRow.ToString()
        };

        for (var i = 0; i < maxColumns; i++)
        {
            var value = i < row.Values.Length ? row.Values[i] : string.Empty;
            values.Add(Quote(value));
        }

        builder.AppendLine(string.Join(',', values));
    }

    var bytes = Encoding.UTF8.GetBytes(builder.ToString());
    return Results.File(bytes, "text/csv", "highlighted-rows.csv");
}

static string Quote(string? value)
{
    value ??= string.Empty;
    if (value.Contains('"') || value.Contains(','))
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    return value;
}

internal sealed class ExtractedRow
{
    public required string SheetName { get; init; }
    public required int SourceRow { get; init; }
    public required string[] Values { get; init; }
    public required string?[] FillColors { get; init; }
}
