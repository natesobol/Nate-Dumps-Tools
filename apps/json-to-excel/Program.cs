using ClosedXML.Excel;
using System.Text.Json;
using System.Text.Json.Nodes;

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

app.MapPost("/api/json-to-excel", async Task<IResult> (HttpRequest request) =>
{
    var (node, errorResult) = await ParseJsonPayloadAsync(request);
    if (errorResult is not null)
    {
        return errorResult;
    }

    if (node is null)
    {
        return Results.BadRequest(new { error = "No JSON payload was provided." });
    }

    using var workbook = BuildWorkbook(node);
    using var stream = new MemoryStream();
    workbook.SaveAs(stream);
    stream.Position = 0;

    var fileName = $"json-export-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";

    return Results.File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName,
        enableRangeProcessing: false
    );
});

app.Run();

static async Task<(JsonNode? node, IResult? errorResult)> ParseJsonPayloadAsync(HttpRequest request)
{
    if (request.HasFormContentType)
    {
        var form = await request.ReadFormAsync();
        var jsonText = form["jsonText"].FirstOrDefault()?.Trim();
        var file = form.Files.GetFile("file");

        if (!string.IsNullOrWhiteSpace(jsonText) && file is not null && file.Length > 0)
        {
            return (null, Results.BadRequest(new { error = "Choose either pasted JSON or a file upload, not both." }));
        }

        if (!string.IsNullOrWhiteSpace(jsonText))
        {
            return ParseJsonString(jsonText);
        }

        if (file is not null && file.Length > 0)
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            return ParseJsonString(content, file.FileName);
        }

        return (null, Results.BadRequest(new { error = "Provide JSON text or upload a JSON file to convert." }));
    }

    using var bodyReader = new StreamReader(request.Body);
    var body = await bodyReader.ReadToEndAsync();
    return ParseJsonString(body);
}

static (JsonNode? node, IResult? errorResult) ParseJsonString(string content, string? source = null)
{
    if (string.IsNullOrWhiteSpace(content))
    {
        return (null, Results.BadRequest(new { error = "JSON content was empty." }));
    }

    try
    {
        var node = JsonNode.Parse(content);
        if (node is null)
        {
            return (null, Results.BadRequest(new { error = "Unable to parse the provided JSON." }));
        }

        return (node, null);
    }
    catch (JsonException jsonEx)
    {
        return (null, Results.BadRequest(new
        {
            error = "Invalid JSON payload.",
            detail = jsonEx.Message,
            source
        }));
    }
}

static XLWorkbook BuildWorkbook(JsonNode node)
{
    var workbook = new XLWorkbook();
    var arraySheets = new Dictionary<string, ArraySheetData>(StringComparer.OrdinalIgnoreCase);

    if (node is JsonArray rootArray)
    {
        var rows = new List<Dictionary<string, string>>();
        for (var index = 0; index < rootArray.Count; index++)
        {
            var item = rootArray[index];
            rows.Add(FlattenForRow(item, $"root[{index}]", arraySheets));
        }

        WriteWorksheet(workbook, "Data", rows);
    }
    else if (node is JsonObject rootObject)
    {
        var row = FlattenForRow(rootObject, "root", arraySheets);
        WriteWorksheet(workbook, "Data", new List<Dictionary<string, string>> { row });
    }
    else
    {
        WriteWorksheet(
            workbook,
            "Data",
            new List<Dictionary<string, string>>
            {
                new() { { "value", FormatValue(node) } }
            }
        );
    }

    foreach (var sheet in arraySheets.Values)
    {
        WriteWorksheet(workbook, sheet.Name, sheet.Rows);
    }

    return workbook;
}

static Dictionary<string, string> FlattenForRow(JsonNode? node, string path, Dictionary<string, ArraySheetData> arraySheets)
{
    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    switch (node)
    {
        case JsonObject obj:
            foreach (var property in obj)
            {
                var keyPath = string.IsNullOrWhiteSpace(path) ? property.Key : $"{path}.{property.Key}";
                switch (property.Value)
                {
                    case JsonObject nestedObject:
                        var nested = FlattenForRow(nestedObject, keyPath, arraySheets);
                        foreach (var entry in nested)
                        {
                            row[$"{property.Key}.{entry.Key}"] = entry.Value;
                        }
                        break;
                    case JsonArray nestedArray:
                        var arraySheet = GetOrCreateArraySheet(arraySheets, keyPath);
                        PopulateArraySheet(arraySheet, nestedArray, keyPath, arraySheets);
                        row[property.Key] = $"See '{arraySheet.Name}' ({nestedArray.Count} item(s))";
                        break;
                    default:
                        row[property.Key] = FormatValue(property.Value);
                        break;
                }
            }
            break;
        case JsonArray array:
            var arrayData = GetOrCreateArraySheet(arraySheets, path);
            PopulateArraySheet(arrayData, array, path, arraySheets);
            row[path] = $"See '{arrayData.Name}' ({array.Count} item(s))";
            break;
        default:
            row[path] = FormatValue(node);
            break;
    }

    return row;
}

