# YouTube Shorts Downloader (C#)

A minimal C# webapp for downloading public YouTube Shorts (60 seconds or less) as MP4 or WebM. It automatically prefers high-resolution vertical streams and can optionally render a centered square crop for feed-friendly exports.

## Features
- Paste a Shorts URL and download the clip as MP4 or WebM
- Prefers vertical, high-resolution muxed streams
- Optional square-crop rendering via FFmpeg
- Returns basic metadata (title, duration, orientation) in response headers for the UI

## Running locally
```bash
cd apps/youtube-shorts-downloader
dotnet run
```

Then open [http://localhost:5000](http://localhost:5000) in your browser. The server downloads FFmpeg binaries on-demand for square crop mode.
