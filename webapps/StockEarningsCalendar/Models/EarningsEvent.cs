namespace StockEarningsCalendar.Models;

public record EarningsEvent
{
    public required string Ticker { get; init; }
    public string? CompanyName { get; init; }
    public DateOnly Date { get; init; }
    public string? TimeOfDay { get; init; }
    public decimal? MarketCap { get; init; }
    public decimal? EstimatedEps { get; init; }
    public decimal? ReportedEps { get; init; }
    public string? FiscalPeriod { get; init; }
    public string Source { get; init; } = "Financial Modeling Prep";
}
