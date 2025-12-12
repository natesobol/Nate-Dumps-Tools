# Currency and Percent Sentence Extractor

A C#/.NET 8 minimal API that scans uploaded documents for sentences or rows containing currency or percentage values. Upload `.txt`, `.csv`, `.xlsx`, `.docx`, or `.pdf` files, optionally set a minimum value (e.g., `1000` for `$1000+`), and export matched lines with file names and line numbers as CSV or TXT.

## Running locally

1. From `apps/currency-percent-sentence-extractor/`, restore dependencies and start the server:

   ```bash
   dotnet run
   ```

2. Open the web UI at `http://localhost:5197` (or whichever port .NET assigns).

3. The API endpoint is available at `POST /api/extract-currency-sentences` for programmatic use.

## Features

- Handles TXT, CSV, XLSX, DOCX, and PDF uploads (50 MB total limit by default)
- Detects $, €, £, ¥, ₹, ₽, ₩, ₺, ¢, and % values with optional numeric thresholding
- Returns file name, line/row number, and raw sentence/row text
- Exports combined results to CSV or plaintext for audits and reports
