# List Comparison / Diff Checker

This C#-hosted webapp compares two lists. Paste content or upload a mix of text, HTML, code, or Word documents to quickly see overlaps and unique entries.

## Features
- Paste text or upload files (TXT, CSV, JSON, HTML/HTM, DOCX, source files) for each list
- Toggle case sensitivity, trimming, and skipping blank lines
- See overlaps plus items unique to List A or List B with quick counts
- Lightweight API served from a .NET 8 minimal app with static UI

## Running locally
1. Install the .NET 8 SDK
2. From `apps/list-comparison/`, run `dotnet run`
3. Open the printed URL (for example `http://localhost:5183`) to load the comparison UI

You can also access the static build through the Node.js host at `/list-comparison` once `npm run dev` is running from the project root.
