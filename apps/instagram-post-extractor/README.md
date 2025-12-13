# Instagram Post Extractor

A minimal .NET 8 webapp that scrapes a public Instagram profile page for post URLs. It can list the links in chronological order and optionally pull down each post's caption so you can copy it.

## Running locally

```bash
cd apps/instagram-post-extractor
(dotnet restore)
dotnet run
```

Visit `http://localhost:5000` (or the port printed by the CLI) to use the UI.

## API

`POST /api/extract`

```json
{
  "profileUrl": "https://www.instagram.com/instagram/",
  "includeDescriptions": true,
  "newestFirst": true
}
```

Returns:

```json
{
  "count": 10,
  "posts": [
    {
      "url": "https://www.instagram.com/p/ABC123/",
      "description": "Photo caption text"
    }
  ]
}
```

Only public profiles are supported; private or blocked accounts will return zero results.
