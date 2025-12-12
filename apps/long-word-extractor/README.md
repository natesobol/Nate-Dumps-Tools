# Long Word Extractor

A lightweight .NET 8 minimal API that finds and counts words above a configurable length across multiple documents.

## Features
- Adjustable word length threshold
- Frequency sorting with deduplication
- Inline text support plus uploads for `.txt`, `.docx`, `.pdf`, `.md/.markdown`, and `.html` files
- Export aggregate counts to CSV or TXT

## Running locally
```bash
cd apps/long-word-extractor
dotnet restore
dotnet run
```
Then open `http://localhost:5000` (or the shown port) in your browser.

## API
- `POST /api/extract` — `multipart/form-data` with `files` (one or more) and optional `text`. Include `threshold` to override the default of 10.
