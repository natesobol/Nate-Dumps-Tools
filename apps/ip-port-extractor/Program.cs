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
    var filterValue = form["filter"].ToString();
    var filter = filterValue?.ToLowerInvariant() switch
    {
        "public" => FilterMode.PublicOnly,
        "private" => FilterMode.PrivateOnly,
        _ => FilterMode.All
    };

    if (form.Files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .txt, .log, .conf, .json, or .xml file." });
    }

    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".conf", ".json", ".xml"
    };

    var results = new List<object>();
    var totalMatches = 0;

    foreach (var file in form.Files)
    {
        var extension = Path.GetExtension(file.FileName);

        if (!allowed.Contains(extension))
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = Array.Empty<object>(),
                error = "Unsupported file type."
            });
            continue;
        }

        if (file.Length == 0)
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = Array.Empty<object>(),
                error = "File was empty."
            });
            continue;
        }

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();
            var matches = ExtractAddresses(content, filter);
            totalMatches += matches.Count;

            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = matches
                    .OrderBy(m => m.Line)
                    .ThenBy(m => m.Address, StringComparer.OrdinalIgnoreCase)
                    .Select(m => new
                    {
                        address = m.Address,
                        port = m.Port,
                        version = m.Version,
                        isPrivate = m.IsPrivate,
                        line = m.Line,
                        snippet = m.Snippet,
                        timestamp = m.Timestamp
                    }),
                error = (string?)null
            });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                source = file.FileName,
                kind = extension.TrimStart('.'),
                matches = Array.Empty<object>(),
                error = ex.Message
            });
        }
    }

    return Results.Ok(new
    {
        filesProcessed = results.Count,
        totalMatches,
        filter = filter.ToString(),
        results
    });
});

app.Run();

static List<IpMatch> ExtractAddresses(string content, FilterMode filter)
{
    var matches = new List<IpMatch>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

    var candidateRegex = new Regex(
        @"(?<token>(?<ip>(?:(?:\d{1,3}\.){3}\d{1,3})|\[[0-9A-Fa-f:]+\]|[0-9A-Fa-f:]{2,}))(?::(?<port>\d{1,5}))?",
        RegexOptions.Compiled
    );

    var timestampRegex = new Regex(
        @"(?<ts>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:?\d{2})?|[A-Z][a-z]{2}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2}|\d{2}/\d{2}/\d{4}\s+\d{2}:\d{2}:\d{2})",
        RegexOptions.Compiled
    );

    for (var i = 0; i < lines.Length; i++)
    {
        var lineNumber = i + 1;
        var line = lines[i];
        var timestamp = ExtractTimestamp(line, timestampRegex);

        foreach (Match match in candidateRegex.Matches(line))
        {
            var rawIp = match.Groups["ip"].Value;
            var portText = match.Groups["port"].Value;

            var cleanedIp = rawIp.Trim('[', ']');
            if (!IPAddress.TryParse(cleanedIp, out var ipAddress))
            {
                continue;
            }

            int? port = null;
            if (!string.IsNullOrEmpty(portText) && int.TryParse(portText, out var parsedPort) && parsedPort >= 0 && parsedPort <= 65535)
            {
                port = parsedPort;
            }

            var isPrivate = IsPrivate(ipAddress);
            if (filter == FilterMode.PrivateOnly && !isPrivate)
            {
                continue;
            }
            if (filter == FilterMode.PublicOnly && isPrivate)
            {
                continue;
            }

            var version = ipAddress.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6";
            var key = $"{ipAddress}::{port}::{lineNumber}";

            if (seen.Add(key))
            {
                matches.Add(new IpMatch(ipAddress.ToString(), port, version, isPrivate, lineNumber, line.Trim(), timestamp));
            }
        }
    }

    return matches;
}

static bool IsPrivate(IPAddress address)
{
    if (IPAddress.IsLoopback(address))
    {
        return true;
    }

    if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
    {
        address = address.MapToIPv4();
    }

    if (address.AddressFamily == AddressFamily.InterNetwork)
    {
        var bytes = address.GetAddressBytes();

        return bytes[0] switch
        {
            10 => true,
            172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
            192 when bytes[1] == 168 => true,
            169 when bytes[1] == 254 => true,
            127 => true,
            _ => false
        };
    }

    if (address.AddressFamily == AddressFamily.InterNetworkV6)
    {
        var bytes = address.GetAddressBytes();
        var firstByte = bytes[0];

        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
        {
            return true;
        }

        if ((firstByte & 0xfe) == 0xfc)
        {
            return true; // Unique local addresses fc00::/7
        }

        if (address.IsIPv6Multicast)
        {
            return true;
        }
    }

    return false;
}

static string? ExtractTimestamp(string line, Regex regex)
{
    var match = regex.Match(line);
    if (!match.Success)
    {
        return null;
    }

    return match.Groups["ts"].Value;
}

enum FilterMode
{
    All,
    PublicOnly,
    PrivateOnly
}

record IpMatch(string Address, int? Port, string Version, bool IsPrivate, int Line, string Snippet, string? Timestamp);
