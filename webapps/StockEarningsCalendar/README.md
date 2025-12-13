# Stock Earnings Calendar Downloader Webapp

Minimal ASP.NET Core API that pulls upcoming earnings dates for specific tickers or sectors and exports the results to CSV or iCalendar files. It targets swing traders who want to build an earnings watchlist quickly.

## Features
- Query earnings by comma-separated tickers or sectors.
- Results can be sorted by date (default), ticker, or market cap.
- Export to CSV watchlist or ICS calendar entries.
- Uses the Financial Modeling Prep API and a sector screener endpoint for discovery.

## Configuration
Set your Financial Modeling Prep API key as an environment variable:

```bash
export FMP_API_KEY=your_key_here
```

Optional configuration values can be provided via `appsettings.json` or environment variables under the `FinancialModelingPrep` section:

- `BaseUrl` (default: `https://financialmodelingprep.com/api/`)
- `ApiKey` (defaults to `FMP_API_KEY`)
- `SectorTickerLimit` (defaults to `50`)

## Running locally

```bash
cd webapps/StockEarningsCalendar
# requires .NET 8 SDK
dotnet restore
dotnet run
```

The app listens on the standard Kestrel ports. Visit `http://localhost:5000/docs` for a quick route overview.

## API Examples
- `GET /api/earnings?tickers=AAPL,MSFT&sortBy=marketCap`
- `GET /api/earnings?sectors=Technology&from=2024-01-01&to=2024-03-31`
- `GET /api/export/csv?tickers=AAPL,NVDA`
- `GET /api/export/ics?sectors=Healthcare&calendarName=Earnings%20Radar`

The JSON response mirrors the earnings data along with market cap estimates pulled from the profile endpoint.
