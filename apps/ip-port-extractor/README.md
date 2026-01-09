# IP & Port Extractor

A C# minimal API webapp that scans log or config files for IPv4/IPv6 addresses with optional ports, making it easier to audit exposed endpoints before shipping configs or firewall rules.

## Features
- Accepts `.txt`, `.log`, `.conf`, `.json`, and `.xml` files
- Extracts IPv4/IPv6 addresses with optional ports (e.g., `192.168.1.1:443`, `[2001:db8::1]:8443`)
- Identifies whether each address is private/reserved (including CGNAT and benchmarking ranges) or public
- Optional filtering for public-only or private-only results
- Captures line numbers plus the first detected timestamp on each line when present
- Export matches to CSV directly from the browser UI
- Suitable for network audits, firewall rule reviews, DevOps config checks, and incident investigations

## Running locally
```bash
cd apps/ip-port-extractor
dotnet run
```

Then open the indicated localhost port (e.g., `http://localhost:5085`) to use the UI.
