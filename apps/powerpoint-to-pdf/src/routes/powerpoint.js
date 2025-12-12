import express from 'express';
import multer from 'multer';
import path from 'path';
import { promisify } from 'util';
import libre from 'libreoffice-convert';

const router = express.Router();
const upload = multer({
  storage: multer.memoryStorage(),
  limits: {
    fileSize: 50 * 1024 * 1024 // 50MB
  }
});

const convertAsync = promisify(libre.convert);

const invalidExtMessage = 'Please upload a .ppt or .pptx file to convert.';

router.post('/powerpoint-to-pdf/convert', upload.single('file'), async (req, res) => {
  if (!req.file) {
    return res.status(400).json({ error: 'No file provided. Choose a PowerPoint file to continue.' });
  }

  const ext = path.extname(req.file.originalname || '').toLowerCase();
  if (!['.ppt', '.pptx'].includes(ext)) {
    return res.status(400).json({ error: invalidExtMessage });
  }

  try {
    const pdfBuffer = await convertAsync(req.file.buffer, '.pdf', undefined);
    const safeName = (req.file.originalname || 'presentation').replace(/\s+/g, '-').replace(/[^a-zA-Z0-9.-]/g, '');
    res.setHeader('Content-Type', 'application/pdf');
    res.setHeader('Content-Disposition', `attachment; filename="${safeName.replace(/\.[^.]+$/, '') || 'presentation'}.pdf"`);
    res.send(pdfBuffer);
  } catch (error) {
    const missingLibre =
      error && typeof error.message === 'string' &&
      (error.message.includes('ENOENT') || error.message.toLowerCase().includes('spawn soffice'));

    const guidance = missingLibre
      ? 'LibreOffice is required for conversion. Install it locally and ensure the soffice binary is on your PATH.'
      : 'Conversion failed. Try a different file or check that LibreOffice is installed.';

    res.status(500).json({ error: guidance });
  }
});

export default router;
