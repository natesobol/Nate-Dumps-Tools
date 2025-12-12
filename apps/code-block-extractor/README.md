# Code Block Extractor

A minimal ASP.NET Core webapp that scans documentation files for fenced, inline, HTML-styled, or indented code snippets.

## Features
- Accepts `.md`, `.txt`, `.docx`, `.html/.htm`, and `.pdf` uploads
- Finds Markdown code fences, inline backtick snippets, indented code blocks, and `<pre>/<code>` HTML sections
- Light language auto-detection using fence hints, CSS classes, file extensions, or simple heuristics
- Download each snippet individually or create a consolidated export

## Run locally
```bash
cd apps/code-block-extractor
dotnet run
```

Then open `http://localhost:5000` (or the shown port) to use the tool.
