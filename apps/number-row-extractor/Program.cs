using System.Data;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

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

app.MapPost("/api/filter", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with a CSV, TSV, or Excel upload." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "Please upload a non-empty .csv, .tsv, or .xlsx file." });
    }

    var matchType = form["matchType"].ToString().ToLowerInvariant();
    var containsText = form["containsText"].ToString();
    var targetValueText = form["targetValue"].ToString();
    var minValueText = form["minValue"].ToString();
    var maxValueText = form["maxValue"].ToString();

    var (filter, error) = BuildFilter(matchType, containsText, targetValueText, minValueText, maxValueText);
    if (error is not null)
    {
        return Results.BadRequest(new { error });
    }

    List<string> requestedColumns = form["columns"].ToString()
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();

    TabularData dataset;
    try
    {
        dataset = await ReadTabularFileAsync(file);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    if (dataset.Headers.Count == 0)
    {
        return Results.BadRequest(new { error = "No headers were detected in the upload." });
    }

    var columnsToCheck = requestedColumns.Count == 0
        ? dataset.Headers
        : dataset.Headers
            .Where(h => requestedColumns.Any(rc => rc.Equals(h, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    if (columnsToCheck.Count == 0)
    {
        return Results.BadRequest(new { error = "None of the requested columns were found in the file header." });
    }

    var matchedRows = dataset.Rows
        .Where(row => columnsToCheck.Any(col => row.TryGetValue(col, out var value) && MatchesFilter(value, filter)))
        .ToList();

    var orderedRows = matchedRows
        .Select(row => dataset.Headers.ToDictionary(
            header => header,
            header => row.TryGetValue(header, out var value) ? value : string.Empty,
            StringComparer.OrdinalIgnoreCase))
        .ToList();

    var csv = BuildCsv(dataset.Headers, orderedRows);

    return Results.Ok(new
    {
        file = file.FileName,
        headers = dataset.Headers,
        inspectedColumns = columnsToCheck,
        totalRows = dataset.Rows.Count,
        matchedCount = matchedRows.Count,
        rows = orderedRows,
        csv
    });
});

app.Run();

static (NumberFilter filter, string? error) BuildFilter(string matchType, string containsText, string targetValueText, string minValueText, string maxValueText)
{
    matchType = string.IsNullOrWhiteSpace(matchType) ? "contains" : matchType;

    switch (matchType)
    {
        case "contains":
            if (string.IsNullOrWhiteSpace(containsText))
            {
                return (NumberFilter.Empty, "Provide the number or digits to search for.");
            }

            return (NumberFilter.ForContains(containsText.Trim()), null);
        case "equals":
        case "greater":
        case "less":
            if (!TryParseDouble(targetValueText, out var target))
            {
                return (NumberFilter.Empty, "Enter a valid number for the target value.");
            }

            return (NumberFilter.ForTarget(matchType, target), null);
        case "between":
            if (!TryParseDouble(minValueText, out var min) || !TryParseDouble(maxValueText, out var max))
            {
                return (NumberFilter.Empty, "Enter valid numbers for the range.");
            }

            if (min > max)
            {
                (min, max) = (max, min);
            }

            return (NumberFilter.ForRange(min, max), null);
        default:
            return (NumberFilter.Empty, "Unsupported match type. Choose contains, equals, greater, less, or between.");
    }
}

static bool TryParseDouble(string value, out double result)
{
    return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result)
        || double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
}

static bool MatchesFilter(string? value, NumberFilter filter)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    var normalized = value.Trim();

    if (filter.MatchType == "contains")
    {
        return normalized.IndexOf(filter.ContainsText, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    if (!double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var numeric))
    {
        return false;
    }

    return filter.MatchType switch
    {
        "equals" => Math.Abs(numeric - filter.TargetValue.GetValueOrDefault()) < 1e-9,
        "greater" => filter.TargetValue.HasValue && numeric > filter.TargetValue.Value,
        "less" => filter.TargetValue.HasValue && numeric < filter.TargetValue.Value,
        "between" => filter.RangeMin.HasValue && filter.RangeMax.HasValue && numeric >= filter.RangeMin && numeric <= filter.RangeMax,
        _ => false
    };
}

static async Task<TabularData> ReadTabularFileAsync(IFormFile file)
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    return extension switch
    {
        ".csv" => await ReadDelimitedAsync(file, ","),
        ".tsv" => await ReadDelimitedAsync(file, "\t"),
        ".xlsx" or ".xls" => await ReadExcelAsync(file),
        _ => throw new InvalidOperationException("Unsupported file type. Upload .csv, .tsv, or .xlsx files.")
    };
}

