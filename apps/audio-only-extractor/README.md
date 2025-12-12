# Audio-Only Extractor

A .NET 8 minimal API that downloads video audio tracks from YouTube, Vimeo, or TikTok links. Choose MP3, WAV, or AAC, optionally normalize loudness, and trim intro/outro segments before downloading.

## Prerequisites
- .NET 8 SDK
- [`yt-dlp`](https://github.com/yt-dlp/yt-dlp) available on the server PATH
- [`ffmpeg`](https://ffmpeg.org/) available on the server PATH

## Run locally
```bash
cd apps/audio-only-extractor
DOTNET_URLS=http://localhost:5180 dotnet run
```

Visit `http://localhost:5180` and paste a video link to start extraction.

## API
`POST /api/extract-audio`

JSON body:
```json
{
  "url": "https://www.youtube.com/watch?v=...",
  "format": "mp3 | wav | aac",
  "normalizeLevels": true,
  "trimStartSeconds": 2.5,
  "trimEndSeconds": 120
}
```

Responses:
- `200 OK` with the transcoded audio file stream
- `400 Bad Request` with `{ "error": "..." }` if validation, download, or transcoding fails

The API validates supported platforms, enforces non-negative trim values, and checks for `yt-dlp`/`ffmpeg` availability before processing.
