# JSON to Excel (.xlsx) Creator

A C# minimal API that converts JSON payloads or uploaded files into a formatted Excel workbook. Top-level arrays land in a `Data` sheet, nested arrays become their own sheets with index columns, and nested objects are flattened with dot notation.

## Run locally
```bash
cd apps/json-to-excel
dotnet restore
DOTNET_URLS=http://localhost:5075 dotnet run
```

Then open `http://localhost:5075` and paste JSON or drag/drop a `.json` file. Downloaded workbooks are generated on the fly using ClosedXML.

## Features
- Accepts either pasted JSON text or a single uploaded file (enforced per request)
- Generates `.xlsx` output with auto-sized columns
- Nested arrays are split into separate sheets with links from the parent row
- Nulls are rendered as blank cells; primitive arrays become simple value rows
