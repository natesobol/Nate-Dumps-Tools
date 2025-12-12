import express from 'express';

const router = express.Router();

router.get('/', (req, res) => {
  res.render('home', { title: 'Home' });
});

router.get('/about', (req, res) => {
  res.render('about', { title: 'About' });
});

router.get('/xml-json-translator', (req, res) => {
  res.render('xml-json-translator', {
    title: 'XML/JSON Translator',
    metaDescription:
      'Convert XML snippets to JSON or reverse the direction instantly in your browser with a drag-and-drop friendly utility.',
    metaKeywords: 'xml to json converter,json to xml translator,browser xml json utility'
  });
});

router.get('/yaml-json-converter', (req, res) => {
  res.render('yaml-json-converter', {
    title: 'YAML/JSON Converter',
    metaDescription:
      'Convert YAML configuration to JSON or reverse JSON into YAML instantly. Paste text, pick a direction, and copy cleanly formatted results.',
    metaKeywords: 'yaml to json converter,json to yaml converter,config translator'
  });
});

router.get('/json-key-flattener', (req, res) => {
  res.render('json-key-flattener', {
    title: 'JSON Key Flattener',
    metaDescription:
      'Upload or paste nested JSON and instantly flatten or unflatten keys using dot notation and array-aware paths—perfect for CSV exports or API payloads.',
    metaKeywords: 'flatten json,unflatten json,json dot notation converter'
  });
});

router.get('/html-metadata-extractor', (req, res) => {
  res.render('html-metadata-extractor', {
    title: 'HTML Metadata Extractor',
    metaDescription:
      'Upload HTML, HTM, or XML files to extract filenames, page titles, meta descriptions, keywords, canonicals, and Open Graph tags in bulk.',
    metaKeywords: 'html metadata extractor,meta description audit,open graph scraper,title tag checker'
  });
});

router.get('/html-tag-cleaner', (req, res) => {
  res.render('html-tag-cleaner', {
    title: 'HTML Tag Cleaner',
    metaDescription:
      'Strip HTML tags or keep only allowlisted elements like <p>, <a>, and <strong>. Clean up scraped pages, CMS pastes, and Word exports.',
    metaKeywords: 'html cleaner,strip html tags,allowlist html tags'
  });
});

router.get('/tabify-untabify', (req, res) => {
  res.render('tabify-untabify', {
    title: 'Tabify or Untabify',
    metaDescription:
      'Convert tabs to spaces or spaces to tabs with configurable spacing, trailing whitespace cleanup, and a preview for quick QA.',
    metaKeywords: 'tabs to spaces,spaces to tabs,indentation converter'
  });
});

router.get('/email-thread-extractor', (req, res) => {
  res.render('email-thread-extractor', {
    title: 'Email Thread Extractor',
    metaDescription:
      'Upload .eml, .msg, or .mbox files to extract sender, recipient, timestamp, attachment, and body text details from threaded replies.',
    metaKeywords: 'eml to csv,email thread extractor,msg parser,mbox parser,email metadata export'
  });
});

router.get('/extract-text-inside-quotes', (req, res) => {
  res.render('extract-text-inside-quotes', {
    title: 'Extract Text Inside Quotes',
    metaDescription:
      'Paste or upload DOCX, emails, code, or blog posts and instantly pull phrases inside single or double quotes with optional deduplication.',
    metaKeywords: 'quote extractor,text inside quotes,single and double quotes parser'
  });
});

router.get('/number-extractor', (req, res) => {
  res.render('number-extractor', {
    title: 'Extract All Numbers From Text, Word, Excel, and CSV Files',
    metaDescription:
      'Upload TXT, DOCX, CSV, or XLSX files or paste text to pull every number with options for decimals, currency symbols, and ignoring numbers inside words.',
    metaKeywords: 'extract numbers from docx,excel number extractor,csv number scraper,financial audit number finder'
  });
});

router.get('/hashtag-mention-extractor', (req, res) => {
  res.render('hashtag-mention-extractor', {
    title: 'Extract Hashtags and @Mentions from Social Media Files',
    metaDescription:
      'Upload CSV, TXT, or JSON exports to find every hashtag and @mention with frequency counts and exportable lists.',
    metaKeywords: 'hashtag extractor,mention finder,social media hashtags,csv hashtag counter,json mention extractor'
  });
});

router.get('/highlighted-text-extractor', (req, res) => {
  res.render('highlighted-text-extractor', {
    title: 'Extract Highlighted Text from Word, PDF, and PowerPoint Files',
    metaDescription:
      'Upload DOCX, PDF, or PPTX files to pull every highlighted or background-colored snippet with optional color tagging for reviewer notes.',
    metaKeywords: 'highlight extractor docx,pdf highlight reader,powerpoint highlighted text,annotated text puller'
  });
});

router.get('/color-extractor', (req, res) => {
  res.render('color-extractor', {
    title: 'Extract Palette Colors from CSS, HTML, SVG, and Figma JSON',
    metaDescription:
      'Upload CSS, HTML, SVG, or Figma JSON exports to pull every hex, rgb(), rgba(), or named color with usage counts and a palette preview.',
    metaKeywords: 'color extractor,css palette finder,figma color audit,svg color parser'
  });
});

router.get('/excel-highlighted-row-extractor', (req, res) => {
  res.render('excel-highlighted-row-extractor', {
    title: 'Extract Highlighted or Color-Coded Rows from Excel',
    metaDescription:
      'Upload .xlsx or .xlsm files and export only the highlighted rows. Filter by fill color, keep original formatting, and download as Excel or CSV.',
    metaKeywords: 'excel highlighted row extractor,color filter excel,conditional formatting export,excel row highlighter'
router.get('/domain-name-extractor', (req, res) => {
  res.render('domain-name-extractor', {
    title: 'Extract Domain Names and URLs From Files',
    metaDescription:
      'Upload TXT, DOCX, CSV, JSON, or HTML files—or paste text—to find every domain and URL with root-only filters, subdomain exclusion, and grouping.',
    metaKeywords: 'domain extractor,url finder,root domain parser,brand monitoring urls'
  });
});

export default router;
