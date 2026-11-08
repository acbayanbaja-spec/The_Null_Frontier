'use strict';

const config = require('./config');
const { createApp } = require('./app');

const app = createApp();

app.listen(config.port, () => {
  // eslint-disable-next-line no-console
  console.log(`[null-frontier] listening on http://0.0.0.0:${config.port}`);
  if (!config.isSupabaseConfigured) {
    // eslint-disable-next-line no-console
    console.warn('[null-frontier] SUPABASE_URL / SUPABASE_ANON_KEY not set — auth-dependent routes will return 503 backend_not_configured');
  }
});
