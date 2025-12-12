# HTML Tag Cleaner

A lightweight C# minimal API that strips HTML tags or keeps only the ones you trust. Paste HTML or upload files, choose an allowlist (e.g., `<p>`, `<a>`, `<strong>`), and receive cleaned content ready for publishing, prompting, or downstream automation.

## Running locally

```
dotnet run --project html-tag-cleaner.csproj
```

Open `http://localhost:5000` (or the port shown in console) to use the drag-and-drop UI hosted from `wwwroot`.

## API

`POST /api/clean`

- **Form fields**
  - `html` (string, optional): Raw HTML text.
  - `file` (file, optional): One or more uploads such as `.html`, `.htm`, `.txt`, `.xml`, or `.md`.
  - `allowedTags` (string, optional): Comma- or space-separated tags to keep. Leave empty to strip everything.
  - `collapseWhitespace` (bool, optional): Defaults to `true`; normalizes whitespace in the cleaned output.

### Example request

```bash
curl -X POST \
  -F "html=<p>Hello <em>world</em></p>" \
  -F "allowedTags=p,em" \
  http://localhost:5000/api/clean
```

### Example response

```json
{
  "mode": "allowlist",
  "allowed": ["p", "em"],
  "collapseWhitespace": true,
  "results": [
    {
      "source": "text",
      "lengthBefore": 25,
      "lengthAfter": 29,
      "cleaned": "<p>Hello emworldem</p>",
      "allowedTags": ["p", "em"]
    }
  ]
}
```
