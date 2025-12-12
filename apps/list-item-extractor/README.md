# List Item Extractor

A minimal ASP.NET Core web app that extracts bulleted and numbered list items from `.docx`, `.pdf`, `.txt`, and `.md` files.
The API returns structured list items with nesting level and the nearest parent heading.

## Running locally

```bash
dotnet run --project apps/list-item-extractor/list-item-extractor.csproj
```

Then open `http://localhost:5000` and upload your files.
