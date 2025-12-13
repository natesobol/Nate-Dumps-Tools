# Watchlist to Calendar Exporter

A C# minimal API webapp that converts a .csv/.txt watchlist or manual ticker list into a color-coded calendar of earnings, dividend, and split events. Generates an .ics file and a printer-friendly sheet.

## Features
- Upload comma-, space-, or line-delimited watchlists (.csv/.txt) or paste tickers manually.
- Deterministic placeholder schedule ready to swap with Yahoo Finance or IEX API responses.
- Color-coded event grid plus downloadable .ics calendar output.
- Printable event sheet with legend and calendar-ready formatting.

## Running locally
```bash
cd apps/watchlist-calendar
dotnet run
```
The app listens on `http://localhost:5000` by default. Open `http://localhost:5000` in your browser to use the UI.
