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

export default router;
