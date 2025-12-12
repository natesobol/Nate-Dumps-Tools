# Sentence Keyword Extractor

A C#/.NET 8 minimal API that scans uploaded documents for sentences containing specific keywords. Upload `.txt`, `.docx`, `.rtf`, `.md`, or `.pdf` files, provide one or more keywords, and download the matched sentences with source file references.

## Running locally

1. From `apps/sentence-keyword-extractor/`, restore dependencies and start the server:

   ```bash
   dotnet run
   ```

2. Open the web UI at `http://localhost:5197` (or whichever port .NET assigns).

3. The API endpoint is available at `POST /api/extract-sentences` for programmatic use.

## Features

- Handles multiple uploads per request (50 MB total limit by default)
- Supports text, markdown, Word, RTF, and PDF files
- Returns full sentences that include any of the provided keywords
- CSV export contains sentence text and source file names for quick reporting
