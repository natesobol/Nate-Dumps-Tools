# Capitalized Phrase Extractor

A .NET 8 minimal API that scans emails, texts, and Word documents for multi-word capitalized phrases such as names, job titles, and organization names. Upload `.eml`, `.msg`, `.txt`, or `.docx` files (or paste raw text) to get alphabetized highlights or per-file groupings.

## Features
- Accepts `.eml`, `.msg`, `.txt`, and `.docx` uploads plus pasted text
- Extracts phrases with two or more consecutive capitalized words, keeping connectors like "of" or "and"
- Sort results alphabetically across all sources or by file
- Processes everything in memory; nothing is written to disk beyond temporary `.msg` parsing

## Running locally
1. Install the .NET 8 SDK if needed.
2. From `apps/capitalized-phrase-extractor/`, run:
   ```bash
   dotnet run
   ```
3. Open the printed URL (for example `http://localhost:5180`) and upload your files or paste text to extract capitalized phrases.
