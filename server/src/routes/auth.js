'use strict';

const express = require('express');
const rateLimit = require('express-rate-limit');
const { z } = require('zod');
const { getSupabase, isConfigured } = require('../lib/supabase');
const { validate } = require('../middleware/validate');

const router = express.Router();

// Rate limit auth endpoints against brute force / credential stuffing.
const authLimiter = rateLimit({
  windowMs: 15 * 60 * 1000,
  limit: 50,
  standardHeaders: 'draft-7',
  legacyHeaders: false,
  message: { error: 'rate_limited' },
});
router.use(authLimiter);

const signupSchema = z.object({
  email: z.string().email().max(320),
  password: z.string().min(6).max(128),
  username: z.string().min(3).max(24).regex(/^[a-zA-Z0-9_.-]+$/, 'username may only contain letters, numbers, _ . -'),
});

const signinSchema = z.object({
  email: z.string().email().max(320),
  password: z.string().min(1).max(128),
});

function notConfigured(res) {
  return res.status(503).json({ error: 'backend_not_configured' });
}

// POST /api/auth/signup  { email, password, username } -> 200 { token }
router.post('/signup', validate(signupSchema), async (req, res) => {
  if (!isConfigured()) return notConfigured(res);
  const { email, password, username } = req.body;

  const supabase = getSupabase();
  const { data, error } = await supabase.auth.signUp({
    email,
    password,
    options: { data: { username } },
  });
  if (error || !data) {
    const status = error && error.status ? error.status : 400;
    return res.status(status).json({ error: 'signup_failed', message: error ? error.message : 'unknown error' });
  }

  // If email confirmation is enabled the signup returns no session; attempt an
  // immediate sign-in so the client still receives a usable token.
  if (!data.session) {
    const signIn = await supabase.auth.signInWithPassword({ email, password });
    if (signIn.error || !signIn.data.session) {
      return res.status(403).json({ error: 'email_confirmation_required' });
    }
    return res.json({ token: signIn.data.session.access_token });
  }

  return res.json({ token: data.session.access_token });
});

// POST /api/auth/signin  { email, password } -> 200 { token } (401 on bad creds)
router.post('/signin', validate(signinSchema), async (req, res) => {
  if (!isConfigured()) return notConfigured(res);
  const { email, password } = req.body;

  const { data, error } = await getSupabase().auth.signInWithPassword({ email, password });
  if (error || !data || !data.session) {
    return res.status(401).json({ error: 'invalid_credentials' });
  }

  return res.json({ token: data.session.access_token });
});

module.exports = router;
