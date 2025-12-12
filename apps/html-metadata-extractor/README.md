# HTML Metadata Extractor

A lightweight C# webapp that batches title and meta tag extraction from HTML, HTM, or XML exports. Upload a handful of pages from a crawl or CMS export and get back the filename, `<title>`, meta description, and optional keywords or Open Graph fields for SEO auditing.

## Features
- Upload multiple `.html`, `.htm`, or `.xml` files at once
- Extracts filename, `<title>`, meta description, canonical URL, and language hints
- Optional keywords plus Open Graph title/description/URL/image/type columns
- CSV export and HTML table preview for quick QA
- Built with ASP.NET Core minimal APIs and HtmlAgilityPack

## Running locally
1. Install the .NET 8 SDK if you haven't already.
2. From `apps/html-metadata-extractor/`, run:
   ```bash
   dotnet run
   ```
3. Open the printed URL (for example `http://localhost:5095`) and drop your HTML files to extract metadata.

You can also access the static build at `/html-metadata-extractor` when running the Node.js host from the project root.
