# Table of Contents Extractor

This browser-first C# webapp extracts table of contents (TOC) entries from PDFs, DOCX files, and EPUB books. It identifies structured headings and TOC navigation to return either a flat list or a nested hierarchy, including page numbers whenever the format supports them.

## Features
- Upload a PDF, DOCX, or EPUB file directly in the browser
- Auto-detect structured headings or built-in TOC navigation
- Export both a flat list of titles and a nested hierarchy view
- Attempts to report page numbers when available (PDF) or location hints for other formats
- Runs entirely in the browser so documents stay local

## Running locally
1. Install the .NET 8 SDK if you don't already have it
2. From `apps/table-of-contents-extractor/`, run `dotnet run`
3. Open the URL printed by the app (for example `http://localhost:5183`) to load the extractor UI

You can also access the static build through the Node.js host at `/table-of-contents-extractor` once `npm run dev` is running from the project root.
