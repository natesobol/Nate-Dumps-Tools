# Resume Contact Info Extractor

A minimal ASP.NET Core 8 static webapp for recruiters. Upload PDF, DOCX, TXT, and other text-based resumes in bulk to pull name, email, phone, LinkedIn, and GitHub details, then export everything to CSV.

## Running locally

```bash
cd apps/resume-contact-extractor
dotnet run
```

Open http://localhost:5000 (or https://localhost:5001) to start uploading resumes and downloading the spreadsheet-ready contact list.

## Features
- Bulk upload of PDF, DOCX, TXT, RTF, and other text-friendly files
- Client-side parsing with pdf.js and Mammoth—no files leave the browser
- Heuristics to infer candidate names plus detection for email, phone, LinkedIn, and GitHub links
- Optional deduplication by email before exporting
- One-click CSV export of all extracted contacts
