import express from 'express';
import session from 'express-session';
import SQLiteStoreFactory from 'connect-sqlite3';
import path from 'path';
import dotenv from 'dotenv';
import helmet from 'helmet';
import morgan from 'morgan';
import { fileURLToPath } from 'url';
import authRoutes from './src/routes/auth.js';
import pageRoutes from './src/routes/pages.js';
import adminRoutes from './src/routes/admin.js';
import excelRoutes from './apps/excel-to-json/src/routes/excel.js';
import csvXmlRoutes from './apps/csv-xml-converter/src/routes/csv-xml.js';
import powerpointRoutes from './apps/powerpoint-to-pdf/src/routes/powerpoint.js';
import { attachSupabase } from './src/middleware/auth.js';
import htmlCleanerRoutes from './src/routes/htmlCleaner.js';
import multer from 'multer';

dotenv.config();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const SQLiteStore = SQLiteStoreFactory(session);
const upload = multer();

function createDebugPayload(error, req, fallbackMessage = 'Unexpected error') {
  const errObj = error instanceof Error ? error : new Error(fallbackMessage);
  return {
    error: errObj.message,
    stack: errObj.stack,
    path: req?.originalUrl,
    method: req?.method,
    timestamp: new Date().toISOString()
  };
}

app.set('view engine', 'ejs');
app.set('views', [
  path.join(__dirname, 'views'),
  path.join(__dirname, 'webapps/webapp-views-htmls'),
  path.join(__dirname, 'apps/excel-to-json/views'),
  path.join(__dirname, 'apps/csv-xml-converter/views')
]);

app.use(helmet());
app.use(express.urlencoded({ extended: true }));
app.use(express.json({ limit: '10mb' }));
app.use(express.static(path.join(__dirname, 'public')));
app.use('/public', express.static(path.join(__dirname, 'public')));

