using System.Globalization;
using System.Text;
using StockEarningsCalendar.Models;

namespace StockEarningsCalendar.Services;

public class CalendarExporter
{
    public string ToCsv(IEnumerable<EarningsEvent> events)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Date,Ticker,Company,Time Of Day,Market Cap,Estimated EPS,Reported EPS,Fiscal Period,Source");

        foreach (var item in events)
        {
            var values = new[]
            {
                item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Escape(item.Ticker),
                Escape(item.CompanyName ?? string.Empty),
                Escape(item.TimeOfDay ?? string.Empty),
                item.MarketCap?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.EstimatedEps?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.ReportedEps?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Escape(item.FiscalPeriod ?? string.Empty),
                Escape(item.Source)
            };
            builder.AppendLine(string.Join(',', values));
        }

        return builder.ToString();
    }

    public string ToIcs(IEnumerable<EarningsEvent> events, string? calendarName = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BEGIN:VCALENDAR");
        builder.AppendLine("VERSION:2.0");
        builder.AppendLine($"PRODID:-//NatesFreeTools//Stock Earnings Calendar//EN");
        if (!string.IsNullOrWhiteSpace(calendarName))
        {
            builder.AppendLine($"X-WR-CALNAME:{EscapeText(calendarName!)}");
        }

        foreach (var item in events)
        {
            builder.AppendLine("BEGIN:VEVENT");
            builder.AppendLine($"SUMMARY:{EscapeText(item.Ticker)} earnings");
            builder.AppendLine($"DTSTART;VALUE=DATE:{item.Date:yyyyMMdd}");
            builder.AppendLine($"DTEND;VALUE=DATE:{item.Date.AddDays(1):yyyyMMdd}");
            builder.AppendLine($"DESCRIPTION:{EscapeText(BuildDescription(item))}");
            builder.AppendLine($"UID:{Guid.NewGuid()}@earnings-calendar");
            builder.AppendLine("END:VEVENT");
        }

        builder.AppendLine("END:VCALENDAR");
        return builder.ToString();
    }

    private static string Escape(string value) => value.Contains(',') ? $"\"{value.Replace("\"", "\"\"") }\"" : value.Replace("\n", " ");

    private static string EscapeText(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\n", "\\n");
    }

    private static string BuildDescription(EarningsEvent item)
    {
        var details = new List<string>
        {
            $"Ticker: {item.Ticker}"
        };

        if (!string.IsNullOrWhiteSpace(item.CompanyName))
        {
            details.Add($"Company: {item.CompanyName}");
        }

        if (!string.IsNullOrWhiteSpace(item.TimeOfDay))
        {
            details.Add($"Time: {item.TimeOfDay}");
        }

        if (item.MarketCap.HasValue)
        {
            details.Add($"Market Cap: {item.MarketCap.Value.ToString("N0", CultureInfo.InvariantCulture)}");
        }

        if (item.EstimatedEps.HasValue)
        {
            details.Add($"Estimated EPS: {item.EstimatedEps.Value}");
        }

        if (item.ReportedEps.HasValue)
        {
            details.Add($"Reported EPS: {item.ReportedEps.Value}");
        }

        if (!string.IsNullOrWhiteSpace(item.FiscalPeriod))
        {
            details.Add($"Fiscal Period: {item.FiscalPeriod}");
        }

        details.Add($"Source: {item.Source}");
        return string.Join("\\n", details);
    }
}
