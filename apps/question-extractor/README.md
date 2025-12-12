# Question Extractor

A .NET 8 minimal API that pulls every question sentence from interview transcripts, surveys, and PDFs. Upload `.pdf`, `.txt`, `.docx`, or `.csv` files, and the tool returns grouped results per file with optional context snippets for qualitative analysis.

## Run locally
```bash
cd apps/question-extractor
dotnet run
```
Then open http://localhost:5000 (or the port shown in the console) and use the web UI.

## API
- `POST /api/extract-questions` — multipart/form-data with one or more files (field name `files`).
  - Optional field `includeContext` (`true`/`false`) to include surrounding text snippets.
  - Response: `{ files: [{ fileName, questions: [{ question, index, context? }] }], totalQuestions }`.

Processing happens in-memory; files are not persisted.
