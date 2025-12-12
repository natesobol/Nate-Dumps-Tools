# Monetize Hub Starter

A starter website for hosting webapps with monetization in mind. It provides a home page, about page, navigation menu, and a login/account system backed by Supabase Auth and the provided public schema (profiles, apps, user_subscriptions, addresses, payment_methods).

## Static preview
- View the static landing page on GitHub Pages: https://natesobol.github.io/Nates-Free-Tools/
- The preview uses `index.html` plus companion static pages in the root and `/apps/` folders
- Static versions of webapps are available at:
  - Excel to JSON: `/apps/excel-to-json/index.html`
  - YAML/JSON Converter: `/apps/yaml-json-converter/index.html`
  - JSON Combiner: `/apps/json-combiner/wwwroot/index.html`
  - CSV/XML Converter: `/apps/csv-xml-converter/index.html`
  - PDF Splitter: `/apps/pdf-splitter/wwwroot/index.html`
  - PowerPoint → PDF: `/apps/powerpoint-to-pdf/wwwroot/index.html`
  - JSON to Excel Creator: `/apps/json-to-excel/wwwroot/index.html`
- Dynamic features (login, admin, server-backed Excel conversion) require running the Node.js server locally or on a host that supports server-side rendering.

## Features
- Home and About pages with modern UI and navigation menu
- Registration and login backed by Supabase Auth with persistent sessions
- Account dashboard for updating profile details and marketing preferences stored in `public.profiles`
- Admin-only dashboard for reviewing all profiles stored in Supabase
- Excel-to-JSON converter webapp with upload, preview, and JSON download
- CSV/XML converter that handles both directions with previews
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

### CSV/XML Data Converter
Located in `apps/csv-xml-converter/`, this tool converts CSV to XML and XML back to CSV.

**Features:**
- Paste data or upload CSV/XML files
- CSV parsing with header detection and trimming
- XML parsing that detects repeating row/record nodes and flattens nested data
- In-memory processing for quick previews and downloads

**Server Route:** `/csv-xml-converter`
**Static Version:** `/apps/csv-xml-converter/index.html`

### PDF Splitter
Located in `apps/pdf-splitter/`, this C#-hosted webapp splits one or more PDFs entirely in the browser.

**Features:**
- Upload a PDF (or multiple PDFs) and configure how to split them
- Choose exact page ranges, per-file page counts, or approximate size targets
- Download each split part as soon as it is generated
- Browser-based processing keeps documents local for quick, private workflows

**Server Route:** `/pdf-splitter`
**Static Version:** `/apps/pdf-splitter/wwwroot/index.html`

### PowerPoint → PDF Converter
Located in `apps/powerpoint-to-pdf/`, this tool uses LibreOffice through the Node server to turn `.ppt` and `.pptx` files into PDFs.

**Features:**
- Upload `.ppt` or `.pptx` files up to 50 MB
- In-memory handling with no disk persistence
- Clear error guidance when LibreOffice is missing from the host

**Server Route:** `/powerpoint-to-pdf`
**Static Version:** `/apps/powerpoint-to-pdf/wwwroot/index.html`

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
├── pdf-splitter/           # Browser-based PDF splitter webapp
│   ├── Program.cs          # Minimal API hosting the static UI
│   ├── pdf-splitter.csproj # .NET project file
│   ├── wwwroot/            # Static web files (served at /pdf-splitter)
│   └── README.md
├── powerpoint-to-pdf/      # LibreOffice-backed PPT/PPTX to PDF converter
│   ├── src/routes/         # Express route handling uploads and conversion
│   ├── wwwroot/            # Static UI assets
│   └── README.md
├── json-combiner/          # C# JSON combiner webapp
│   ├── wwwroot/            # Static web files
│   ├── Program.cs          # Main application
│   └── README.md
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
- C# .NET (JSON Combiner, JSON to Excel, and PDF Splitter webapps)

## Notes
- User passwords are managed by Supabase Auth
- Profile updates persist to Supabase and refresh the session payload
- Replace placeholder links in the footer and pricing sections with production resources when ready
- Old `/excel-to-json.html` in root redirects to new location for backwards compatibility
