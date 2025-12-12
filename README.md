# Monetize Hub Starter

A starter website for hosting webapps with monetization in mind. It provides a home page, about page, navigation menu, and a login/account system backed by Supabase Auth and the provided public schema (profiles, apps, user_subscriptions, addresses, payment_methods).

## Static preview
- View the static landing page on GitHub Pages: https://natesobol.github.io/Nates-Free-Tools/
- The preview uses `index.html` plus companion static pages in the root and `/apps/` folders
- Static versions of webapps are available at:
  - Excel to JSON: `/apps/excel-to-json/index.html`
  - YAML/JSON Converter: `/apps/yaml-json-converter/index.html`
- JSON Combiner: `/apps/json-combiner/wwwroot/index.html`
- Batch File Renamer: `/apps/batch-file-renamer/wwwroot/index.html`
- CSV/XML Converter: `/apps/csv-xml-converter/index.html`
- List Comparison / Diff Checker: `/apps/list-comparison/wwwroot/index.html`
- Extract Text Inside Quotes: `/apps/extract-text-inside-quotes/wwwroot/index.html`
- PDF Splitter: `/apps/pdf-splitter/wwwroot/index.html`
- Table Data Extractor: `/apps/table-data-extractor/wwwroot/index.html`
- Bullet List Extractor: `/apps/bullet-list-extractor/wwwroot/index.html`
- PowerPoint → PDF: `/apps/powerpoint-to-pdf/wwwroot/index.html`
- PowerPoint Image Extractor: `/apps/powerpoint-image-extractor/wwwroot/index.html`
- JSON to Excel Creator: `/apps/json-to-excel/wwwroot/index.html`
- Multi-CSV Column Merger: `/apps/multi-csv-column-merger/wwwroot/index.html`
- Phone Number Extractor: `/apps/phone-number-extractor/wwwroot/index.html`
- Product SKU Extractor: `/apps/product-sku-extractor/wwwroot/index.html`
- PDF Link Extractor: `/apps/pdf-link-extractor/wwwroot/index.html`
- Resume Contact Info Extractor: `/apps/resume-contact-extractor/wwwroot/index.html`
- Color Extractor: `/apps/color-extractor/wwwroot/index.html`
- Image Path Extractor: `/apps/image-path-extractor/wwwroot/index.html`
- File Path Extractor: `/apps/file-path-extractor/wwwroot/index.html`
- Capitalized Phrase Extractor: `/apps/capitalized-phrase-extractor/wwwroot/index.html`
- Sentence Keyword Extractor: `/apps/sentence-keyword-extractor/wwwroot/index.html`
- Dynamic features (login, admin, server-backed Excel conversion) require running the Node.js server locally or on a host that supports server-side rendering.

## Features
- Home and About pages with modern UI and navigation menu
- Registration and login backed by Supabase Auth with persistent sessions
- Account dashboard for updating profile details and marketing preferences stored in `public.profiles`
- Admin-only dashboard for reviewing all profiles stored in Supabase
- Excel-to-JSON converter webapp with upload, preview, and JSON download
- CSV/XML converter that handles both directions with previews
- List comparison/diff checker with text, HTML, DOCX, and code-friendly inputs
- Browser-based PDF splitter for page- or size-based slicing
- Supabase database for user data plus SQLite-backed session storage
- EJS templating with responsive styling

## Getting started
1. Install dependencies:
   ```bash
   npm install
   ```
2. Copy the environment template and update secrets as needed:
   ```bash
   cp .env.example .env
   ```
   At minimum set `SUPABASE_URL` and `SUPABASE_ANON_KEY`; the repo defaults to the provided project URL and anon key if none are supplied.
3. Start the development server:
   ```bash
   npm run dev
   ```
4. Visit `http://localhost:3000` to view the site.

## Webapps

### Excel to JSON Converter
Located in `apps/excel-to-json/`, this webapp converts Excel spreadsheets to JSON format.

**Features:**
- Upload `.xls` or `.xlsx` files up to 5 MB
- Preview JSON output with multi-sheet support
- Download converted JSON files
- Server-side processing via Express routes
- Static HTML version for GitHub Pages

**Server Route:** `/excel-to-json`  
**Static Version:** `/apps/excel-to-json/index.html`

### XML/JSON Translator
Located in `apps/xml-json-translator/`, this browser-based translator converts XML snippets to JSON or reverses JSON back to
XML without any uploads.

**Features:**
- Paste or drag-and-drop XML and JSON payloads
- Handles attributes and repeated elements with array-friendly output
- Converts entirely in the browser for quick tickets, demos, or docs

**Server Route:** `/xml-json-translator`
**Static Version:** `/apps/xml-json-translator/index.html`

