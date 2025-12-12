# PowerPoint → PDF converter

A server-backed tool that uses LibreOffice to convert `.ppt` and `.pptx` uploads into PDFs. Files stay in memory and are not written to disk.

## Running locally

1. Install LibreOffice so the `soffice` binary is on your `PATH`.
2. Install dependencies: `npm install`.
3. Start the Node server: `npm run dev`.
4. Open `http://localhost:3000/powerpoint-to-pdf` to use the converter UI.

If LibreOffice is missing, the API responds with a friendly error instead of a generic failure.
