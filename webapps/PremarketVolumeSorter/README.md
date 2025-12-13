# Premarket Volume Sorter Webapp

A lightweight ASP.NET Core Razor Pages app that ranks a watchlist of tickers by pre-market volume versus their 30-day average. It flags low-float tickers and 3x volume movers, and can export scan results to CSV.

## Getting started

1. Install .NET 8 SDK.
2. From this folder run:
   ```bash
   dotnet restore
   dotnet run
   ```
3. Navigate to `https://localhost:5001` (or the console URL).

## Features
- Upload a CSV/txt watchlist or use the built-in defaults.
- Simulated pre-market volume feed that can be replaced with a live market data provider.
- Flags:
  - **3x average volume**
  - **Low float** (≤ 50M shares)
- Sorts by volume multiple and exports results to CSV.

## Extending with real market data
Replace `DemoStockDataProvider` in `Services/DemoStockDataProvider.cs` with an implementation that calls your preferred data API. Register it in `Program.cs` via `builder.Services.AddSingleton<IStockDataProvider, YourProvider>()`.
