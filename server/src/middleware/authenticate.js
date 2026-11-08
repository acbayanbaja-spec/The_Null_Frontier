'use strict';

const { getSupabase, isConfigured } = require('../lib/supabase');
const config = require('../config');

/**
 * Verifies the Supabase access JWT sent as "Authorization: Bearer <token>".
 * Uses the anon key + the user's own token, so RLS policies apply to every
 * subsequent query. Attaches the Supabase user object to req.user.
 *
 * 503 { error: "backend_not_configured" } when Supabase env vars are missing.
 * 401 { error: "unauthorized" } when the token is missing or invalid.
 */
async function authenticate(req, res, next) {
  if (!isConfigured()) {
    return res.status(503).json({ error: 'backend_not_configured' });
  }

  const header = req.headers.authorization || '';
  const token = header.startsWith('Bearer ') ? header.slice('Bearer '.length).trim() : null;
  if (!token) {
    return res.status(401).json({ error: 'unauthorized' });
  }

  try {
    const { data, error } = await getSupabase().auth.getUser(token);
    if (error || !data || !data.user) {
      return res.status(401).json({ error: 'unauthorized' });
    }
    req.user = data.user;
    req.token = token;
    // Per-request client that carries the user's JWT on every PostgREST call,
    // so Postgres Row Level Security policies apply to all queries.
    const { createClient } = require('@supabase/supabase-js');
    req.supabase = createClient(config.supabase.url, config.supabase.anonKey, {
      auth: { persistSession: false, autoRefreshToken: false },
      global: { headers: { Authorization: `Bearer ${token}` } },
    });
    return next();
  } catch (err) {
    return res.status(401).json({ error: 'unauthorized' });
  }
}

module.exports = { authenticate };
