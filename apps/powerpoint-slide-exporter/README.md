# PowerPoint Slide Exporter

Export each slide in a `.pptx` or `.ppt` deck as individual PNG images or formatted HTML snippets. Files are processed in-memory and bundled into a single zip for quick sharing.

## Running locally

1. Ensure LibreOffice (`soffice`) is installed and available on your `PATH`.
2. From `apps/powerpoint-slide-exporter/`, run:
   ```bash
   dotnet run
   ```
3. Open http://localhost:5000 to use the exporter UI.

## API

`POST /api/export`

- **file**: multipart/form-data file input (.ppt or .pptx)
- **mode**: `images` (default), `html`, or `both`

Returns a zip containing PNG images, HTML output, or both.
