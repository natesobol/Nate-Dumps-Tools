import sqlite3 from 'sqlite3';
import path from 'path';
import bcrypt from 'bcrypt';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const dbPath = process.env.DATABASE_PATH || path.join(__dirname, '..', 'data', 'app.db');

sqlite3.verbose();
const db = new sqlite3.Database(dbPath);

async function seedAdminUser() {
  db.get(`SELECT id FROM users WHERE email = ?`, ['admin@example.com'], async (err, row) => {
    if (err) {
      console.error('Failed to check for default admin user', err);
      return;
    }

    if (!row) {
      const passwordHash = await bcrypt.hash('admin', 12);
      db.run(
        `INSERT INTO users (email, password_hash, full_name, subscription_plan, marketing_opt_in, role) VALUES (?, ?, ?, 'enterprise', 0, 'admin')`,
        ['admin@example.com', passwordHash, 'Admin'],
        (insertErr) => {
          if (insertErr) {
            console.error('Failed to seed default admin user', insertErr);
          }
        }
      );
    }
  });
}

db.serialize(() => {
  db.run(`
    CREATE TABLE IF NOT EXISTS users (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      email TEXT UNIQUE NOT NULL,
      password_hash TEXT NOT NULL,
      full_name TEXT,
      subscription_plan TEXT DEFAULT 'free',
      marketing_opt_in INTEGER DEFAULT 0,
      role TEXT DEFAULT 'user' CHECK(role IN ('user', 'admin')),
      created_at TEXT DEFAULT (datetime('now')),
      updated_at TEXT DEFAULT (datetime('now'))
    )
  `);

  db.run(`ALTER TABLE users ADD COLUMN role TEXT DEFAULT 'user' CHECK(role IN ('user', 'admin'))`, (err) => {
    if (err && !err.message.includes('duplicate column name')) {
      console.error('Failed to add role column', err);
    }
  });

  seedAdminUser();
});

export function createUser({ email, passwordHash, fullName, subscriptionPlan, marketingOptIn, role = 'user' }) {
  return new Promise((resolve, reject) => {
    const stmt = `INSERT INTO users (email, password_hash, full_name, subscription_plan, marketing_opt_in, role) VALUES (?, ?, ?, ?, ?, ?)`;
    db.run(stmt, [email, passwordHash, fullName || null, subscriptionPlan || 'free', marketingOptIn ? 1 : 0, role], function (err) {
      if (err) return reject(err);
      resolve({ id: this.lastID, email, fullName, subscriptionPlan, marketingOptIn, role });
    });
  });
}

export function getUserByEmail(email) {
  return new Promise((resolve, reject) => {
    db.get(`SELECT * FROM users WHERE email = ?`, [email], (err, row) => {
      if (err) return reject(err);
      resolve(row);
    });
  });
}

export function getUserById(id) {
  return new Promise((resolve, reject) => {
    db.get(`SELECT * FROM users WHERE id = ?`, [id], (err, row) => {
      if (err) return reject(err);
      resolve(row);
    });
  });
}

export function updateUserProfile(id, { fullName, subscriptionPlan, marketingOptIn }) {
  return new Promise((resolve, reject) => {
    const stmt = `
      UPDATE users
      SET full_name = ?,
          subscription_plan = ?,
          marketing_opt_in = ?,
          updated_at = datetime('now')
      WHERE id = ?
    `;
    db.run(stmt, [fullName || null, subscriptionPlan || 'free', marketingOptIn ? 1 : 0, id], function (err) {
      if (err) return reject(err);
      resolve(this.changes > 0);
    });
  });
}

export function getAllUsers() {
  return new Promise((resolve, reject) => {
    db.all(
      `SELECT id, email, full_name, subscription_plan, marketing_opt_in, role, created_at, updated_at FROM users ORDER BY created_at DESC`,
      (err, rows) => {
        if (err) return reject(err);
        resolve(rows);
      }
    );
  });
}

export default db;