// App routes - clean URLs
app.use('/batch-file-renamer', express.static(path.join(__dirname, 'apps/batch-file-renamer/wwwroot')));
app.use('/batch-pdf-text-extractor', express.static(path.join(__dirname, 'apps/batch-pdf-text-extractor/wwwroot')));
app.use('/bullet-list-extractor', express.static(path.join(__dirname, 'apps/bullet-list-extractor/wwwroot')));
app.use('/capitalized-phrase-extractor', express.static(path.join(__dirname, 'apps/capitalized-phrase-extractor/wwwroot')));
app.use('/color-extractor', express.static(path.join(__dirname, 'apps/color-extractor/wwwroot')));
app.use('/domain-name-extractor', express.static(path.join(__dirname, 'apps/domain-name-extractor/wwwroot')));
app.use('/csv-xml-converter', express.static(path.join(__dirname, 'apps/csv-xml-converter')));
app.use('/currency-percent-sentence-extractor', express.static(path.join(__dirname, 'apps/currency-percent-sentence-extractor/wwwroot')));
app.use('/email-signature-extractor', express.static(path.join(__dirname, 'apps/email-signature-extractor/wwwroot')));
app.use('/email-thread-extractor', express.static(path.join(__dirname, 'apps/email-thread-extractor/wwwroot')));
app.use('/excel-to-json', express.static(path.join(__dirname, 'apps/excel-to-json')));
app.use('/extract-text-inside-quotes', express.static(path.join(__dirname, 'apps/extract-text-inside-quotes/wwwroot')));
app.use('/file-path-extractor', express.static(path.join(__dirname, 'apps/file-path-extractor/wwwroot')));
app.use('/code-comment-extractor', express.static(path.join(__dirname, 'apps/code-comment-extractor/wwwroot')));
app.use('/find-and-replace', express.static(path.join(__dirname, 'apps/find-and-replace/wwwroot')));
app.use('/hashtag-mention-extractor', express.static(path.join(__dirname, 'apps/hashtag-mention-extractor/wwwroot')));
app.use('/highlighted-text-extractor', express.static(path.join(__dirname, 'apps/highlighted-text-extractor')));
app.use('/html-metadata-extractor', express.static(path.join(__dirname, 'apps/html-metadata-extractor/wwwroot')));
app.use('/html-tag-cleaner', express.static(path.join(__dirname, 'apps/html-tag-cleaner/wwwroot')));
app.use('/image-path-extractor', express.static(path.join(__dirname, 'apps/image-path-extractor/wwwroot')));
app.use('/json-combiner', express.static(path.join(__dirname, 'apps/json-combiner/wwwroot')));
app.use('/json-to-excel', express.static(path.join(__dirname, 'apps/json-to-excel/wwwroot')));
app.use('/list-comparison', express.static(path.join(__dirname, 'apps/list-comparison/wwwroot')));
app.use('/multi-csv-column-merger', express.static(path.join(__dirname, 'apps/multi-csv-column-merger/wwwroot')));
app.use('/named-entity-extractor', express.static(path.join(__dirname, 'apps/named-entity-extractor/wwwroot')));
app.use('/number-extractor', express.static(path.join(__dirname, 'apps/number-extractor/wwwroot')));
app.use('/number-row-extractor', express.static(path.join(__dirname, 'apps/number-row-extractor/wwwroot')));
app.use('/pattern-text-extractor', express.static(path.join(__dirname, 'apps/pattern-text-extractor/wwwroot')));
app.use('/pdf-link-extractor', express.static(path.join(__dirname, 'apps/pdf-link-extractor/wwwroot')));
app.use('/pdf-splitter', express.static(path.join(__dirname, 'apps/pdf-splitter/wwwroot')));
app.use('/phone-number-extractor', express.static(path.join(__dirname, 'apps/phone-number-extractor/wwwroot')));
app.use(
  '/podcast-episode-downloader',
  express.static(path.join(__dirname, 'apps/podcast-episode-downloader/wwwroot'))
);
app.use('/powerpoint-image-extractor', express.static(path.join(__dirname, 'apps/powerpoint-image-extractor/wwwroot')));
app.use('/powerpoint-slide-exporter', express.static(path.join(__dirname, 'apps/powerpoint-slide-exporter/wwwroot')));
app.use('/powerpoint-to-pdf', express.static(path.join(__dirname, 'apps/powerpoint-to-pdf/wwwroot')));
app.use('/product-sku-extractor', express.static(path.join(__dirname, 'apps/product-sku-extractor/wwwroot')));
app.use('/question-extractor', express.static(path.join(__dirname, 'apps/question-extractor/wwwroot')));
app.use('/list-item-extractor', express.static(path.join(__dirname, 'apps/list-item-extractor/wwwroot')));
app.use('/repetitive-phrase-extractor', express.static(path.join(__dirname, 'apps/repetitive-phrase-extractor/wwwroot')));
app.use('/resume-contact-extractor', express.static(path.join(__dirname, 'apps/resume-contact-extractor/wwwroot')));
app.use('/sentence-keyword-extractor', express.static(path.join(__dirname, 'apps/sentence-keyword-extractor/wwwroot')));
app.use('/tabify-untabify', express.static(path.join(__dirname, 'apps/tabify-untabify/wwwroot')));
app.use('/table-data-extractor', express.static(path.join(__dirname, 'apps/table-data-extractor/wwwroot')));
app.use(
  '/excel-highlighted-row-extractor',
  express.static(path.join(__dirname, 'apps/excel-highlighted-row-extractor/wwwroot'))
);
app.use('/table-of-contents-extractor', express.static(path.join(__dirname, 'apps/table-of-contents-extractor/wwwroot')));
app.use('/text-url-extractor', express.static(path.join(__dirname, 'apps/text-url-extractor/wwwroot')));
app.use('/timestamp-extractor', express.static(path.join(__dirname, 'apps/timestamp-extractor/wwwroot')));
app.use('/xml-json-translator', express.static(path.join(__dirname, 'apps/xml-json-translator')));
app.use('/yaml-json-converter', express.static(path.join(__dirname, 'apps/yaml-json-converter')));
app.use('/youtube-playlist-downloader', express.static(path.join(__dirname, 'apps/youtube-playlist-downloader/wwwroot')));
app.use(morgan('dev'));

app.use(
  session({
    store: new SQLiteStore({
      db: process.env.SESSION_DB || 'sessions.db',
      dir: path.join(__dirname, 'data')
    }),
    secret: process.env.SESSION_SECRET || 'change-me-please',
    resave: false,
    saveUninitialized: false,
    cookie: {
      httpOnly: true,
      secure: process.env.NODE_ENV === 'production',
      sameSite: 'lax',
      maxAge: 1000 * 60 * 60 * 24 * 7
    }
  })
);

app.use(attachSupabase);

app.use('/', pageRoutes);
app.use('/', authRoutes);
app.use('/', adminRoutes);
app.use('/', htmlCleanerRoutes);
app.use('/', excelRoutes);
app.use('/', csvXmlRoutes);
app.use('/', powerpointRoutes);

