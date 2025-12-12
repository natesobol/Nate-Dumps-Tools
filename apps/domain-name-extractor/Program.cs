using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    var onlyRootDomains = ParseBool(form["onlyRootDomains"], false);
    var excludeSubdomains = ParseBool(form["excludeSubdomains"], false);
    var groupByDomain = ParseBool(form["groupByDomain"], false);

    if (form.Files.Count == 0 && string.IsNullOrWhiteSpace(form["text"]))
    {
        return Results.BadRequest(new { error = "Upload at least one file or provide inline text." });
    }

    var allMatches = new List<DomainMatch>();
    var items = new List<object>();

    if (!string.IsNullOrWhiteSpace(form["text"]))
    {
        var inlineText = form["text"].ToString();
        var domains = ExtractDomains(inlineText, "Inline text", "text", onlyRootDomains, excludeSubdomains);
        allMatches.AddRange(domains);

        items.Add(new
        {
            source = "Inline text",
            kind = "text",
            count = domains.Count,
            domains,
            error = (string?)null
        });
    }

    foreach (var file in form.Files)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var kind = extension.TrimStart('.');

        var item = new
        {
            source = file.FileName,
            kind,
            count = 0,
            domains = Array.Empty<DomainMatch>(),
            error = (string?)null
        };

        try
        {
            string content = extension switch
            {
                ".txt" or ".csv" or ".json" or ".html" => await ReadAsStringAsync(file),
                ".docx" => await ReadDocxAsync(file),
                _ => throw new InvalidOperationException("Unsupported file type. Use .txt, .docx, .csv, .json, or .html.")
            };

            var domains = ExtractDomains(content, file.FileName, kind, onlyRootDomains, excludeSubdomains);
            allMatches.AddRange(domains);

            item = new
            {
                item.source,
                item.kind,
                count = domains.Count,
                domains,
                error = (string?)null
            };
        }
        catch (Exception ex)
        {
            item = new
            {
                item.source,
                item.kind,
                item.count,
                item.domains,
                error = ex.Message
            };
        }

        items.Add(item);
    }

    var frequencies = allMatches
        .GroupBy(match => match.Domain)
        .Select(group => new
        {
            domain = group.Key,
            count = group.Count()
        })
        .OrderByDescending(entry => entry.count)
        .ThenBy(entry => entry.domain)
        .ToList();

    var grouped = groupByDomain
        ? allMatches
            .GroupBy(match => match.Domain)
            .Select(group => new
            {
                domain = group.Key,
                count = group.Count(),
                samples = group.Take(5).Select(m => m.Context).Distinct().ToList()
            })
            .OrderByDescending(entry => entry.count)
            .ThenBy(entry => entry.domain)
            .ToList()
        : Array.Empty<object>();

    var csv = BuildCsv(allMatches);

    return Results.Ok(new
    {
        onlyRootDomains,
        excludeSubdomains,
        groupByDomain,
        totalMentions = allMatches.Count,
        uniqueDomains = frequencies.Count,
        frequencies,
        grouped,
        results = items,
        csv
    });
});

app.Run();

static bool ParseBool(string? value, bool defaultValue)
{
    return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
}

static async Task<string> ReadAsStringAsync(IFormFile file)
{
    using var reader = new StreamReader(file.OpenReadStream());
    return await reader.ReadToEndAsync();
}

static async Task<string> ReadDocxAsync(IFormFile file)
{
    await using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Seek(0, SeekOrigin.Begin);

    using var wordDoc = WordprocessingDocument.Open(stream, false);
    var body = wordDoc.MainDocumentPart?.Document.Body;
    return body?.InnerText ?? string.Empty;
}

static List<DomainMatch> ExtractDomains(string content, string source, string kind, bool onlyRootDomains, bool excludeSubdomains)
{
    var matches = new List<DomainMatch>();
    if (string.IsNullOrWhiteSpace(content))
    {
        return matches;
    }

    var regex = new Regex("(?:(?:https?://|www\\.)[^\\s\"'<>]+|[A-Za-z0-9.-]+\\.[A-Za-z]{2,})(?=[\\s\\n\\r\"'<>]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    var lines = content.Replace("\r\n", "\n").Split('\n');

    foreach (var line in lines)
    {
        var trimmedLine = line.Trim();
        if (string.IsNullOrEmpty(trimmedLine))
        {
            continue;
        }

        foreach (Match match in regex.Matches(trimmedLine))
        {
            var token = TrimToken(match.Value);
            if (TryNormalizeDomain(token, onlyRootDomains, excludeSubdomains, out var domain))
            {
                matches.Add(domain with { Source = source, Kind = kind, Context = trimmedLine });
            }
        }
    }

    return matches;
}

static string TrimToken(string token)
{
    return token.Trim().Trim().TrimEnd('.', ',', ';', ':', ')', '(', ']', '[', '"', '\'', '>', '<', '!', '?');
}

static bool TryNormalizeDomain(string token, bool onlyRootDomains, bool excludeSubdomains, out DomainMatch match)
{
    match = default;
    if (string.IsNullOrWhiteSpace(token))
    {
        return false;
    }

    var candidate = token;
    if (!candidate.StartsWith("http", StringComparison.OrdinalIgnoreCase))
    {
        candidate = "http://" + candidate;
    }

    if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
    {
        return false;
    }

    var host = uri.Host.Trim();
    if (string.IsNullOrWhiteSpace(host))
    {
        return false;
    }

    var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
    if (excludeSubdomains && labels.Length > 2)
    {
        return false;
    }

    var rootDomain = labels.Length >= 2 ? string.Join('.', labels[^2], labels[^1]) : host;
    var selectedDomain = onlyRootDomains ? rootDomain : host;

    match = new DomainMatch(selectedDomain, token, host, rootDomain, string.Empty, string.Empty);
    return true;
}

static string BuildCsv(IEnumerable<DomainMatch> matches)
{
    var sb = new StringBuilder();
    sb.AppendLine("Domain,Original,Host,RootDomain,Source,Context");

    foreach (var match in matches)
    {
        sb.AppendLine(string.Join(',',
            Escape(match.Domain),
            Escape(match.Original),
            Escape(match.Host),
            Escape(match.RootDomain),
            Escape(match.Source),
            Escape(match.Context)));
    }

    return sb.ToString();
}

static string Escape(string value)
{
    if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    return value;
}

record DomainMatch(string Domain, string Original, string Host, string RootDomain, string Source, string Context)
{
    public string Kind { get; init; } = string.Empty;
}
