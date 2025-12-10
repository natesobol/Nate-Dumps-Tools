import express from 'express';
import bcrypt from 'bcrypt';
import { body, validationResult } from 'express-validator';
import { createUser, getUserByEmail, getUserById, updateUserProfile } from '../db.js';
import { ensureAuthenticated, flashMessage } from '../middleware/auth.js';

const router = express.Router();

router.get('/login', (req, res) => {
  res.render('login', { title: 'Login' });
});

router.post(
  '/login',
  body('email').isEmail().withMessage('Please enter a valid email address.'),
  body('password').notEmpty().withMessage('Password is required.'),
  async (req, res) => {
    const errors = validationResult(req);
    if (!errors.isEmpty()) {
      flashMessage(req, 'error', errors.array()[0].msg);
      return res.redirect('/login');
    }

    const { email, password } = req.body;
    const user = await getUserByEmail(email.toLowerCase());

    if (!user) {
      flashMessage(req, 'error', 'Invalid credentials.');
      return res.redirect('/login');
    }

    const match = await bcrypt.compare(password, user.password_hash);
    if (!match) {
      flashMessage(req, 'error', 'Invalid credentials.');
      return res.redirect('/login');
    }

    req.session.userId = user.id;
    req.session.user = {
      id: user.id,
      email: user.email,
      fullName: user.full_name,
      subscriptionPlan: user.subscription_plan,
      marketingOptIn: Boolean(user.marketing_opt_in),
      role: user.role
    };

    const redirectTo = req.session.returnTo || '/account';
    delete req.session.returnTo;
    return res.redirect(redirectTo);
  }
);

router.get('/register', (req, res) => {
  res.render('register', { title: 'Create account' });
});

router.post(
  '/register',
  body('email').isEmail().withMessage('Please enter a valid email address.'),
  body('password').isLength({ min: 8 }).withMessage('Password must be at least 8 characters.'),
  body('fullName').trim().notEmpty().withMessage('Name is required.'),
  async (req, res) => {
    const errors = validationResult(req);
    if (!errors.isEmpty()) {
      flashMessage(req, 'error', errors.array()[0].msg);
      return res.redirect('/register');
    }

    const { email, password, fullName } = req.body;
    const existing = await getUserByEmail(email.toLowerCase());
    if (existing) {
      flashMessage(req, 'error', 'An account with that email already exists.');
      return res.redirect('/register');
    }

    const passwordHash = await bcrypt.hash(password, 10);
    await createUser({ email: email.toLowerCase(), passwordHash, fullName });
    flashMessage(req, 'success', 'Account created. Please log in.');
    return res.redirect('/login');
  }
);

router.post('/logout', (req, res) => {
  req.session.destroy(() => {
    res.redirect('/');
  });
});

router.get('/account', ensureAuthenticated, async (req, res) => {
  const user = await getUserById(req.session.userId);
  res.render('account', {
    title: 'Your account',
    user
  });
});

router.post(
  '/account',
  ensureAuthenticated,
  body('fullName').trim().notEmpty().withMessage('Name is required.'),
  body('subscriptionPlan').isIn(['free', 'pro', 'enterprise']).withMessage('Invalid plan selected.'),
  async (req, res) => {
    const errors = validationResult(req);
    if (!errors.isEmpty()) {
      flashMessage(req, 'error', errors.array()[0].msg);
      return res.redirect('/account');
    }

    const { fullName, subscriptionPlan, marketingOptIn } = req.body;
    await updateUserProfile(req.session.userId, {
      fullName,
      subscriptionPlan,
      marketingOptIn: marketingOptIn === 'on'
    });

    req.session.user = {
      ...req.session.user,
      fullName,
      subscriptionPlan,
      marketingOptIn: marketingOptIn === 'on'
    };

    flashMessage(req, 'success', 'Profile updated.');
    return res.redirect('/account');
  }
);

export default router;