### YAML/JSON Converter
Located in `apps/yaml-json-converter/`, this in-browser tool flips YAML to JSON or JSON back to YAML for config files and API payloads.

**Features:**
- Client-side parsing powered by `js-yaml` with clear error messages
- Copy-ready formatting that preserves nested objects and arrays
- Swap directions without re-pasting your source text

**Server Route:** `/yaml-json-converter`
**Static Version:** `/apps/yaml-json-converter/index.html`

### JSON Combiner
Located in `apps/json-combiner/`, this C# .NET minimal API webapp combines multiple JSON files.

**Features:**
- Upload multiple JSON files
- Arrays are concatenated
- Objects are deep-merged
- Mixed types are wrapped into a single array
- Standalone web UI

**Run locally:**
```bash
cd apps/json-combiner
dotnet run
```

### Multi-CSV Column Merger
Located in `apps/multi-csv-column-merger/`, this C# minimal API merges several CSV exports using a shared key column.

**Features:**
- Drag-and-drop support for multiple CSV uploads
- Auto-detects common key headers (email, product ID, order ID) with manual override
- Choose inner or outer join behavior for merged rows
- Preview aligned results before downloading
- Export merged data as CSV or Excel

**Run locally:**
```bash
cd apps/multi-csv-column-merger
dotnet run
```

### Phone Number Extractor
Located in `apps/phone-number-extractor/`, this C# minimal API scans Excel or CSV files for U.S. and international phone numbers.

**Features:**
- Upload `.xls`, `.xlsx`, or `.csv` files
- Extract phone numbers from any cell using libphonenumber
- Output options: E.164, international, national, RFC3966, or a custom placeholder pattern
- Optional deduplication by country code and national number
- Returns source file and original text for each match

**Run locally:**
```bash
cd apps/phone-number-extractor
dotnet run
```

### Resume Contact Info Extractor
Located in `apps/resume-contact-extractor/`, this ASP.NET Core static webapp pulls recruiting-ready contacts from resume uploads.

**Features:**
- Upload PDFs, DOCX files, and other text-friendly resume formats in bulk
- Browser-only parsing with pdf.js and Mammoth for quick, private processing
- Detects name candidates, email, phone numbers, plus LinkedIn and GitHub profile links
- Optional deduplication by email
- Export the full contact list to CSV in one click

**Run locally:**
```bash
cd apps/resume-contact-extractor
dotnet run
```

### File Path Extractor
Located in `apps/file-path-extractor/`, this .NET minimal API scans code and config files for referenced asset paths.

**Features:**
- Accepts `.js`, `.py`, `.json`, `.yaml/.yml`, `.md/.markdown`, and `.txt`
- Finds absolute and relative paths like `/images/logo.svg` or `../data/export.csv`
- Reports line numbers and trimmed context snippets for each match
- Export matches to CSV or copy JSON directly from the UI

**Run locally:**
```bash
cd apps/file-path-extractor
dotnet run
```

### Find & Replace Utility
Located in `apps/find-and-replace/`, this .NET minimal API performs bulk find-and-replace operations across pasted text or uploaded files.

**Features:**
- Regex or plain-text matching with case-sensitivity toggle
- Process one or many files at once with per-file match counts
- Handles plain text plus .json, .md, .html, .rtf, and .docx files with downloads in the right format
- Instant previews plus download-ready updated files
- In-memory processing only—no uploads are persisted

**Run locally:**
```bash
cd apps/find-and-replace
dotnet run
```

### Batch File Renamer by Pattern
Located in `apps/batch-file-renamer/`, this .NET 8 minimal API applies consistent naming schemes across many files at once.

**Features:**
- Add prefixes and suffixes without touching file contents
- Optional find-and-replace on filenames with case sensitivity
- Auto-numbering with configurable start, padding, and prefix/suffix placement
- Preview original vs. renamed filenames and download a zip of the new set
- In-memory processing only for quick, safe cleanups

**Run locally:**
```bash
cd apps/batch-file-renamer
dotnet run
```

### CSV/XML Data Converter
Located in `apps/csv-xml-converter/`, this tool converts CSV to XML and XML back to CSV.

**Features:**
- Paste data or upload CSV/XML files
- CSV parsing with header detection and trimming
- XML parsing that detects repeating row/record nodes and flattens nested data
- In-memory processing for quick previews and downloads

**Server Route:** `/csv-xml-converter`
**Static Version:** `/apps/csv-xml-converter/index.html`

### List Comparison / Diff Checker
Located in `apps/list-comparison/`, this C# minimal app compares two lists from pasted text or uploaded documents.

**Features:**
- Accepts TXT/CSV/JSON, HTML/HTM, DOCX, and common source-code files for each list
- Toggle case sensitivity, trimming, and blank-line filtering before comparing
- Returns overlaps plus items unique to List A or List B with counts
- Extracts plain text from DOCX and HTML uploads server-side

