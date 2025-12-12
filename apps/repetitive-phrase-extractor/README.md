# Repetitive Phrase & Sentence Finder

A minimal ASP.NET Core web app that scans inline text plus TXT, DOCX, RTF, and HTML uploads for repeated wording. It surfaces repeated sentences (5+ words) and multi-word phrases (3–8 words), including frequency counts and the line numbers where they appear.

## Endpoints
- `GET /health` - health check.
- `POST /api/analyze` - multipart form endpoint that accepts `files` and optional `text`. Returns repeated sentences and phrases with counts and line numbers per input.

## Running locally
```bash
cd apps/repetitive-phrase-extractor
DOTNET_URLS=http://localhost:5114 dotnet run
```
Then open http://localhost:5114 to use the UI.
