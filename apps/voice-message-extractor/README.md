# Voice Message Extractor (WhatsApp/Telegram)

A minimal ASP.NET Core webapp that extracts and converts voice notes from exported WhatsApp or Telegram chats. Upload ZIP archives of chat exports containing `.opus` (WhatsApp) or `.ogg` (Telegram) audio messages, list them with timestamps, and batch download them as MP3 or WAV alongside a metadata CSV.

## Running locally

```bash
cd apps/voice-message-extractor
dotnet run
```

Then open `http://localhost:5000` (or the port shown in the console) to use the UI.

## Features
- Upload chat export ZIPs with WhatsApp `.opus` or Telegram `.ogg` files
- Preview all detected voice messages and their timestamps
- Convert to MP3 or WAV (requires FFmpeg, downloaded automatically on first run)
- Batch download converted audio plus `metadata.csv` summarizing filenames, original paths, timestamps, and durations

## Notes
- Only `.zip`, `.opus`, and `.ogg` uploads are processed. Other files are ignored with a warning.
- FFmpeg binaries are downloaded to a temporary folder on demand using `Xabe.FFmpeg.Downloader`.
- Temporary extraction folders are cleaned up after each request.
