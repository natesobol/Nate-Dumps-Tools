# TikTok Video Downloader (C#)

A lightweight ASP.NET Core Razor Pages app for downloading TikTok videos through a configurable API endpoint. It supports requesting the original video or watermark-free versions (depending on the provider) and lets you choose 720p or 1080p output where available.

## Configuration
Update `appsettings.json` with your provider details:

- `TikTokDownloader:ApiEndpoint` — POST endpoint that accepts `{ url, resolution, removeWatermark }` and returns JSON with `downloadUrl` and optional `fileName`.
- `TikTokDownloader:ApiKey` — API key header value (sent as `X-API-Key`).
- `TikTokDownloader:DefaultFileName` — used when the API does not return a file name.

## Running locally
Ensure you have the .NET 8 SDK installed, then from this folder run:

```bash
 dotnet restore
 dotnet run
```

Navigate to `https://localhost:5001` and paste a TikTok video URL to download it as an MP4. Use only with content you have rights to download.
