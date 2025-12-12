# Extract Table Data From PDF, Word, and HTML Files

A .NET 8 minimal API that detects tables inside PDF, DOCX, HTML, and HTM files, then exports them to clean CSV or Excel workbooks.

## Features
- Upload PDF, DOCX, HTML, or HTM files.
- Auto-detect grid-style and borderless tables.
- Preview detected rows and columns in the browser.
- Export all tables as CSV or XLSX in one click.

## Running locally
```bash
cd apps/table-data-extractor
dotnet run
```

Then open `http://localhost:5100` (or the port displayed in the console) to use the web UI.

## API
- `POST /api/extract` — multipart/form-data with `file`; returns detected tables.
- `POST /api/export` — JSON body with `{ format: "csv" | "xlsx", tables: TableResult[] }` to download the combined export.
