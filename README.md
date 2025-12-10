# Monetize Hub Starter

A starter website for hosting webapps with monetization in mind. It provides a home page, about page, navigation menu, and a robust login/account system backed by SQLite.

## Features
- Home and About pages with modern UI and navigation menu
- Registration and login with hashed passwords and persistent sessions
- Account dashboard for updating profile, subscription plan, and marketing preferences
- Admin-only dashboard for reviewing all accounts (seeded with `admin@example.com` / `admin` — change immediately)
- SQLite database for user data and session storage
- EJS templating with responsive styling

## Getting started
1. Install dependencies:
   ```bash
   npm install
   ```
2. Copy the environment template and update secrets as needed:
   ```bash
   cp .env.example .env
   ```
3. Start the development server:
   ```bash
   npm run dev
   ```
4. Visit `http://localhost:3000` to view the site.

## Tech stack
- Node.js + Express
- SQLite for data and session storage
- EJS for server-rendered views
- Helmet and secure session defaults for baseline security

## Notes
- User passwords are stored as bcrypt hashes.
- Profile updates persist to the database and refresh the session payload.
- Replace placeholder links in the footer and pricing sections with production resources when ready.
