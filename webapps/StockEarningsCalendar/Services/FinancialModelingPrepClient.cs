using System.Net.Http.Json;
using System.Text.Json;
using StockEarningsCalendar.Models;

namespace StockEarningsCalendar.Services;

public class FinancialModelingPrepClient
{
    private readonly HttpClient _httpClient;
    private readonly ApiOptions _options;

    public FinancialModelingPrepClient(HttpClient httpClient, ApiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(options.BaseUrl);
        }
    }

    public async Task<IReadOnlyList<string>> GetTickersForSectorAsync(string sector, CancellationToken cancellationToken)
    {
        var query = $"v3/stock-screener?sector={Uri.EscapeDataString(sector)}&limit={_options.SectorTickerLimit}&apikey={_options.ApiKey}";
        var response = await _httpClient.GetAsync(query, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<string>();
        }

        var payload = await response.Content.ReadFromJsonAsync<List<StockScreenerEntry>>(cancellationToken: cancellationToken);
        return payload?.Select(p => p.Symbol).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? Array.Empty<string>();
    }

    public async Task<IReadOnlyList<EarningsEvent>> GetEarningsForTickerAsync(string ticker, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var path = $"v3/earning_calendar/{ticker}?apikey={_options.ApiKey}";
        if (from.HasValue)
        {
            path += $"&from={from:yyyy-MM-dd}";
        }

        if (to.HasValue)
        {
            path += $"&to={to:yyyy-MM-dd}";
        }

        var response = await _httpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<EarningsEvent>();
        }

        var raw = await response.Content.ReadFromJsonAsync<List<EarningCalendarEntry>>(cancellationToken: cancellationToken);
        if (raw is null)
        {
            return Array.Empty<EarningsEvent>();
        }

        var marketCap = await GetMarketCapAsync(ticker, cancellationToken);

        return raw
            .Where(e => e.Date is not null)
            .Select(entry => new EarningsEvent
            {
                Ticker = entry.Symbol ?? ticker.ToUpperInvariant(),
                CompanyName = entry.Name,
                Date = DateOnly.FromDateTime(entry.Date!.Value.Date),
                TimeOfDay = entry.Time,
                EstimatedEps = entry.EpsEstimated,
                ReportedEps = entry.Eps,
                FiscalPeriod = entry.FiscalDateEnding,
                MarketCap = marketCap
            })
            .ToList();
    }

    private async Task<decimal?> GetMarketCapAsync(string ticker, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"v3/profile/{ticker}?apikey={_options.ApiKey}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() > 0)
            {
                var first = document.RootElement[0];
                if (first.TryGetProperty("mktCap", out var marketCapProperty) && marketCapProperty.TryGetDecimal(out var marketCap))
                {
                    return marketCap;
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private sealed class EarningCalendarEntry
    {
        public string? Symbol { get; set; }
        public string? Name { get; set; }
        public DateTime? Date { get; set; }
        public string? Time { get; set; }
        public decimal? Eps { get; set; }
        public decimal? EpsEstimated { get; set; }
        public string? FiscalDateEnding { get; set; }
    }

    private sealed class StockScreenerEntry
    {
        public string? Symbol { get; set; }
    }
}
