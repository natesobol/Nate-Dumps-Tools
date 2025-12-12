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

export default router;
