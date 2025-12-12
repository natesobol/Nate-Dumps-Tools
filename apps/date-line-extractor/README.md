# Date Line Extractor

A minimal ASP.NET Core 8 tool that finds every sentence or log entry containing a recognizable date or time. Upload `.txt`, `.docx`, `.pdf`, `.csv`, or `.log` files, group matches by day/month/year, and download the results with timestamps.

## Running locally

```bash
cd apps/date-line-extractor
dotnet run
```

Then open http://localhost:5000 (or https://localhost:5001) to upload files, choose grouping, and export timestamped entries.

## Features
- Accepts `.txt`, `.docx`, `.pdf`, `.csv`, and `.log` files
- Detects common date/time formats such as `MM/DD/YYYY`, `12 Dec 2025`, `2025-12-31`, and `03:45 PM`
- Splits text into sentences/log lines and surfaces only those with dates or times
- Optional grouping by detected day, month, or year
- Export-ready results include the original entry, file source, and a timestamp column
