# YouTube Video Downloader

A minimal ASP.NET Core webapp that lets users download their own public YouTube videos in MP4 or WebM plus audio-only (AAC/MP3) formats.

## Features
- Paste a single YouTube URL to auto-detect available streams
- Choose muxed video resolutions (144p–1080p+, MP4 or WebM)
- Audio-only downloads with selectable AAC or MP3
- Inline metadata preview and quick download buttons

> ℹ️ MP3 exports use FFmpeg. Ensure `ffmpeg` is installed and available on your system `PATH` before requesting MP3 output; AAC/M4A downloads do not require FFmpeg.

## Run locally
```bash
cd apps/youtube-video-downloader
dotnet restore
dotnet run
```

By default the app serves the UI at `http://localhost:5000` (or `https://localhost:5001`).
