# Batch File Renamer by Pattern

A .NET 8 minimal API that renames multiple files in-memory using prefixes, suffixes, find-and-replace rules, and auto-numbering.

## Features
- Apply prefixes and suffixes without touching file contents
- Optional search-and-replace on filenames with case sensitivity
- Auto-numbering with configurable start, padding, and prefix/suffix placement
- Preview the before/after mapping and download a zip of the renamed files
- Processes uploads entirely in memory—no disk writes

## Run locally
```bash
cd apps/batch-file-renamer
dotnet run
```

Visit `http://localhost:5000` to use the UI at `/` and the API at `/api/rename`.