**Server Route:** `/list-comparison`
**Static Version:** `/apps/list-comparison/wwwroot/index.html`

### Extract Text Inside Quotes
Located in `apps/extract-text-inside-quotes/`, this C#-hosted browser app isolates text between straight or curly quotes from pasted content or uploaded files.

**Features:**
- Paste raw text or drag-and-drop DOCX, TXT, MD, JSON, and other text-friendly files
- Handles straight and smart quotes with a configurable minimum word count
- Optional deduplication plus one-click copy for single phrases or the entire list

**Server Route:** `/extract-text-inside-quotes`
**Static Version:** `/apps/extract-text-inside-quotes/wwwroot/index.html`

### PDF Splitter
Located in `apps/pdf-splitter/`, this C#-hosted webapp splits one or more PDFs entirely in the browser.

**Features:**
- Upload a PDF (or multiple PDFs) and configure how to split them
- Choose exact page ranges, per-file page counts, or approximate size targets
- Download each split part as soon as it is generated
- Browser-based processing keeps documents local for quick, private workflows

**Server Route:** `/pdf-splitter`
**Static Version:** `/apps/pdf-splitter/wwwroot/index.html`

### Bullet List Extractor
Located in `apps/bullet-list-extractor/`, this .NET 8 minimal API pulls bulleted and numbered lists from uploaded documents.

**Features:**
- Upload multiple `.docx`, `.pdf`, or `.txt` files at once
- Preserves bullet symbols and numbering formats from Word files
- Detects indented list items in PDFs and plain text
- Returns per-file results plus a total count for easy exports

**Server Route:** `/bullet-list-extractor`
**Static Version:** `/apps/bullet-list-extractor/wwwroot/index.html`

### PDF Link Extractor
Located in `apps/pdf-link-extractor/`, this C#-hosted webapp extracts every URL from PDFs by scanning both visible text and link annotations.

**Features:**
- Upload one or more PDFs and parse them locally in the browser
- Captures URLs embedded in text plus clickable link annotations
- Shows pages and source types for each link
- Export results to TXT, CSV, or a clickable HTML index

**Server Route:** `/pdf-link-extractor`
**Static Version:** `/apps/pdf-link-extractor/wwwroot/index.html`

### Batch PDF Text Extractor
Located in `apps/batch-pdf-text-extractor/`, this .NET 8 minimal API extracts text from many PDFs at once with layered fallbacks.

**Features:**
- Drag-and-drop multiple PDFs
- Layout-aware extraction with word-level and letter-level fallbacks
- Combined or per-page structured text output with warnings for tricky pages

**Run locally:**
```bash
cd apps/batch-pdf-text-extractor
dotnet run
```

### Extract Table Data From PDF, Word, and HTML Files
Located in `apps/table-data-extractor/`, this .NET 8 minimal API auto-detects tables in PDFs, DOCX files, and saved HTML pages.

**Features:**
- Accepts `.pdf`, `.docx`, `.html`, and `.htm` uploads
- Detects grid-style and borderless tables with per-table previews
- Honors HTML captions and Word headers while normalizing columns
- Export all detected tables together as CSV or multi-sheet Excel files

**Run locally:**
```bash
cd apps/table-data-extractor
dotnet run
```

### PowerPoint → PDF Converter
Located in `apps/powerpoint-to-pdf/`, this tool uses LibreOffice through the Node server to turn `.ppt` and `.pptx` files into PDFs.

**Features:**
- Upload `.ppt` or `.pptx` files up to 50 MB
- In-memory handling with no disk persistence
- Clear error guidance when LibreOffice is missing from the host

**Server Route:** `/powerpoint-to-pdf`
**Static Version:** `/apps/powerpoint-to-pdf/wwwroot/index.html`

### PowerPoint Slide Exporter
Located in `apps/powerpoint-slide-exporter/`, this .NET 8 webapp converts each slide to PNG images or HTML snippets using LibreOffice.

**Features:**
- Upload `.ppt` or `.pptx` files
- Choose PNG, HTML, or both outputs bundled into a single zip
- Clear guidance when LibreOffice is missing locally

**Run locally:**
```bash
cd apps/powerpoint-slide-exporter
dotnet run
```

### Named Entity Extractor
Located in `apps/named-entity-extractor/`, this C# minimal API hosts a browser-based NLP tool for pulling people, organizations, and locations from documents.

**Features:**
- Upload `.pdf`, `.docx`, or `.txt` files and process them entirely in the browser
- Combined and per-file entity breakdowns with deduplication controls
- Quick JSON export for downstream analysis

