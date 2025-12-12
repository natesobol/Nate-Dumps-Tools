# Pattern-Based Text Extractor

A .NET 8 minimal API that scans uploaded `.txt`, `.csv`, `.html`, and `.docx` files for regex or keyword patterns. It returns every match plus the line context so you can audit logs, scrape semi-structured data, or spot emails and phone numbers quickly.

## Features
- Drag-and-drop upload for multiple files at once
- Regex mode with case-sensitivity toggle
- Keyword mode (comma, semicolon, or newline separated)
- Context-rich results with file name, extension, and line number
- Works with text, CSV, HTML (script/style stripped), and DOCX sources

## Running locally
```bash
cd apps/pattern-text-extractor
DOTNET_ENVIRONMENT=Development dotnet run
```

Then open `http://localhost:5000` (or the port shown in the console) to use the web UI.
