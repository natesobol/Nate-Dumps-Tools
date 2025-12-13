using PremarketVolumeSorter.Models;

namespace PremarketVolumeSorter.Services;

public class VolumeAnalyzer
{
    private const decimal LowFloatThreshold = 50_000_000m;
    private const decimal UnusualMultiplier = 3m;

    public IReadOnlyList<StockAnalysis> BuildAnalysis(IEnumerable<StockQuote> quotes)
    {
        return quotes.Select(q => new StockAnalysis
        {
            Ticker = q.Ticker,
            AverageVolume30Day = q.AverageVolume30Day,
            PremarketVolume = q.PremarketVolume,
            FloatShares = q.FloatShares,
            VolumeMultiple = SafeRatio(q.PremarketVolume, q.AverageVolume30Day),
            IsThreeXAverage = q.PremarketVolume >= q.AverageVolume30Day * UnusualMultiplier,
            IsLowFloat = q.FloatShares <= LowFloatThreshold,
            RetrievedAt = q.RetrievedAt
        })
        .OrderByDescending(a => a.VolumeMultiple)
        .ThenByDescending(a => a.PremarketVolume)
        .ToList();
    }

    private static decimal SafeRatio(decimal numerator, decimal denominator)
    {
        if (denominator == 0) return 0;
        return Math.Round(numerator / denominator, 2);
    }
}
