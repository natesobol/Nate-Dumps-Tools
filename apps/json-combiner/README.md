# C# JSON Combiner

A minimal ASP.NET Core webapp for combining multiple JSON payloads into a single output. Arrays are concatenated, objects are deep-merged, and mixed roots are wrapped in an array. This project lives under `apps/json-combiner` to keep it isolated from the other webapps in the repository.

## Running locally

```bash
cd apps/json-combiner
dotnet run
```

Then open http://localhost:5000 (or https://localhost:5001) and upload multiple JSON files.

## Combining rules

- **Arrays → Array:** array roots are appended into one array.
- **Objects → Object:** object roots are merged; matching property names are combined recursively. If both properties are arrays they are concatenated, otherwise the most recent value wins.
- **Mixed → Array:** a mixture of root types is wrapped into a single array in the order uploaded.
- Parsing issues are returned alongside the combined result so you can see which files failed.
