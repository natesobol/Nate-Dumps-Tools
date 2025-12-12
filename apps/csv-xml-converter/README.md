# CSV/XML Data Converter

This webapp converts CSV to well-formed XML and XML back to CSV on the fly. It is packaged for Monetize Hub so you can drop it alongside other tools.

## Endpoints
- `GET /csv-xml-converter` - render the converter UI
- `POST /csv-xml-converter` - handle conversion (CSV → XML or XML → CSV)
- `POST /csv-xml-converter/download` - download the converted payload

## Features
- Paste data directly or upload CSV/XML files up to 2 MB
- CSV parsing with header detection and whitespace trimming
- XML parsing that searches for repeating `<row>` or `<record>` elements and flattens nested values
- In-memory processing only—no files are written to disk

## Running locally
1. Install dependencies: `npm install`
2. Start the server: `npm run dev`
3. Visit `http://localhost:3000/csv-xml-converter`
