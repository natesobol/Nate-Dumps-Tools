# Extract Text Inside Quotes

A lightweight .NET 8 static web app that lets you paste or upload text-heavy files (DOCX, TXT, MD, JSON, HTML, and more) and instantly return anything found inside single or double quotes. Processing happens entirely in the browser, so content never leaves your device.

## Features
- Paste raw text or drag-and-drop multiple files at once.
- DOCX support via in-browser parsing of the document XML.
- Smart quote handling for straight and curly quotes.
- Minimum word threshold and optional deduplication.
- Copy individual phrases or copy all results at once.

## Running locally
```
dotnet run --project apps/extract-text-inside-quotes/extract-text-inside-quotes.csproj
```
Then open http://localhost:5000 (or the port shown in the console).