**Static Version:** `/apps/named-entity-extractor/wwwroot/index.html`

### Timestamp Extractor
Located in `apps/timestamp-extractor/`, this ASP.NET Core webapp extracts timestamps from transcripts, subtitle files, logs, and chat exports.

**Features:**
- Upload `.srt`, `.vtt`, `.txt`, `.csv`, and `.json` files
- Detects subtitle-style ranges plus standalone time-of-day stamps (e.g., `00:12:30` or `12:30 PM`)
- Computes durations for start/end ranges and shows the context line
- Optional filters for minimum duration or how frequently a timestamp appears across files

**Run locally:**
```bash
cd apps/timestamp-extractor
dotnet run
```

## Project Structure

```
apps/
├── excel-to-json/          # Excel to JSON converter webapp
│   ├── src/routes/         # Express routes
│   ├── views/              # EJS templates
│   ├── index.html          # Static version
│   └── README.md
├── yaml-json-converter/    # YAML ↔ JSON converter webapp
│   └── index.html          # Static version (browser-only)
├── xml-json-translator/    # XML ↔ JSON translator webapp
│   └── index.html          # Static version (browser-only)
├── csv-xml-converter/      # CSV/XML converter webapp
│   ├── src/routes/         # Express routes
│   ├── views/              # EJS templates
│   └── index.html          # Static version
├── list-comparison/        # List comparison/diff checker webapp
│   ├── Program.cs          # Minimal API and document ingestion
│   ├── list-comparison.csproj
│   ├── wwwroot/            # Static UI assets
│   └── README.md
├── pdf-splitter/           # Browser-based PDF splitter webapp
│   ├── Program.cs          # Minimal API hosting the static UI
│   ├── pdf-splitter.csproj # .NET project file
│   ├── wwwroot/            # Static web files (served at /pdf-splitter)
│   └── README.md
├── pdf-link-extractor/     # Browser-based PDF link extractor webapp
│   ├── Program.cs          # Minimal API hosting the static UI
│   ├── pdf-link-extractor.csproj
│   ├── wwwroot/            # Static web files (served at /pdf-link-extractor)
│   └── README.md
├── powerpoint-image-extractor/ # .NET PPTX image extractor
│   ├── Program.cs              # Minimal API handling validation and ZIP packaging
│   ├── powerpoint-image-extractor.csproj
│   └── wwwroot/                # Static UI assets
├── powerpoint-to-pdf/      # LibreOffice-backed PPT/PPTX to PDF converter
│   ├── src/routes/         # Express route handling uploads and conversion
│   ├── wwwroot/            # Static UI assets
│   └── README.md
├── json-combiner/          # C# JSON combiner webapp
│   ├── wwwroot/            # Static web files
│   ├── Program.cs          # Main application
│   └── README.md
├── find-and-replace/       # .NET find-and-replace utility
│   ├── Program.cs          # Minimal API and text/file processor
│   ├── find-and-replace.csproj
│   └── wwwroot/            # Static UI assets
├── named-entity-extractor/ # Browser-based named entity extraction
│   ├── Program.cs          # Minimal API hosting the static UI
│   ├── named-entity-extractor.csproj
│   └── wwwroot/            # Static UI assets
├── batch-file-renamer/     # .NET batch renamer for filename cleanup
│   ├── Program.cs          # Minimal API building renamed archives
│   ├── batch-file-renamer.csproj
│   └── wwwroot/            # Static UI assets
├── file-path-extractor/    # .NET file path extraction utility
│   ├── Program.cs          # Minimal API and path detector
│   ├── file-path-extractor.csproj
│   └── wwwroot/            # Static UI assets
├── date-line-extractor/    # C# date/datetime line extractor
│   ├── Program.cs          # Minimal API reading PDFs, DOCX, TXT, CSV, and logs
│   ├── date-line-extractor.csproj
│   └── wwwroot/            # Static UI assets
└── json-to-excel/          # C# JSON → Excel creator
    ├── wwwroot/            # Static UI assets
    ├── Program.cs          # Minimal API and converter
    └── README.md
```

## Tech Stack
- Node.js + Express (main application)
- Supabase for user data with SQLite session storage
- EJS for server-rendered views
- Helmet and secure session defaults for baseline security
- C# .NET (JSON Combiner, JSON to Excel, PDF Splitter, PDF Link Extractor, Bullet List Extractor, List Comparison, Find & Replace, Batch File Renamer, PowerPoint Image Extractor, and Date Line Extractor webapps)

## Notes
- User passwords are managed by Supabase Auth
- Profile updates persist to Supabase and refresh the session payload
- Replace placeholder links in the footer and pricing sections with production resources when ready
- Old `/excel-to-json.html` in root redirects to new location for backwards compatibility
