# Product SKU Extractor

A minimal ASP.NET Core 8 webapp that scans Excel or CSV files for alphanumeric SKU patterns (e.g., `ABC-1234-XZ`) across sheets and columns with optional validation rules.

## Running locally

```bash
cd apps/product-sku-extractor
dotnet run
```

Then open http://localhost:5000 (or https://localhost:5001) to upload spreadsheets and download extracted SKUs.

## Features
- Accepts `.xls`, `.xlsx`, and `.csv` files
- Detects SKUs with hyphenated alphanumeric patterns across every sheet and column
- Optional validation rules: prefix matching plus minimum/maximum length checks
- Returns the source file name, worksheet, and cell coordinates for each match
- Deduplicates SKUs per uploaded file
