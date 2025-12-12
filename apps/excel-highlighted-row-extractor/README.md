# Excel Highlighted Row Extractor

A minimal ASP.NET web app that pulls only highlighted or color-coded rows from .xlsx or .xlsm workbooks. Filter by a specific fill color, keep the original fill shading, and export the results as either `.xlsx` or `.csv`.

## Run locally
```bash
cd apps/excel-highlighted-row-extractor
dotnet restore
DOTNET_URLS=http://localhost:5086 dotnet run
```

Then open `http://localhost:5086` to upload Excel files and download filtered rows.

## Features
- Detects manual fill colors and conditional-formatting rules that apply background fills
- Optional color filter (hex) to limit results to a specific highlight
- Choose to export fill colors or raw values only
- Outputs either a compact `.xlsx` workbook or `.csv` with sheet and row context
