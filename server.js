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
import { setUserLocals } from './src/middleware/auth.js';
import './src/db.js';

dotenv.config();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const SQLiteStore = SQLiteStoreFactory(session);

app.set('view engine', 'ejs');
app.set('views', path.join(__dirname, 'views'));

app.use(helmet());
app.use(express.urlencoded({ extended: true }));
app.use(express.static(path.join(__dirname, 'public')));
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

app.use(setUserLocals);

app.use('/', pageRoutes);
app.use('/', authRoutes);
app.use('/', adminRoutes);

app.use((req, res) => {
  res.status(404).render('404', { title: 'Not found' });
});

const port = process.env.PORT || 3000;
app.listen(port, () => {
  console.log(`Server listening on http://localhost:${port}`);
});
