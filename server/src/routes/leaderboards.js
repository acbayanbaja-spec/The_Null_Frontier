'use strict';

const express = require('express');
const { z } = require('zod');
const { getSupabase, isConfigured } = require('../lib/supabase');
const { authenticate } = require('../middleware/authenticate');
const { validate } = require('../middleware/validate');

const router = express.Router();

const leaderboardIdSchema = z.string().min(1).max(64).regex(/^[a-zA-Z0-9_.-]+$/, 'invalid leaderboard id');

const scoreSchema = z.object({
  leaderboardId: leaderboardIdSchema,
  score: z.number().int().min(0).max(Number.MAX_SAFE_INTEGER),
});

// POST /api/leaderboards/score  { leaderboardId, score } -> 200 { ok, rank }
// Upserts the caller's best-known score (highest wins, so keep the max).
router.post('/score', authenticate, validate(scoreSchema), async (req, res) => {
  const { leaderboardId, score } = req.body;
  const supabase = req.supabase;
  const userId = req.user.id;

  const { data: existing } = await supabase
    .from('leaderboard_entries')
    .select('score')
    .eq('leaderboard_id', leaderboardId)
    .eq('user_id', userId)
    .maybeSingle();

  const finalScore = existing && existing.score > score ? existing.score : score;

  const { error } = await supabase
    .from('leaderboard_entries')
    .upsert(
      {
        leaderboard_id: leaderboardId,
        user_id: userId,
        score: finalScore,
        updated_at: new Date().toISOString(),
      },
      { onConflict: 'leaderboard_id,user_id' }
    );

  if (error) {
    return res.status(500).json({ error: 'score_write_failed', message: error.message });
  }

  // Rank = number of strictly-higher scores in this leaderboard + 1.
  const { count, error: countError } = await supabase
    .from('leaderboard_entries')
    .select('user_id', { count: 'exact', head: true })
    .eq('leaderboard_id', leaderboardId)
    .gt('score', finalScore);

  if (countError) {
    return res.status(500).json({ error: 'rank_failed', message: countError.message });
  }

  return res.json({ ok: true, rank: (count || 0) + 1 });
});

// GET /api/leaderboards/:leaderboardId -> 200 { entries: [{ username, score, rank }] }
// Public read (no auth required); RLS allows world-readable leaderboard selects.
router.get('/:leaderboardId', async (req, res) => {
  if (!isConfigured()) {
    return res.status(503).json({ error: 'backend_not_configured' });
  }
  const parsed = leaderboardIdSchema.safeParse(req.params.leaderboardId);
  if (!parsed.success) {
    return res.status(400).json({ error: 'validation_error', message: 'invalid leaderboard id' });
  }
  const leaderboardId = parsed.data;

  const { data, error } = await getSupabase()
    .from('leaderboard_entries')
    .select('user_id, score, profiles!inner(username)')
    .eq('leaderboard_id', leaderboardId)
    .order('score', { ascending: false })
    .limit(100);

  if (error) {
    return res.status(500).json({ error: 'leaderboard_read_failed', message: error.message });
  }

  const rows = data || [];
  const entries = rows.map((row, index) => ({
    username: row.profiles && row.profiles.username ? row.profiles.username : 'unknown',
    score: Number(row.score),
    rank: index + 1,
  }));

  return res.json({ entries });
});

module.exports = router;
