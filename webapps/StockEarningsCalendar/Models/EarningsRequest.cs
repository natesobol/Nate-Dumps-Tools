namespace StockEarningsCalendar.Models;

public class EarningsRequest
{
    public List<string> Tickers { get; init; } = new();
    public List<string> Sectors { get; init; } = new();
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string SortBy { get; init; } = "date";

    public static EarningsRequest FromQuery(string? tickers, string? sectors, string? sortBy, DateOnly? from, DateOnly? to)
    {
        var request = new EarningsRequest
        {
            From = from,
            To = to,
            SortBy = string.IsNullOrWhiteSpace(sortBy) ? "date" : sortBy
        };

        if (!string.IsNullOrWhiteSpace(tickers))
        {
            request.Tickers.AddRange(tickers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (!string.IsNullOrWhiteSpace(sectors))
        {
            request.Sectors.AddRange(sectors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return request;
    }
}
