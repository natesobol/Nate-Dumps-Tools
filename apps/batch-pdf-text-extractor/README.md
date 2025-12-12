# Batch PDF Text Extractor

A .NET 8 minimal API webapp for pulling text out of many PDFs at once. It uses PdfPig to extract text with layered fallbacks so that even tricky documents return something useful. Choose combined text or per-page structured output, and drag-and-drop as many PDFs as you need.

## Features
- Drag and drop or browse multiple PDF files at once
- Layered extraction strategies: layout-aware content order, word-based merging, and letter concatenation fallback
- Structured output with per-page text plus combined text view
- Clear warnings when a fallback was needed or a page was empty
- In-memory processing only—documents are never written to disk

## Running locally
1. Install the .NET 8 SDK if needed.
2. From `apps/batch-pdf-text-extractor/`, run:
   ```bash
   dotnet run
   ```
3. Open the printed URL (for example `http://localhost:5183`) and drop in PDFs to extract their text.

You can also host the static build through the Node.js server once it is running with `npm run dev` from the project root.
