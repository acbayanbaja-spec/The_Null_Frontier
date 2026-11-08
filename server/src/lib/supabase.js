'use strict';

const { createClient } = require('@supabase/supabase-js');
const config = require('../config');

// Anon-key client used for ALL user-authenticated traffic. Every request that
// carries a user JWT is executed with that JWT (via getUser / per-request
// auth), so Postgres Row Level Security applies. The service-role key is never
// used here.
let anonClient = null;

function getSupabase() {
  if (!config.isSupabaseConfigured) return null;
  if (!anonClient) {
    anonClient = createClient(config.supabase.url, config.supabase.anonKey, {
      auth: { persistSession: false, autoRefreshToken: false },
    });
  }
  return anonClient;
}

function isConfigured() {
  return config.isSupabaseConfigured;
}

module.exports = { getSupabase, isConfigured };
