# Presentation Heading Extractor

A C#/.NET 8 minimal API webapp that pulls slide titles, bullet headers, and section dividers from presentations. It supports PowerPoint (.pptx), Keynote (.key), PDF slide decks, and OpenDocument presentations (.odp), with options to preview results or export to TXT, CSV, or DOCX.

## Running locally

1. From `apps/presentation-heading-extractor/`, restore dependencies and start the server:

   ```bash
   dotnet run
   ```

2. Open the UI at the indicated port (e.g., `http://localhost:5000`) and upload a presentation.

3. The API is available at `POST /api/extract` and returns JSON when `export=json`.

## Features

- Slide-by-slide headings plus a summary mode for key points
- Supports .pptx, .key, .pdf, and .odp inputs
- Exports to .txt, .csv, or .docx, or returns structured JSON
- Uses LibreOffice for cross-format HTML conversion when native parsing isn't available
