# IP and Port Extractor

A C#/.NET 8 minimal API that scans text, log, config, JSON, or XML files for IPv4/IPv6 addresses with optional port numbers. It tags entries as public or private, captures timestamps when present on the line, and lets you export the findings to CSV.

## Running locally

```bash
cd apps/ip-port-extractor
DOTNET_URLS=http://localhost:5002 dotnet run
```

Then open http://localhost:5002 (or the HTTPS endpoint) to upload files or paste text, filter public/private addresses, and download the CSV.

## Features
- Supports `.txt`, `.log`, `.conf`, `.json`, and `.xml` uploads plus inline text
- Regex detection for IPv4/IPv6 with optional ports (e.g., `192.168.1.1:443` or `[2001:db8::1]:8443`)
- Optional filtering for only public or only private ranges
- Captures line numbers and timestamps when present for easier auditing
- Download results to CSV for firewall checks or ticket attachments
