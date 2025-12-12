using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

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

    var prefix = form["prefix"].FirstOrDefault()?.Trim();
    var minLength = int.TryParse(form["minLength"].FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minLen)
        ? minLen
        : (int?)null;
    var maxLength = int.TryParse(form["maxLength"].FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxLen)
        ? maxLen
        : (int?)null;

    var skuPatterns = new List<Regex>
    {
        new(@"[A-Z0-9]{2,}(?:-[A-Z0-9]{1,}){1,4}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"[A-Z]{2,}[0-9]{2,}[A-Z0-9-]*", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    var results = new List<SkuResult>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var file in files)
    {
        await using var stream = file.OpenReadStream();

        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet();

        foreach (DataTable table in dataSet.Tables)
        {
            var sheetName = table.TableName;

            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
                {
                    var cellValue = table.Rows[rowIndex][columnIndex]?.ToString();
                    if (string.IsNullOrWhiteSpace(cellValue))
                    {
                        continue;
                    }

                    foreach (var pattern in skuPatterns)
                    {
                        foreach (Match match in pattern.Matches(cellValue))
                        {
                            var candidate = match.Value.Trim();
                            if (!IsValidSku(candidate, prefix, minLength, maxLength))
                            {
                                continue;
                            }

                            var signature = $"{candidate}|{file.FileName}";
                            if (seen.Add(signature))
                            {
                                results.Add(new SkuResult(candidate, file.FileName, sheetName, rowIndex + 1, columnIndex + 1));
                            }
                        }
                    }
                }
            }
        }
    }

    return Results.Ok(new { count = results.Count, skus = results });
});

app.Run();

static bool IsValidSku(string candidate, string? requiredPrefix, int? minLength, int? maxLength)
{
    var sanitized = Regex.Replace(candidate, @"[^A-Za-z0-9]", "");

    if (minLength.HasValue && sanitized.Length < minLength)
    {
        return false;
    }

    if (maxLength.HasValue && sanitized.Length > maxLength)
    {
        return false;
    }

    if (!string.IsNullOrWhiteSpace(requiredPrefix) &&
        !candidate.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return true;
}

public record SkuResult(string Sku, string FileName, string Sheet, int Row, int Column);
