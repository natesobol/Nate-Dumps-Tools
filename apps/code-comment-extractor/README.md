# C# Code Comment Extractor

A minimal ASP.NET Core webapp that extracts comments from source files. Upload JavaScript, TypeScript, Python, Java, C, C++, or HTML files and the API returns each commented line with file name and line numbers.

## Features
- Detects single-line (`//`, `#`) and block comments (`/* */`, `<!-- -->`, triple quotes)
- Optional filters for TODO/FIXME notes or doc comments (`/**`, triple quotes)
- Returns file name, line number, cleaned comment text, and category
- Browser UI for uploading multiple files at once and copying results

## Running locally
```bash
cd apps/code-comment-extractor
dotnet run
```
Then open http://localhost:5091 and upload one or more supported files.
