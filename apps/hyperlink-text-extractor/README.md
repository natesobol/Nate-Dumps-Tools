# Hyperlinked Text Extractor

A .NET 8 minimal API that extracts visible link labels and URLs from docs and web-like files.

## Features
- Upload `.docx`, `.pdf`, `.html`, `.md`, or `.txt` files
- Filter internal vs external links using your domain
- Export results to CSV or TXT and open a clickable HTML preview

## Run locally
```bash
cd apps/hyperlink-text-extractor
dotnet run
```

Visit `http://localhost:5000` (or the console output) to use the tool.
