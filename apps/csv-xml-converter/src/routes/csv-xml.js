import express from 'express';
import multer from 'multer';
import path from 'path';
import { parse as parseCsv } from 'csv-parse/sync';
import { stringify as stringifyCsv } from 'csv-stringify/sync';
import { Builder, Parser } from 'xml2js';

const router = express.Router();

const upload = multer({
  storage: multer.memoryStorage(),
  limits: {
    fileSize: 2 * 1024 * 1024
  },
  fileFilter: (req, file, cb) => {
    const allowedExtensions = ['.csv', '.xml'];
    const ext = path.extname(file.originalname).toLowerCase();
    if (allowedExtensions.includes(ext)) {
      cb(null, true);
    } else {
      cb(new Error('Please upload a CSV or XML file.'));
    }
  }
});

function buildMeta(req) {
  const baseUrl = `${req.protocol}://${req.get('host')}`;
  const canonicalUrl = `${baseUrl}${req.originalUrl.split('?')[0]}`;
  const metaDescription =
    'Convert CSV to XML or XML to CSV instantly. Upload or paste data, preview the conversion, and download clean payloads.';
  const metaKeywords = 'csv to xml converter, xml to csv, data converter, webapp';
  const ogTitle = 'CSV/XML Data Converter | Monetize Hub';
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
        name: 'CSV/XML Data Converter',
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

function renderPage(req, res, extras = {}) {
  const meta = buildMeta(req);
  res.render('csv-xml-converter', {
    title: 'CSV/XML Data Converter',
    direction: extras.direction || 'csv-to-xml',
    input: extras.input,
    ...meta,
    ...extras
  });
}

function convertCsvToXml(csvText) {
  const rows = parseCsv(csvText, { columns: true, skip_empty_lines: true, trim: true });
  if (!rows.length) {
    throw new Error('No rows detected. Make sure your CSV includes a header row and at least one data row.');
  }
  const builder = new Builder({ rootName: 'dataset', xmldec: { version: '1.0', encoding: 'UTF-8' } });
  return builder.buildObject({ row: rows });
}

function extractRowsFromXml(payload) {
  if (!payload || typeof payload !== 'object') {
    return [];
  }

  if (Array.isArray(payload)) {
    const objects = payload.filter(item => typeof item === 'object' && item !== null);
    if (objects.length) {
      return objects;
    }
  }

  if (payload.row) {
    return Array.isArray(payload.row) ? payload.row : [payload.row];
  }

  if (payload.record) {
    return Array.isArray(payload.record) ? payload.record : [payload.record];
  }

  const arrayChild = Object.values(payload).find(
    value => Array.isArray(value) && value.some(item => typeof item === 'object' && item !== null)
  );
  if (arrayChild) {
    return arrayChild;
  }

  const nestedObjects = Object.values(payload).filter(value => typeof value === 'object' && value !== null);
  for (const nested of nestedObjects) {
    const rows = extractRowsFromXml(nested);
    if (rows.length) {
      return rows;
    }
  }

  return [payload];
}

function normalizeRow(row) {
  const normalized = {};
  Object.entries(row || {}).forEach(([key, value]) => {
    if (Array.isArray(value)) {
      normalized[key] = value
        .map(item => (typeof item === 'object' && item !== null ? JSON.stringify(item) : item ?? ''))
        .join('; ');
    } else if (typeof value === 'object' && value !== null) {
      normalized[key] = JSON.stringify(value);
    } else {
      normalized[key] = value ?? '';
    }
  });
  return normalized;
}

async function convertXmlToCsv(xmlText) {
  const parser = new Parser({ explicitArray: false, mergeAttrs: true, explicitRoot: false, trim: true });
  const parsed = await parser.parseStringPromise(xmlText);
  const rows = extractRowsFromXml(parsed);
  if (!rows.length) {
    throw new Error('No data rows found in the XML payload. Include repeating elements like <row> or <record>.');
  }
  const normalizedRows = rows.map(normalizeRow);
  return stringifyCsv(normalizedRows, { header: true });
}

function pickPayload(req) {
  const textInput = req.body.dataInput?.trim();
  if (textInput) {
    return { source: 'pasted input', text: textInput };
  }
  if (req.file) {
    return { source: req.file.originalname, text: req.file.buffer.toString('utf-8') };
  }
  return null;
}

router.get('/csv-xml-converter', (req, res) => {
  renderPage(req, res);
});

router.post('/csv-xml-converter', (req, res, next) => {
  upload.single('dataFile')(req, res, err => {
    if (err) {
      return renderPage(req, res, { error: err.message, input: req.body.dataInput, direction: req.body.direction });
    }
    next();
  });
});

router.post('/csv-xml-converter', async (req, res) => {
  const direction = req.body.direction === 'xml-to-csv' ? 'xml-to-csv' : 'csv-to-xml';
  const payload = pickPayload(req);

  if (!payload) {
    return renderPage(req, res, {
      error: 'Please paste data or upload a CSV/XML file to convert.',
      direction
    });
  }

  try {
    let output;
    let fileExtension;

    if (direction === 'csv-to-xml') {
      output = convertCsvToXml(payload.text);
      fileExtension = '.xml';
    } else {
      output = await convertXmlToCsv(payload.text);
      fileExtension = '.csv';
    }

    const baseName = payload.source === 'pasted input' ? 'converted-data' : path.parse(payload.source).name;
    const fileName = `${baseName}${fileExtension}`;

    renderPage(req, res, {
      result: {
        output,
        fileName,
        direction,
        sourceLabel: payload.source,
        downloadPayload: Buffer.from(output).toString('base64')
      },
      direction,
      input: payload.source === 'pasted input' ? payload.text : ''
    });
  } catch (error) {
    renderPage(req, res, {
      error: error.message || 'We could not process that data. Please check the format and try again.',
      direction,
      input: payload.text
    });
  }
});

router.post('/csv-xml-converter/download', (req, res) => {
  const { payload, fileName } = req.body;
  if (!payload) {
    return res.redirect('/csv-xml-converter');
  }

  const buffer = Buffer.from(payload, 'base64');
  const defaultName = fileName || 'converted-data.txt';
  const contentType = defaultName.endsWith('.csv') ? 'text/csv' : 'application/xml';

  res.setHeader('Content-Type', contentType);
  res.setHeader('Content-Disposition', `attachment; filename="${defaultName}"`);
  res.send(buffer);
});

export default router;
