namespace PremarketVolumeSorter.Models;

public record StockAnalysis
{
    public required string Ticker { get; init; }
    public required decimal AverageVolume30Day { get; init; }
    public required decimal PremarketVolume { get; init; }
    public required decimal FloatShares { get; init; }
    public required decimal VolumeMultiple { get; init; }
    public required bool IsThreeXAverage { get; init; }
    public required bool IsLowFloat { get; init; }
    public required DateTime RetrievedAt { get; init; }
}
