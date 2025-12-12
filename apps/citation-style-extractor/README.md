# Citation Style Extractor

A lightweight .NET 8 web app that pulls inline citations and bibliography entries from academic files. Upload DOCX, PDF, TXT, or RTF papers and export the detected citations to CSV or BibTeX.

## Running the app

```
dotnet run --project apps/citation-style-extractor/citation-style-extractor.csproj
```

Then open [http://localhost:5000](http://localhost:5000) (or the port shown in the console).

## API

`POST /api/extract`

- **files**: one or more files (.docx, .pdf, .txt, .rtf)
- **includeInline**: `true|false` (default `true`)
- **includeBibliography**: `true|false` (default `true`)
- **export**: `csv`, `bib`, `all`, or blank for JSON only

### Response

```json
{
  "includeInline": true,
  "includeBibliography": true,
  "files": [
    {
      "file": "paper.docx",
      "inlineCount": 3,
      "bibliographyCount": 5,
      "inlineCitations": ["(Smith, 2022)", "[12]"],
      "bibliography": ["Smith, J. ..."],
      "csv": "...base64...",
      "bibTeX": "@article{ref1,...}"
    }
  ]
}
```

`csv` is base64-encoded content; `bibTeX` is plain text ready for download.
