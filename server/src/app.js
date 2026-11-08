'use strict';

const express = require('express');
const helmet = require('helmet');
const cors = require('cors');
const config = require('./config');

const authRoutes = require('./routes/auth');
const saveRoutes = require('./routes/saves');
const leaderboardRoutes = require('./routes/leaderboards');

function createApp() {
  const app = express();

  app.disable('x-powered-by');
  app.set('trust proxy', 1);

  app.use(helmet());
  app.use(cors({
    origin: config.corsOrigin === '*' ? true : config.corsOrigin.split(',').map((o) => o.trim()),
  }));
  app.use(express.json({ limit: '2mb' }));

  app.get('/api/health', (req, res) => res.json({ ok: true }));

  app.use('/api/auth', authRoutes);
  app.use('/api/saves', saveRoutes);
  app.use('/api/leaderboards', leaderboardRoutes);

  // 404 for anything else under /api
  app.use('/api', (req, res) => res.status(404).json({ error: 'not_found' }));

  // JSON body parse errors and any thrown errors land here.
  // eslint-disable-next-line no-unused-vars
  app.use((err, req, res, next) => {
    if (err && err.type === 'entity.parse.failed') {
      return res.status(400).json({ error: 'invalid_json' });
    }
    return res.status(500).json({ error: 'internal_error' });
  });

  return app;
}

module.exports = { createApp };
