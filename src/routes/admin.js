import express from 'express';
import { ensureAdmin } from '../middleware/auth.js';
import { getAllUsers } from '../db.js';

const router = express.Router();

router.get('/admin', ensureAdmin, async (req, res) => {
  const users = await getAllUsers();
  res.render('admin', {
    title: 'Admin dashboard',
    users
  });
});

export default router;
