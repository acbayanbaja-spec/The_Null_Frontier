'use strict';

const express = require('express');
const { z } = require('zod');
const { authenticate } = require('../middleware/authenticate');
const { validate } = require('../middleware/validate');

const router = express.Router();

// Arbitrary JSON object save blob (limited to 1 MB).
const saveSchema = z.record(z.string(), z.any()).or(z.array(z.any()));

// GET /api/saves/cloud -> 200 { saveData } (null when no save exists)
router.get('/cloud', authenticate, async (req, res) => {
  const { data, error } = await req.supabase
    .from('player_saves')
    .select('save_data')
    .eq('user_id', req.user.id)
    .maybeSingle();

  if (error) {
    return res.status(500).json({ error: 'save_read_failed', message: error.message });
  }

  return res.json({ saveData: data && data.save_data !== undefined ? data.save_data : null });
});

// PUT /api/saves/cloud  body = arbitrary JSON save blob -> 200 { ok: true }
router.put('/cloud', authenticate, validate(saveSchema), async (req, res) => {
  const row = {
    user_id: req.user.id,
    save_data: req.body,
    updated_at: new Date().toISOString(),
  };

  const { error } = await req.supabase
    .from('player_saves')
    .upsert(row, { onConflict: 'user_id' });

  if (error) {
    return res.status(500).json({ error: 'save_write_failed', message: error.message });
  }

  return res.json({ ok: true });
});

module.exports = router;
