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

export default router;
