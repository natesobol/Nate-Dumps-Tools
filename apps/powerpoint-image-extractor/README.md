# PowerPoint Image Extractor

A lightweight .NET 8 minimal API that unpacks every embedded image from a `.pptx` presentation and returns them as a zipped download. The UI ships with a modern dark theme so educators, designers, and content teams can quickly repurpose visuals without manual copy/paste.

## Features
- Validates `.pptx` uploads up to 50 MB
- Filters files from the `ppt/media` directory and keeps common image formats (PNG, JPG, GIF, BMP, TIFF, WEBP)
- Packages images into a single ZIP with duplicate-safe filenames
- Friendly error responses for empty uploads, invalid PowerPoints, or decks without images

## Getting started
1. Navigate into the app directory:
   ```bash
   cd apps/powerpoint-image-extractor
   ```
2. Run the API and static site:
   ```bash
   dotnet run
   ```
3. Open the UI at http://localhost:5249 (or the port shown in the console) and upload a `.pptx`.

## API
`POST /api/extract`
- **Body:** `multipart/form-data` with a `file` field containing a `.pptx`
- **Success:** `application/zip` stream with all embedded images
- **Errors:** JSON payload with an `error` message describing what went wrong

## Project layout
- `Program.cs` — minimal API handling validation and ZIP packaging
- `wwwroot/index.html` — landing page, upload form, and fetch logic
- `powerpoint-image-extractor.csproj` — .NET project settings
