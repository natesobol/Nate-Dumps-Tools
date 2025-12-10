import { getUserById } from '../db.js';

export function ensureAuthenticated(req, res, next) {
  if (req.session.userId) {
    return next();
  }
  req.session.returnTo = req.originalUrl;
  return res.redirect('/login');
}

export async function ensureAdmin(req, res, next) {
  if (!req.session.userId) {
    req.session.returnTo = req.originalUrl;
    return res.redirect('/login');
  }

  const user = await getUserById(req.session.userId);
  if (user?.role === 'admin') {
    req.session.user = {
      id: user.id,
      email: user.email,
      fullName: user.full_name,
      subscriptionPlan: user.subscription_plan,
      marketingOptIn: Boolean(user.marketing_opt_in),
      role: user.role
    };
    return next();
  }

  return res.status(403).render('404', { title: 'Forbidden' });
}

export function setUserLocals(req, res, next) {
  res.locals.currentUser = req.session.user;
  res.locals.flash = req.session.flash;
  delete req.session.flash;
  next();
}

export function flashMessage(req, type, message) {
  req.session.flash = { type, message };
}