static void PopulateArraySheet(ArraySheetData sheet, JsonArray array, string path, Dictionary<string, ArraySheetData> arraySheets)
{
    for (var index = 0; index < array.Count; index++)
    {
        var item = array[index];
        var baseRow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "index", index.ToString() }
        };

        switch (item)
        {
            case JsonObject obj:
                var flattened = FlattenForRow(obj, $"{path}[{index}]", arraySheets);
                foreach (var entry in flattened)
                {
                    baseRow[entry.Key] = entry.Value;
                }
                break;
            case JsonArray nestedArray:
                var nestedSheet = GetOrCreateArraySheet(arraySheets, $"{path}[{index}]");
                PopulateArraySheet(nestedSheet, nestedArray, $"{path}[{index}]", arraySheets);
                baseRow[$"{path}[{index}]"] = $"See '{nestedSheet.Name}' ({nestedArray.Count} item(s))";
                break;
            default:
                baseRow["value"] = FormatValue(item);
                break;
        }

        sheet.Rows.Add(baseRow);
    }
}

static string FormatValue(JsonNode? node)
{
    if (node is null)
    {
        return string.Empty;
    }

    if (node is JsonValue value && value.TryGetValue<JsonElement>(out var element) && element.ValueKind == JsonValueKind.Null)
    {
        return string.Empty;
    }

    return node switch
    {
        JsonValue value when value.TryGetValue(out string? str) => str,
        JsonValue value when value.TryGetValue(out bool boolean) => boolean.ToString(),
        JsonValue value when value.TryGetValue(out double number) => number.ToString(),
        _ => node.ToJsonString()
    };
}

static ArraySheetData GetOrCreateArraySheet(Dictionary<string, ArraySheetData> arraySheets, string path)
{
    if (arraySheets.TryGetValue(path, out var sheet))
    {
        return sheet;
    }

    var usedNames = arraySheets.Values.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var sheetName = CreateSheetName(path, usedNames);

    sheet = new ArraySheetData(sheetName);
    arraySheets[path] = sheet;
    return sheet;
}

static string CreateSheetName(string path, HashSet<string> usedNames)
{
    var tokens = path
        .Replace("[", ".")
        .Replace("]", string.Empty)
        .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var baseName = tokens.Length == 0 ? "ArrayData" : string.Join("_", tokens);
    baseName = baseName.Length > 28 ? baseName[..28] : baseName;

    var candidate = baseName;
    var counter = 2;
    while (usedNames.Contains(candidate))
    {
        candidate = $"{baseName}_{counter}";
        counter++;
    }

    usedNames.Add(candidate);
    return candidate;
}

static void WriteWorksheet(XLWorkbook workbook, string name, List<Dictionary<string, string>> rows)
{
    var sheetName = EnsureUniqueName(workbook, name);
    var worksheet = workbook.Worksheets.Add(sheetName);

    if (rows.Count == 0)
    {
        worksheet.Cell(1, 1).Value = "No rows to display";
        return;
    }

    var headers = rows.SelectMany(r => r.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    for (var col = 0; col < headers.Count; col++)
    {
        worksheet.Cell(1, col + 1).Value = headers[col];
    }

    for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
    {
        var row = rows[rowIndex];
        for (var colIndex = 0; colIndex < headers.Count; colIndex++)
        {
            var header = headers[colIndex];
            row.TryGetValue(header, out var value);
            worksheet.Cell(rowIndex + 2, colIndex + 1).Value = value ?? string.Empty;
        }
    }

    worksheet.Columns().AdjustToContents();
}

static string EnsureUniqueName(XLWorkbook workbook, string desired)
{
    var candidate = desired;
    var counter = 2;

    while (workbook.Worksheets.Contains(candidate))
    {
        candidate = $"{desired}_{counter}";
        counter++;
    }

    return candidate;
}

class ArraySheetData
{
    public ArraySheetData(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public List<Dictionary<string, string>> Rows { get; } = new();
}