// Stub APIs for tools that do not yet have a Node backend so the front-end
// can surface clear JSON errors instead of HTML 404 pages.
const unsupportedApiEndpoints = [
  '/api/download-options',
  '/api/extract-audio',
  '/api/chapters',
  '/api/rename',
  '/api/extract',
  '/api/extract-lists',
  '/api/extract-colors',
  '/api/extract-currency-sentences',
  '/api/extract-highlighted-rows',
  '/api/scan',
  '/api/replace',
  '/api/clean',
  '/api/json-to-excel',
  '/api/compare',
  '/api/list-item-extractor',
  '/api/merge',
  '/api/filter',
  '/api/fetch-episodes',
  '/api/export',
  '/api/extract-questions',
  '/api/analyze',
  '/api/extract-sentences',
  '/api/convert',
  '/api/events',
  '/api/export-ics',
  '/api/playlist/download',
  '/api/playlist/info',
  '/api/download',
  '/api/streams'
];

unsupportedApiEndpoints.forEach((endpoint) => {
  // Avoid overriding any real handler that might be added later.
  app.all(endpoint, (req, res, next) => {
    if (res.headersSent) return next();
    const debug = createDebugPayload(new Error('Endpoint not yet implemented'), req);
    res.status(501).json({
      error: 'This API endpoint is not yet implemented on the unified server.',
      endpoint,
      debug
    });
  });
});

// JSON combiner API for the /json-combiner webapp
app.post('/api/combine', upload.array('files'), (req, res, next) => {
  try {
    const files = req.files || [];

    if (!files.length) {
      return res.status(400).json({ error: 'No files were uploaded.', debug: createDebugPayload(null, req, 'No files were uploaded') });
    }

    const parsedNodes = [];
    const parseErrors = [];

    for (const file of files) {
      if (!file.buffer?.length) {
        parseErrors.push({ file: file.originalname, message: 'File was empty.' });
        continue;
      }

      try {
        const content = file.buffer.toString('utf8');
        const jsonNode = JSON.parse(content);
        parsedNodes.push(jsonNode);
      } catch (error) {
        parseErrors.push({
          file: file.originalname,
          message: error instanceof Error ? error.message : 'Invalid JSON payload.',
          stack: error instanceof Error ? error.stack : undefined
        });
      }
    }

    if (!parsedNodes.length) {
      return res.status(400).json({ error: 'Unable to parse any JSON payloads.', details: parseErrors, debug: createDebugPayload(null, req, 'All JSON payloads failed to parse') });
    }

    const combined = combineNodes(parsedNodes);
    const combinedType = Array.isArray(combined)
      ? 'array'
      : combined && typeof combined === 'object'
        ? 'object'
        : 'mixed';

    res.json({ combinedType, combined, parseErrors, debug: createDebugPayload(null, req, 'Combine request diagnostics') });
  } catch (error) {
    next(error);
  }
});

// Return JSON for unknown API endpoints so the front-end can surface useful errors
app.use('/api', (req, res) => {
  res.status(404).json({
    error: 'API endpoint not found',
    path: req.originalUrl,
    debug: createDebugPayload(new Error('API endpoint not found'), req)
  });
});

app.use((err, req, res, next) => {
  console.error('Unhandled request error', {
    path: req.originalUrl,
    method: req.method,
    message: err?.message,
    stack: err?.stack
  });

  if (req.originalUrl.startsWith('/api')) {
    return res.status(err.status || 500).json({
      error: err.message || 'Internal server error',
      stack: err.stack,
      path: req.originalUrl,
      method: req.method,
      timestamp: new Date().toISOString()
    });
  }

  res.status(500);
  res.set('Content-Type', 'text/plain');
  res.send(`An unexpected error occurred while processing your request.\n\n${err.stack || err.message || err}`);
});

app.use((req, res) => {
  res.status(404).render('404', { title: 'Not found' });
});

const port = process.env.PORT || 3000;
app.listen(port, () => {
  console.log(`Server listening on http://localhost:${port}`);
});

function combineNodes(nodes) {
  const allArrays = nodes.every((node) => Array.isArray(node));
  const allObjects = nodes.every((node) => node && typeof node === 'object' && !Array.isArray(node));

  if (allArrays) {
    return nodes.flatMap((node) => node);
  }

  if (allObjects) {
    return nodes.reduce((acc, obj) => deepMerge(acc, obj), {});
  }

  return nodes.map((node) => cloneValue(node));
}

function deepMerge(target, source) {
  for (const [key, value] of Object.entries(source)) {
    if (Array.isArray(value)) {
      target[key] = Array.isArray(target[key]) ? [...target[key], ...value] : cloneValue(value);
    } else if (value && typeof value === 'object') {
      target[key] = deepMerge(target[key] && typeof target[key] === 'object' ? target[key] : {}, value);
    } else {
      target[key] = value;
    }
  }
  return target;
}

function cloneValue(value) {
  if (Array.isArray(value)) {
    return value.map((item) => cloneValue(item));
  }

  if (value && typeof value === 'object') {
    return deepMerge({}, value);
  }

  return value;
}
