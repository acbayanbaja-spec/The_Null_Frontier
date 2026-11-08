'use strict';

require('dotenv').config();

const config = {
  port: Number.parseInt(process.env.PORT, 10) || 3000,
  corsOrigin: process.env.CORS_ORIGIN || '*',
  supabase: {
    url: process.env.SUPABASE_URL || '',
    anonKey: process.env.SUPABASE_ANON_KEY || '',
    serviceKey: process.env.SUPABASE_SERVICE_KEY || '',
  },
};

config.isSupabaseConfigured = Boolean(config.supabase.url && config.supabase.anonKey);

module.exports = config;
