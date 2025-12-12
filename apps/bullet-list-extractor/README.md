# Bullet List Extractor

A C#/.NET 8 minimal API that extracts bulleted and numbered list items from uploaded Word (.docx), PDF, or text files. It preserves list markers (bullets or numbering) and indentation levels so you can quickly copy action items, to-dos, or presentation points.

## Running locally

1. From `apps/bullet-list-extractor/`, restore dependencies and start the server:

   ```bash
   dotnet run
   ```

2. Open the web UI at `http://localhost:5197` (or whichever port .NET assigns) and upload one or more `.docx`, `.pdf`, or `.txt` files.

3. The API endpoint is available at `POST /api/extract-lists` for programmatic use.

## Features

- Handles multiple uploads per request
- Reads Word numbering definitions to keep bullet/number formatting
- Detects indented lists in PDFs and plain text
- Returns JSON with per-file results and total list items found
- 50 MB upload limit by default (configurable via `FormOptions`)
