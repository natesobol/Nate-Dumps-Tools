# Migration Guide - Organized Webapp Structure

## Changes Made

Your webapps have been reorganized into well-named folders under `/apps/`:

### New Structure

```
apps/
├── excel-to-json/           # Excel to JSON Converter (Node.js/Express)
│   ├── src/
│   │   └── routes/
│   │       └── excel.js     # Express routes
│   ├── views/
│   │   └── excel-to-json.ejs
│   ├── index.html           # Static GitHub Pages version
│   └── README.md
├── yaml-json-converter/     # YAML ↔ JSON Converter (static)
│   └── index.html
├── xml-json-translator/     # XML ↔ JSON Translator (static)
│   └── index.html
│
└── json-combiner/           # JSON Combiner (C# .NET)
    ├── wwwroot/
    │   ├── index.html
    │   └── styles.css
    ├── Program.cs
    ├── *.csproj
    └── README.md
```

## Files Updated

### Server Configuration
- ✅ `server.js` - Updated to import Excel routes from new location
- ✅ `server.js` - Added apps/excel-to-json/views to view paths array

### HTML Files
- ✅ `index.html` - Updated webapp links to point to new locations
- ✅ `about.html` - Updated webapp links and converter buttons
- ✅ `login.html` - Updated webapp navigation dropdown
- ✅ `register.html` - Updated webapp navigation dropdown
- ✅ `admin.html` - Updated webapp navigation dropdown
- ✅ `excel-to-json.html` - Converted to redirect page (for backwards compatibility)

### EJS Templates
- ✅ `apps/excel-to-json/views/excel-to-json.ejs` - Updated partial includes to use relative paths
- ⚠️ `views/partials/page-top.ejs` - No changes needed (uses server routes like `/excel-to-json`)

### Documentation
- ✅ `README.md` - Updated with new webapp structure and organization
- ✅ `apps/excel-to-json/README.md` - Created comprehensive documentation

## Old Files to Clean Up

You can safely delete these old files once you've verified everything works:

```bash
# Old Excel to JSON files (now in apps/excel-to-json/)
rm /workspaces/Nates-Free-Tools/src/routes/excel.js
rm /workspaces/Nates-Free-Tools/views/excel-to-json.ejs
```

**Note:** Keep `/workspaces/Nates-Free-Tools/excel-to-json.html` as it now serves as a redirect for backwards compatibility.

## How It Works Now

### Excel to JSON Webapp

**Server Routes (Express):**
- Routes are loaded from: `apps/excel-to-json/src/routes/excel.js`
- Views are in: `apps/excel-to-json/views/`
- Server still serves the same endpoints: `/excel-to-json`, `/excel-to-json/download`

**Static Version (GitHub Pages):**
- Standalone HTML at: `apps/excel-to-json/index.html`
- All links updated to use relative paths (`../../public/css/styles.css`)
- Fully functional client-side processing

### JSON Combiner Webapp

**Location:** `apps/json-combiner/`
- Already was in apps folder, now properly documented
- C# .NET minimal API
- Standalone webapp with its own wwwroot

## Testing Checklist

- [ ] Run `npm run dev` to start the server
- [ ] Navigate to `/excel-to-json` - should work normally
- [ ] Upload an Excel file and test conversion
- [ ] Check all HTML navigation links point to correct locations
- [ ] Verify static HTML files load with correct CSS paths
- [ ] Test the old `excel-to-json.html` redirects properly

## Benefits of This Organization

1. **Clear Separation** - Each webapp has its own folder with all related files
2. **Scalability** - Easy to add new webapps following the same pattern
3. **Maintainability** - Routes, views, and static files are colocated
4. **Documentation** - Each app has its own README
5. **Multiple Technologies** - Supports different tech stacks (Node.js, C#, etc.)
6. **Backwards Compatibility** - Old links redirect automatically
