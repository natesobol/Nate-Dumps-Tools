# PDF Splitter

This C#-hosted, browser-first webapp splits one or more PDF documents into smaller PDFs without sending data to a server. It is inspired by Sobolsoft's PDF Split Multiple Files Software and is aimed at users who frequently ask how to extract or split pages.

## Features
- Upload a single PDF or batch multiple PDFs for splitting
- Choose exact page ranges, per-file page counts, or approximate size estimates
- Runs entirely in the browser for fast, privacy-preserving processing
- Download split parts as individual files

## Running locally
1. Install the .NET 8 SDK if you don't already have it
2. From `apps/pdf-splitter/`, run `dotnet run`
3. Open the URL printed by the app (for example `http://localhost:5183`) to load the splitter UI

You can also access the static build through the Node.js host at `/pdf-splitter` once `npm run dev` is running from the project root.