static async Task<TabularData> ReadDelimitedAsync(IFormFile file, string delimiter)
{
    using var stream = file.OpenReadStream();
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        TrimOptions = TrimOptions.Trim,
        BadDataFound = null,
        MissingFieldFound = null,
        Delimiter = delimiter
    };

    using var csv = new CsvReader(reader, config);

    if (!await csv.ReadAsync() || !csv.ReadHeader())
    {
        throw new InvalidOperationException("Unable to read a header row from the file.");
    }

    var headers = (csv.HeaderRecord ?? Array.Empty<string>())
        .Where(h => !string.IsNullOrWhiteSpace(h))
        .Select(h => h.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    var rows = new List<Dictionary<string, string>>(capacity: 64);

    while (await csv.ReadAsync())
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            var value = csv.GetField(header) ?? string.Empty;
            row[header] = value;
        }

        rows.Add(row);
    }

    return new TabularData(headers, rows);
}

static Task<TabularData> ReadExcelAsync(IFormFile file)
{
    using var stream = file.OpenReadStream();
    using var reader = ExcelReaderFactory.CreateReader(stream);

    var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
    {
        ConfigureDataTable = _ => new ExcelDataTableConfiguration
        {
            UseHeaderRow = true
        }
    });

    if (dataSet.Tables.Count == 0)
    {
        throw new InvalidOperationException("No worksheets were found in the Excel file.");
    }

    var table = dataSet.Tables[0];

    var headers = table.Columns
        .Cast<DataColumn>()
        .Select(c => c.ColumnName)
        .Where(h => !string.IsNullOrWhiteSpace(h))
        .Select(h => h.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    var rows = new List<Dictionary<string, string>>(table.Rows.Count);

    foreach (DataRow dataRow in table.Rows)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            var value = dataRow[header]?.ToString() ?? string.Empty;
            row[header] = value;
        }

        rows.Add(row);
    }

    return Task.FromResult(new TabularData(headers, rows));
}

static string BuildCsv(IEnumerable<string> headers, IEnumerable<IDictionary<string, string>> rows)
{
    var headerList = headers.ToList();
    var sb = new StringBuilder();

    sb.AppendLine(string.Join(',', headerList.Select(EscapeCsv)));

    foreach (var row in rows)
    {
        var values = headerList.Select(h => row.TryGetValue(h, out var value) ? value : string.Empty);
        sb.AppendLine(string.Join(',', values.Select(EscapeCsv)));
    }

    return sb.ToString();
}

static string EscapeCsv(string value)
{
    if (value.Contains('"'))
    {
        value = value.Replace("\"", "\"\"");
    }

    if (value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
    {
        return $"\"{value}\"";
    }

    return value;
}

record TabularData(List<string> Headers, List<Dictionary<string, string>> Rows);

record NumberFilter(string MatchType, double? TargetValue, double? RangeMin, double? RangeMax, string ContainsText)
{
    public static NumberFilter Empty => new("contains", null, null, null, string.Empty);

    public static NumberFilter ForContains(string containsText) => new("contains", null, null, null, containsText);

    public static NumberFilter ForTarget(string matchType, double target) => new(matchType, target, null, null, target.ToString(CultureInfo.InvariantCulture));

    public static NumberFilter ForRange(double min, double max) => new("between", null, min, max, string.Empty);
}
