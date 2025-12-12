# Image Caption & Alt Text Extractor

Upload `.html`, `.md`, `.docx`, or `.pdf` files to collect every image reference alongside its caption and alt text. The tool parses HTML `<figure>` and `<img>` tags, Markdown image syntax, Word image metadata, and common PDF accessibility labels. Results can be filtered to exclude blank entries and exported to CSV, TXT, or previewed HTML for quick audits.

## Running locally

```bash
dotnet run --project image-caption-alt-extractor.csproj
```

Open the printed URL (defaults to `http://localhost:5000`) to use the drag-and-drop UI hosted from `wwwroot`.
