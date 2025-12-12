# Email Thread Extractor

A .NET 8 minimal API that accepts `.eml`, `.msg`, or `.mbox` uploads, extracts sender/recipient metadata, threaded replies, and attachment details, then returns CSV metadata plus concatenated body text exports.

## Running locally

```bash
cd apps/email-thread-extractor
 dotnet restore
 dotnet run
```

The app defaults to `http://localhost:5000`. Visit the root URL in your browser to use the UI, or POST files to `/api/extract` with `multipart/form-data`.

### API shape

- **POST** `/api/extract`
  - Form fields: `includeAttachments` (`true|false`) and one or more files (accepts `.eml`, `.msg`, `.mbox`).
  - Response: JSON with per-file messages, attachment metadata, CSV export text, and body export text.

- **GET** `/health`: lightweight readiness check.

### Notes

- Attachments are kept in memory only; set `includeAttachments=true` to return base64 payloads you can download from the UI.
- MBOX files return one message record per entry inside the mailbox.
