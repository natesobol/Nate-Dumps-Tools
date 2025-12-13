using PremarketVolumeSorter.Models;

namespace PremarketVolumeSorter.Services;

public interface IStockDataProvider
{
    Task<IReadOnlyList<StockQuote>> GetPremarketDataAsync(IEnumerable<string> tickers, CancellationToken cancellationToken = default);
}
