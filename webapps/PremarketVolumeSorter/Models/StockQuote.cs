namespace PremarketVolumeSorter.Models;

public record StockQuote
{
    public required string Ticker { get; init; }
    public required decimal AverageVolume30Day { get; init; }
    public required decimal PremarketVolume { get; init; }
    public required decimal FloatShares { get; init; }
    public required DateTime RetrievedAt { get; init; }
}
