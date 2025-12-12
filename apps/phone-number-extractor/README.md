# Phone Number Extractor

A minimal ASP.NET Core 8 webapp that scans Excel or CSV files for U.S. and international phone numbers, normalizes them, and returns a deduplicated list.

## Running locally

```bash
cd apps/phone-number-extractor
dotnet run
```

Then open http://localhost:5000 (or https://localhost:5001) to upload spreadsheets, pick an output format, and download normalized numbers.

## Features
- Accepts `.xls`, `.xlsx`, and `.csv` files
- Extracts phone numbers from any cell using libphonenumber (international support)
- Output formatting options: E.164, international, national, RFC3966, or custom placeholder pattern
- Optional deduplication by country code and national number
- Returns the source file and original text for each match
