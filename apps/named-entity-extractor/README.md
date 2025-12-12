# Named Entity Extractor

A browser-based C# minimal API webapp that surfaces people, organizations, and places from uploaded documents.

## Features
- Upload `.pdf`, `.docx`, or `.txt` files (multiple at once)
- Client-side parsing for PDF/DOCX/TXT
- Named-entity extraction for people, organizations, and locations
- Grouped summaries plus per-file results with optional deduplication
- Download results as JSON

## Run locally
```bash
cd apps/named-entity-extractor
dotnet run
```
Then open `http://localhost:5100` (or the port shown in the console).
