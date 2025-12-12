# Text URL Extractor

A browser-hosted C# webapp for extracting valid hyperlinks from folders or multiple uploaded text-based files. Filter to a specific domain and optionally strip results down to subpaths for that host.

## Features
- Drag-and-drop support plus folder selection (`webkitdirectory`) for bulk scanning
- Recognizes .txt, .md, .log, .json, .csv, .xml, and other text-friendly files
- Domain filtering that matches the host and its subdomains
- Optional "subpaths only" mode to drop the scheme/host when the domain filter is applied
- Deduplicated results with per-file counts, copy, and CSV export
- 100% client-side processing

## Running locally
```bash
cd apps/text-url-extractor
dotnet run
```

The app will be available at `http://localhost:5000` by default.
