# PDF Link Extractor

A browser-hosted webapp for extracting every URL from one or more PDF files. It scans both the visible text and link annotations, then lets you export the findings to TXT, CSV, or a clickable HTML index.

## Features
- Drag-and-drop or browse for multiple PDFs
- Detects URLs inside text plus link annotations on every page
- Consolidated results with page numbers and source types
- Export as TXT, CSV, or a clickable HTML index
- 100% in-browser processing using pdf.js

## Running locally
```bash
cd apps/pdf-link-extractor
dotnet run
```

The app will be available at `http://localhost:5000` by default.
