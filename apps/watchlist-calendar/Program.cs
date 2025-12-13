using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<WatchlistParser>();
builder.Services.AddSingleton<MarketEventService>();
builder.Services.AddSingleton<IcsExporter>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/events", async Task<IResult> (
    HttpRequest request,
    WatchlistParser parser,
    MarketEventService marketEventService) =>
{
    var parseResult = await parser.ParseAsync(request);
    if (parseResult.Problem is not null)
    {
        return parseResult.Problem;
    }

    var events = marketEventService.BuildEvents(parseResult.Symbols);

    return Results.Ok(new
    {
        symbols = parseResult.Symbols,
        events,
        warnings = parseResult.Warnings
    });
});

app.MapPost("/api/export-ics", async Task<IResult> (
    HttpRequest request,
    WatchlistParser parser,
    MarketEventService marketEventService,
    IcsExporter exporter) =>
{
    var parseResult = await parser.ParseAsync(request);
    if (parseResult.Problem is not null)
    {
        return parseResult.Problem;
    }

    var events = marketEventService.BuildEvents(parseResult.Symbols);
    if (events.Count == 0)
    {
        return Results.BadRequest(new { error = "No events available to export." });
    }

    var calendarName = parseResult.Symbols.Count switch
    {
        0 => "Watchlist Calendar",
        1 => $"{parseResult.Symbols[0]} Events",
        _ => $"{parseResult.Symbols.Count} Ticker Events"
    };

    var icsContent = exporter.BuildCalendar(events, calendarName);

    return Results.File(Encoding.UTF8.GetBytes(icsContent),
        contentType: "text/calendar",
        fileDownloadName: "watchlist-events.ics");
});

app.Run();

record WatchlistParseResult(List<string> Symbols, List<string> Warnings, IResult? Problem);

class WatchlistParser
{
    private static readonly HashSet<string> AllowedExtensions = [".csv", ".txt"]; // manual and form text allowed by default

    public async Task<WatchlistParseResult> ParseAsync(HttpRequest request)
    {
        if (!request.HasFormContentType)
        {
            return new WatchlistParseResult([], [], Results.BadRequest(new { error = "Expected multipart/form-data payload." }));
        }

        var form = await request.ReadFormAsync();
        var warnings = new List<string>();
        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var manual = form["manualSymbols"].ToString();
        if (!string.IsNullOrWhiteSpace(manual))
        {
            foreach (var symbol in NormalizeSymbols(manual))
            {
                symbols.Add(symbol);
            }
        }

        foreach (var file in form.Files)
        {
            if (file.Length == 0)
            {
                warnings.Add($"{file.FileName} was empty.");
                continue;
            }

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add($"{file.FileName} is not a supported file type. Use .csv or .txt.");
                continue;
            }

            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            foreach (var symbol in NormalizeSymbols(content))
            {
                symbols.Add(symbol);
            }
        }

        if (symbols.Count == 0)
        {
            return new WatchlistParseResult([], warnings, Results.BadRequest(new
            {
                error = "No symbols provided. Add tickers manually or upload a .csv/.txt watchlist.",
                warnings
            }));
        }

        return new WatchlistParseResult(symbols.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList(), warnings, null);
    }

    private static IEnumerable<string> NormalizeSymbols(string content)
    {
        var separators = new[] { '\n', '\r', ',', ';', '\t', ' ' };
        var tokens = content
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Trim().ToUpperInvariant())
            .Where(t => t.Length > 0 && t.Length <= 8);

        foreach (var token in tokens)
        {
            yield return token;
        }
    }
}

enum MarketEventType
{
    Earnings,
    Dividend,
    Split
}

record MarketEvent(string Symbol, MarketEventType EventType, DateTimeOffset Date, string Title, string Notes, string ColorHex);

class MarketEventService
{
    private static readonly Dictionary<MarketEventType, string> Colors = new()
    {
        [MarketEventType.Earnings] = "#2563eb",
        [MarketEventType.Dividend] = "#16a34a",
        [MarketEventType.Split] = "#f97316"
    };

    public List<MarketEvent> BuildEvents(IEnumerable<string> symbols)
    {
        var events = new List<MarketEvent>();
        var baseDate = DateTimeOffset.UtcNow.Date.AddDays(3);

        foreach (var symbol in symbols)
        {
            var hash = ComputeDeterministicOffset(symbol);
            var earningsDate = baseDate.AddDays(hash % 10);
            var dividendDate = baseDate.AddDays((hash % 7) + 12);
            var splitDate = baseDate.AddDays((hash % 5) + 20);

            events.Add(new MarketEvent(symbol, MarketEventType.Earnings, earningsDate,
                $"{symbol} Earnings", "Estimated earnings date from placeholder schedule.", Colors[MarketEventType.Earnings]));

            events.Add(new MarketEvent(symbol, MarketEventType.Dividend, dividendDate,
                $"{symbol} Dividend", "Projected dividend payment date.", Colors[MarketEventType.Dividend]));

            events.Add(new MarketEvent(symbol, MarketEventType.Split, splitDate,
                $"{symbol} Split/Corporate Action", "Placeholder corporate action window.", Colors[MarketEventType.Split]));
        }

        return events.OrderBy(e => e.Date).ThenBy(e => e.Symbol, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static int ComputeDeterministicOffset(string symbol)
    {
        var bytes = Encoding.UTF8.GetBytes(symbol.ToUpperInvariant());
        var hash = SHA256.HashData(bytes);
        return BitConverter.ToInt32(hash, 0) & int.MaxValue;
    }
}

class IcsExporter
{
    public string BuildCalendar(IEnumerable<MarketEvent> events, string calendarName)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BEGIN:VCALENDAR");
        builder.AppendLine("VERSION:2.0");
        builder.AppendLine("PRODID:-//Watchlist Calendar Exporter//EN");
        builder.AppendLine("CALSCALE:GREGORIAN");
        builder.AppendLine($"X-WR-CALNAME:{Escape(calendarName)}");

        foreach (var evt in events)
        {
            builder.AppendLine("BEGIN:VEVENT");
            builder.AppendLine($"UID:{Guid.NewGuid()}@watchlist");
            builder.AppendLine($"DTSTAMP:{FormatDate(DateTimeOffset.UtcNow)}");
            builder.AppendLine($"DTSTART;VALUE=DATE:{FormatDate(evt.Date)}");
            builder.AppendLine($"SUMMARY:{Escape(evt.Title)}");
            builder.AppendLine($"DESCRIPTION:{Escape(evt.Notes)}");
            builder.AppendLine($"CATEGORIES:{evt.EventType}");
            builder.AppendLine($"X-COLOR:{evt.ColorHex}");
            builder.AppendLine("END:VEVENT");
        }

        builder.AppendLine("END:VCALENDAR");
        return builder.ToString();
    }

    private static string FormatDate(DateTimeOffset date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private static string Escape(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace(",", "\\,")
            .Replace(";", "\\;")
            .Replace("\n", "\\n");
    }
}
