# Audiobook Chapter Downloader

A .NET 8 minimal API that fetches chapter audio from LibriVox or Archive.org items and lets you download selected tracks as MP3s or merge them into a single file.

## Features
- Paste a LibriVox or Archive.org book URL and retrieve chapter names and durations
- Select specific chapters to download as a ZIP of MP3 files
- Merge selected chapters into one MP3 (requires `ffmpeg` on the server)
- Client-side UI served from `wwwroot` with simple controls for selection and download

## Running locally
```bash
cd apps/audiobook-chapter-downloader
dotnet run
```

Then open `http://localhost:5250` (or the port shown in console) and paste a supported book URL.

## Notes
- LibriVox lookups use the public API with the URL slug to locate the project and chapters.
- Archive.org items are resolved via the metadata endpoint and filter for MP3 files only.
- Merging relies on `ffmpeg -f concat` to stitch MP3s without re-encoding. Ensure `ffmpeg` is available if you need the merge option.
