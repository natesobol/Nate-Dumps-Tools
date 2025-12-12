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

dotenv.config();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const SQLiteStore = SQLiteStoreFactory(session);

app.set('view engine', 'ejs');
app.set('views', [
  path.join(__dirname, 'views'),
  path.join(__dirname, 'webapps/webapp-views-htmls'),
  path.join(__dirname, 'apps/excel-to-json/views'),
  path.join(__dirname, 'apps/csv-xml-converter/views')
]);

app.use(helmet());
app.use(express.urlencoded({ extended: true }));
app.use(express.static(path.join(__dirname, 'public')));
app.use('/json-combiner', express.static(path.join(__dirname, 'apps/json-combiner/wwwroot')));
app.use('/pdf-splitter', express.static(path.join(__dirname, 'apps/pdf-splitter/wwwroot')));
app.use('/json-to-excel', express.static(path.join(__dirname, 'apps/json-to-excel/wwwroot')));
app.use('/powerpoint-to-pdf', express.static(path.join(__dirname, 'apps/powerpoint-to-pdf/wwwroot')));
app.use(
  '/powerpoint-image-extractor',
  express.static(path.join(__dirname, 'apps/powerpoint-image-extractor/wwwroot'))
);
app.use('/list-comparison', express.static(path.join(__dirname, 'apps/list-comparison/wwwroot')));
app.use('/html-tag-cleaner', express.static(path.join(__dirname, 'apps/html-tag-cleaner/wwwroot')));
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
app.use('/', excelRoutes);
app.use('/', csvXmlRoutes);
app.use('/', powerpointRoutes);

app.use((req, res) => {
  res.status(404).render('404', { title: 'Not found' });
});

const port = process.env.PORT || 3000;
app.listen(port, () => {
  console.log(`Server listening on http://localhost:${port}`);
});
