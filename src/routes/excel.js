import express from 'express';
import multer from 'multer';
import xlsx from 'xlsx';
import path from 'path';

const router = express.Router();

const upload = multer({
  storage: multer.memoryStorage(),
  limits: {
    fileSize: 5 * 1024 * 1024
  },
  fileFilter: (req, file, cb) => {
    const allowedMimeTypes = [
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      'application/vnd.ms-excel'
    ];
    const allowedExtensions = ['.xls', '.xlsx'];
    const ext = path.extname(file.originalname).toLowerCase();
    if (allowedMimeTypes.includes(file.mimetype) || allowedExtensions.includes(ext)) {
      cb(null, true);
    } else {
      cb(new Error('Please upload a valid Excel file (.xls or .xlsx).'));
    }
  }
});

function buildMeta(req) {
  const baseUrl = `${req.protocol}://${req.get('host')}`;
  const canonicalUrl = `${baseUrl}${req.originalUrl.split('?')[0]}`;
  const metaDescription =
    'Free Excel to JSON converter webapp: upload spreadsheets, preview JSON, and download clean data for APIs or integrations.';
  const metaKeywords = 'excel to json converter, xlsx to json, spreadsheet api export, data tooling, free webapp';
  const ogTitle = 'Excel to JSON Converter | Monetize Hub';
  return {
    canonicalUrl,
    metaDescription,
    metaKeywords,
    ogTitle,
    ogDescription: metaDescription,
    structuredData: JSON.stringify(
      {
        '@context': 'https://schema.org',
        '@type': 'WebApplication',
        name: 'Excel to JSON Converter',
        applicationCategory: 'UtilitiesApplication',
        operatingSystem: 'Any',
        offers: { '@type': 'Offer', price: '0', priceCurrency: 'USD' },
        description: metaDescription,
        url: canonicalUrl
      },
      null,
      2
    )
  };
}

function renderConverter(req, res, extras = {}) {
  const meta = buildMeta(req);
  res.render('excel-to-json', {
    title: 'Excel to JSON Converter',
    ...meta,
    ...extras
  });
}

router.get('/excel-to-json', (req, res) => {
  renderConverter(req, res);
});

router.post('/excel-to-json', (req, res, next) => {
  upload.single('excelFile')(req, res, err => {
    if (err) {
      return renderConverter(req, res, { error: err.message });
    }
    next();
  });
});

router.post('/excel-to-json', (req, res) => {
  if (!req.file) {
    return renderConverter(req, res, { error: 'Please select an Excel file to convert.' });
  }

  try {
    const workbook = xlsx.read(req.file.buffer, { type: 'buffer' });
    const parsedSheets = {};

    workbook.SheetNames.forEach(sheetName => {
      const worksheet = workbook.Sheets[sheetName];
      const rows = xlsx.utils.sheet_to_json(worksheet, { defval: null, raw: false });
      parsedSheets[sheetName] = rows;
    });

    const jsonString = JSON.stringify(parsedSheets, null, 2);
    const baseName = path.parse(req.file.originalname).name || 'excel-data';
    const fileName = `${baseName}-data.json`;

    renderConverter(req, res, {
      result: {
        fileName,
        jsonString,
        downloadPayload: Buffer.from(jsonString).toString('base64'),
        sheetCount: workbook.SheetNames.length
      }
    });
  } catch (error) {
    renderConverter(req, res, {
      error:
        'We could not parse that file. Please make sure it is a valid Excel (.xls or .xlsx) spreadsheet with tabular data.'
    });
  }
});

router.post('/excel-to-json/download', (req, res) => {
  const { payload, fileName } = req.body;
  if (!payload) {
    return res.redirect('/excel-to-json');
  }

  const buffer = Buffer.from(payload, 'base64');
  res.setHeader('Content-Type', 'application/json');
  res.setHeader('Content-Disposition', `attachment; filename="${fileName || 'excel-data.json'}"`);
  return res.send(buffer);
});

export default router;
