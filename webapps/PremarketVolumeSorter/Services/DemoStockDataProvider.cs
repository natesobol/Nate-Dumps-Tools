using PremarketVolumeSorter.Models;

namespace PremarketVolumeSorter.Services;

/// <summary>
/// A placeholder data provider that simulates delayed pre-market data.
/// Replace with a real market data API integration for production use.
/// </summary>
public class DemoStockDataProvider : IStockDataProvider
{
    private static readonly Dictionary<string, decimal> DefaultAverages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AAPL"] = 52000000m,
        ["TSLA"] = 146000000m,
        ["AMD"] = 65000000m,
        ["PLTR"] = 105000000m,
        ["NVDA"] = 47000000m,
        ["MARA"] = 47000000m,
        ["SOFI"] = 37000000m,
        ["GME"] = 8000000m,
        ["AMC"] = 31000000m,
        ["CVNA"] = 18000000m,
    };

    private static readonly Dictionary<string, decimal> DefaultFloats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AAPL"] = 16000000000m,
        ["TSLA"] = 3300000000m,
        ["AMD"] = 1600000000m,
        ["PLTR"] = 2110000000m,
        ["NVDA"] = 2400000000m,
        ["MARA"] = 204000000m,
        ["SOFI"] = 956000000m,
        ["GME"] = 304000000m,
        ["AMC"] = 517000000m,
        ["CVNA"] = 116000000m,
    };

    public Task<IReadOnlyList<StockQuote>> GetPremarketDataAsync(IEnumerable<string> tickers, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var random = new Random(now.Millisecond);

        var quotes = tickers
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToUpperInvariant())
            .Distinct()
            .Select(ticker =>
            {
                var average = DefaultAverages.TryGetValue(ticker, out var avg)
                    ? avg
                    : random.Next(3_000_000, 30_000_000);

                var floatShares = DefaultFloats.TryGetValue(ticker, out var floatValue)
                    ? floatValue
                    : random.Next(20_000_000, 500_000_000);

                var simulatedPremarketVolume = average * (decimal)(0.4 + random.NextDouble() * 4.5);

                return new StockQuote
                {
                    Ticker = ticker,
                    AverageVolume30Day = Math.Round(average, 0),
                    FloatShares = Math.Round(floatShares, 0),
                    PremarketVolume = Math.Round(simulatedPremarketVolume, 0),
                    RetrievedAt = now
                };
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<StockQuote>>(quotes);
    }
}
