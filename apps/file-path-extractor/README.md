# File Path Extractor

A C# minimal API that scans uploaded source files for referenced file paths so teams can audit assets before packaging or deployment.

## Features
- Accepts `.js`, `.py`, `.json`, `.yaml`, `.yml`, `.md/.markdown`, and `.txt` uploads
- Detects absolute and relative paths such as `/images/pic.png`, `./data/file.csv`, or `../assets/logo.svg`
- Reports line numbers and source snippets for each match
- Returns deduplicated, per-file match lists that can be exported from the UI

## Running locally
```bash
cd apps/file-path-extractor
dotnet run
```

Then open `http://localhost:5085` (or the port shown in the console) to use the browser UI.
