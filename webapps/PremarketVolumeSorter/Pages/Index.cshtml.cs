using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PremarketVolumeSorter.Models;
using PremarketVolumeSorter.Services;

namespace PremarketVolumeSorter.Pages;

public class IndexModel : PageModel
{
    private readonly WatchlistStore _watchlistStore;
    private readonly IStockDataProvider _dataProvider;
    private readonly VolumeAnalyzer _analyzer;

    public IndexModel(WatchlistStore watchlistStore, IStockDataProvider dataProvider, VolumeAnalyzer analyzer)
    {
        _watchlistStore = watchlistStore;
        _dataProvider = dataProvider;
        _analyzer = analyzer;
    }

    [BindProperty]
    public IFormFile? Upload { get; set; }

    public List<string> Watchlist { get; private set; } = new();
    public List<StockAnalysis> Results { get; private set; } = new();
    public string StatusMessage { get; private set; } = string.Empty;

    public void OnGet()
    {
        Watchlist = _watchlistStore.GetWatchlist().ToList();
        Results = _watchlistStore.GetLastResults().ToList();
    }

    public async Task<IActionResult> OnPostScanAsync()
    {
        Watchlist = _watchlistStore.GetWatchlist().ToList();
        if (!Watchlist.Any())
        {
            StatusMessage = "Add at least one ticker to scan.";
            return Page();
        }

        var quotes = await _dataProvider.GetPremarketDataAsync(Watchlist);
        Results = _analyzer.BuildAnalysis(quotes).ToList();
        _watchlistStore.SetLastResults(Results);
        StatusMessage = $"Scanned {Watchlist.Count} tickers at {DateTime.Now:t}.";
        return Page();
    }

    public IActionResult OnPostUseDefault()
    {
        _watchlistStore.ResetToDefault();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (Upload == null || Upload.Length == 0)
        {
            StatusMessage = "Upload a CSV or text file with tickers.";
            OnGet();
            return Page();
        }

        using var reader = new StreamReader(Upload.OpenReadStream());
        var text = await reader.ReadToEndAsync();
        var tickers = text
            .Split(new[] { '\n', '\r', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();

        if (!tickers.Any())
        {
            StatusMessage = "No tickers found in the file.";
            OnGet();
            return Page();
        }

        _watchlistStore.SetWatchlist(tickers);
        StatusMessage = $"Loaded {tickers.Count} tickers.";
        return RedirectToPage();
    }

    public IActionResult OnPostExport()
    {
        var results = _watchlistStore.GetLastResults();
        if (!results.Any())
        {
            StatusMessage = "Run a scan before exporting.";
            OnGet();
            return Page();
        }

        var builder = new StringBuilder();
        builder.AppendLine("Ticker,PremarketVolume,AverageVolume30Day,VolumeMultiple,FloatShares,IsThreeXAverage,IsLowFloat,Timestamp");
        foreach (var row in results)
        {
            builder.AppendLine($"{row.Ticker},{row.PremarketVolume},{row.AverageVolume30Day},{row.VolumeMultiple},{row.FloatShares},{row.IsThreeXAverage},{row.IsLowFloat},{row.RetrievedAt:O}");
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return File(bytes, "text/csv", "premarket-volume.csv");
    }
}
