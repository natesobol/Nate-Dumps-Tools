using PremarketVolumeSorter.Models;

namespace PremarketVolumeSorter.Services;

public class WatchlistStore
{
    private readonly List<string> _tickers = new(DefaultWatchlist);
    private List<StockAnalysis> _lastResults = new();
    private readonly object _lock = new();

    private static readonly List<string> DefaultWatchlist =
    [
        "AAPL", "TSLA", "AMD", "PLTR", "NVDA", "MARA", "SOFI", "GME", "AMC", "CVNA"
    ];

    public IReadOnlyList<string> GetWatchlist()
    {
        lock (_lock)
        {
            return _tickers.ToList();
        }
    }

    public void SetWatchlist(IEnumerable<string> tickers)
    {
        lock (_lock)
        {
            _tickers.Clear();
            _tickers.AddRange(tickers
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToUpperInvariant())
                .Distinct());
        }
    }

    public IReadOnlyList<StockAnalysis> GetLastResults()
    {
        lock (_lock)
        {
            return _lastResults.ToList();
        }
    }

    public void SetLastResults(IEnumerable<StockAnalysis> results)
    {
        lock (_lock)
        {
            _lastResults = results.ToList();
        }
    }

    public void ResetToDefault()
    {
        SetWatchlist(DefaultWatchlist);
    }
}
