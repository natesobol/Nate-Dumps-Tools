using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    var scope = NormalizeScope(form["scope"]);
    var text = form["text"].ToString();

    if (form.Files.Count == 0 && string.IsNullOrWhiteSpace(text))
    {
        return Results.BadRequest(new { error = "Upload at least one supported file or paste inline text." });
    }

    var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".conf", ".json", ".xml"
    };

    var results = new List<object>();
    var totalMatches = 0;

    if (!string.IsNullOrWhiteSpace(text))
    {
        var matches = ExtractMatches(text, scope);
        totalMatches += matches.Count;

        results.Add(new
        {
            source = "Inline text",
            kind = "text",
            count = matches.Count,
            matches,
            error = (string?)null
        });
    }

    foreach (var file in form.Files)
    {
        var extension = Path.GetExtension(file.FileName);

        if (!allowedExtensions.Contains(extension))
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                count = 0,
                matches = Array.Empty<IpMatch>(),
                error = "Unsupported file type. Use .txt, .log, .conf, .json, or .xml."
            });
            continue;
        }

        if (file.Length == 0)
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                count = 0,
                matches = Array.Empty<IpMatch>(),
                error = "File is empty."
            });
            continue;
        }

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();
            var matches = ExtractMatches(content, scope);
            totalMatches += matches.Count;

            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                count = matches.Count,
                matches,
                error = (string?)null
            });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                count = 0,
                matches = Array.Empty<IpMatch>(),
                error = ex.Message
            });
        }
    }

    return Results.Ok(new
    {
        scope,
        totalMatches,
        inputsProcessed = results.Count,
        results
    });
});

app.Run();

static string NormalizeScope(string? value)
{
    return value?.ToLowerInvariant() switch
    {
        "public" => "public",
        "private" => "private",
        _ => "all"
    };
}

static List<IpMatch> ExtractMatches(string content, string scope)
{
    var matches = new List<IpMatch>();
    var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

    for (var i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        var timestamp = ExtractTimestamp(line);

        foreach (Match match in IpRegex.Matches(line))
        {
            var parsed = ParseMatch(match);
            if (parsed is null)
            {
                continue;
            }

            var isPrivate = IsPrivate(parsed.Value);

            if (scope == "public" && isPrivate)
            {
                continue;
            }

            if (scope == "private" && !isPrivate)
            {
                continue;
            }

            matches.Add(new IpMatch(
                parsed.Address,
                parsed.Port,
                parsed.Version,
                isPrivate ? "private" : "public",
                timestamp,
                i + 1,
                line.Trim()
            ));
        }
    }

    return matches;
}

static string? ExtractTimestamp(string line)
{
    var match = TimestampRegex.Match(line);
    return match.Success ? match.Value : null;
}

static ParsedIp? ParseMatch(Match match)
{
    var ipPort = match.Groups["ipv4"].Success
        ? match.Groups["ipv4"].Value
        : match.Groups["ipv6"].Value;

    if (string.IsNullOrWhiteSpace(ipPort))
    {
        ipPort = match.Groups["full"].Value;
    }

    ipPort = ipPort.Trim('[', ']');

    if (!IPAddress.TryParse(ipPort, out var address))
    {
        return null;
    }

    string? portValue = match.Groups["port"].Success
        ? match.Groups["port"].Value
        : match.Groups["port6"].Value;

    int? port = null;
    if (!string.IsNullOrEmpty(portValue) && int.TryParse(portValue, out var parsedPort) && parsedPort is >= 1 and <= 65535)
    {
        port = parsedPort;
    }

    var version = address.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6";

    return new ParsedIp(address, ipPort, port, version);
}

static bool IsPrivate(IPAddress address)
{
    if (IPAddress.IsLoopback(address))
    {
        return true;
    }

    if (address.AddressFamily == AddressFamily.InterNetwork)
    {
        var bytes = address.GetAddressBytes();

        return bytes switch
        {
            var b when b[0] == 10 => true,
            var b when b[0] == 172 && b[1] is >= 16 and <= 31 => true,
            var b when b[0] == 192 && b[1] == 168 => true,
            var b when b[0] == 169 && b[1] == 254 => true,
            var b when b[0] == 100 && b[1] is >= 64 and <= 127 => true,
            _ => false
        };
    }

    if (address.AddressFamily == AddressFamily.InterNetworkV6)
    {
        var bytes = address.GetAddressBytes();
        var firstByte = bytes[0];

        // fc00::/7 unique local, fe80::/10 link-local, ::1 loopback handled above
        return firstByte is 0xfc or 0xfd || (firstByte == 0xfe && (bytes[1] & 0xc0) == 0x80);
    }

    return false;
}

record IpMatch(string Address, int? Port, string Version, string Scope, string? Timestamp, int Line, string Snippet);

record ParsedIp(IPAddress Value, string Address, int? Port, string Version);

static class Patterns
{
    public const string IpPattern =
        @"(?<full>(?:(?<![\w.])(?<ipv4>(?:25[0-5]|2[0-4]\d|1?\d?\d)(?:\.(?:25[0-5]|2[0-4]\d|1?\d?\d)){3})(?!\d))(?:\:(?<port>\d{1,5}))?|\[?(?<ipv6>(?:(?:[A-Fa-f0-9]{1,4}:){2,7}[A-Fa-f0-9]{0,4}|::1|::))(?:%[\w.]+)?\]?(?:\:(?<port6>\d{1,5}))?)";

    public const string TimestampPattern =
        @"\b\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:?\d{2})?\b|\b\d{2}/\d{2}/\d{4}\s+\d{2}:\d{2}:\d{2}\b|\b\d{4}-\d{2}-\d{2}\b";
}

static readonly Regex IpRegex = new(Patterns.IpPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

static readonly Regex TimestampRegex = new(Patterns.TimestampPattern, RegexOptions.Compiled);
