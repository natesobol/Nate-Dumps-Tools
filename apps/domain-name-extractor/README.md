# Domain Name Extractor

A lightweight C# web app that pulls domains and URLs out of text, DOCX, CSV, JSON, and HTML files. Filters let you focus on root domains, skip subdomains, and group results with frequency counts. Export everything—including the original context lines—to CSV for brand monitoring or link QA.

## Running locally

```bash
cd apps/domain-name-extractor
DOTNET_URLS=http://0.0.0.0:5010 dotnet run
```

Then open http://localhost:5010.
