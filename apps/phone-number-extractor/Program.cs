using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using PhoneNumbers;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
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

    var defaultRegion = (form["region"].FirstOrDefault() ?? "US").ToUpperInvariant();
    var format = form["format"].FirstOrDefault() ?? "E164";
    var customFormat = form["customFormat"].FirstOrDefault();
    var deduplicate = bool.TryParse(form["dedupe"].FirstOrDefault(), out var dedupeFlag) && dedupeFlag;

    var phoneUtil = PhoneNumberUtil.GetInstance();
    var results = new List<PhoneHit>();
    var seen = new HashSet<string>();
    var errors = new List<object>();

    foreach (var file in files)
    {
        try
        {
            var cells = await ReadCellsAsync(file);

            foreach (var cellValue in cells)
            {
                if (string.IsNullOrWhiteSpace(cellValue))
                {
                    continue;
                }

                foreach (var match in phoneUtil.FindNumbers(cellValue, defaultRegion, PhoneNumberUtil.Leniency.POSSIBLE, long.MaxValue))
                {
                    var number = match.Number;

                    if (!phoneUtil.IsPossibleNumber(number) || !phoneUtil.IsValidNumber(number))
                    {
                        continue;
                    }

                    var e164 = phoneUtil.Format(number, PhoneNumberFormat.E164);

                    if (deduplicate && !seen.Add(e164))
                    {
                        continue;
                    }

                    results.Add(new PhoneHit
                    {
                        File = file.FileName,
                        OriginalText = match.RawString,
                        CountryCode = number.CountryCode,
                        NationalNumber = number.NationalNumber.ToString(CultureInfo.InvariantCulture),
                        Region = phoneUtil.GetRegionCodeForNumber(number) ?? defaultRegion,
                        E164 = e164,
                        Formatted = FormatNumber(number, format, customFormat, phoneUtil)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add(new { file = file.FileName, message = ex.Message });
        }
    }

    return Results.Ok(new
    {
        count = results.Count,
        deduplicated = deduplicate,
        numbers = results,
        errors
    });
});

app.Run();

static string FormatNumber(PhoneNumber number, string format, string? customFormat, PhoneNumberUtil util)
{
    return format switch
    {
        "National" => util.Format(number, PhoneNumberFormat.NATIONAL),
        "International" => util.Format(number, PhoneNumberFormat.INTERNATIONAL),
        "RFC3966" => util.Format(number, PhoneNumberFormat.RFC3966),
        "Custom" when !string.IsNullOrWhiteSpace(customFormat) => ApplyCustomFormat(number, customFormat!, util),
        _ => util.Format(number, PhoneNumberFormat.E164)
    };
}

static string ApplyCustomFormat(PhoneNumber number, string customFormat, PhoneNumberUtil util)
{
    return customFormat
        .Replace("{E164}", util.Format(number, PhoneNumberFormat.E164))
        .Replace("{International}", util.Format(number, PhoneNumberFormat.INTERNATIONAL))
        .Replace("{National}", util.Format(number, PhoneNumberFormat.NATIONAL))
        .Replace("{RFC3966}", util.Format(number, PhoneNumberFormat.RFC3966))
        .Replace("{CountryCode}", number.CountryCode.ToString(CultureInfo.InvariantCulture))
        .Replace("{NationalNumber}", number.NationalNumber.ToString(CultureInfo.InvariantCulture));
}

static async Task<IEnumerable<string>> ReadCellsAsync(IFormFile file)
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    return extension switch
    {
        ".xls" or ".xlsx" => await ReadExcelAsync(file),
        ".csv" => await ReadCsvAsync(file),
        _ => throw new InvalidOperationException($"Unsupported file type: {extension}. Please upload CSV or Excel files.")
    };
}

static async Task<IEnumerable<string>> ReadExcelAsync(IFormFile file)
{
    using var stream = file.OpenReadStream();
    using var reader = ExcelReaderFactory.CreateReader(stream);
    var values = new List<string>();

    do
    {
        while (reader.Read())
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (!reader.IsDBNull(i))
                {
                    var value = reader.GetValue(i)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }
            }
        }
    } while (reader.NextResult());

    return values;
}

static async Task<IEnumerable<string>> ReadCsvAsync(IFormFile file)
{
    var values = new List<string>();
    using var stream = file.OpenReadStream();
    using var reader = new StreamReader(stream);

    while (!reader.EndOfStream)
    {
        var line = await reader.ReadLineAsync();
        if (line is null)
        {
            continue;
        }

        values.Add(line);
        foreach (var cell in line.Split(','))
        {
            if (!string.IsNullOrWhiteSpace(cell))
            {
                values.Add(cell);
            }
        }
    }

    return values;
}

record PhoneHit
{
    public string File { get; init; } = string.Empty;
    public string OriginalText { get; init; } = string.Empty;
    public int CountryCode { get; init; }
    public string NationalNumber { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string E164 { get; init; } = string.Empty;
    public string Formatted { get; init; } = string.Empty;
}
