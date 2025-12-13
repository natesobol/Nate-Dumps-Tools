using StockEarningsCalendar.Models;

namespace StockEarningsCalendar.Services;

public class EarningsService
{
    private readonly FinancialModelingPrepClient _fmpClient;

    public EarningsService(FinancialModelingPrepClient fmpClient)
    {
        _fmpClient = fmpClient;
    }

    public async Task<IReadOnlyList<EarningsEvent>> GetEarningsAsync(EarningsRequest request, CancellationToken cancellationToken)
    {
        var tickers = new HashSet<string>(request.Tickers.Select(t => t.ToUpperInvariant()));

        foreach (var sector in request.Sectors)
        {
            var sectorTickers = await _fmpClient.GetTickersForSectorAsync(sector, cancellationToken);
            foreach (var ticker in sectorTickers)
            {
                tickers.Add(ticker.ToUpperInvariant());
            }
        }

        if (tickers.Count == 0)
        {
            return Array.Empty<EarningsEvent>();
        }

        var tasks = tickers.Select(t => _fmpClient.GetEarningsForTickerAsync(t, request.From, request.To, cancellationToken));
        var results = await Task.WhenAll(tasks);
        var flattened = results.SelectMany(r => r).ToList();

        return Sort(flattened, request.SortBy);
    }

    private static IReadOnlyList<EarningsEvent> Sort(List<EarningsEvent> events, string sortBy)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "ticker" => events.OrderBy(e => e.Ticker, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Date).ToList(),
            "marketcap" => events.OrderByDescending(e => e.MarketCap ?? 0).ThenBy(e => e.Date).ToList(),
            _ => events.OrderBy(e => e.Date).ThenBy(e => e.Ticker, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }
}
