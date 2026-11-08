# NULL FRONTIER — Development Roadmap

Derived from `Docs/TECHNICAL_AUDIT.md`. Each stage is a shippable, testable
increment. A stage is complete only when its acceptance checks pass **and** the
result is committed. Untested device claims are never part of an exit criterion.

Order is deliberate: data integrity and truthful UI before content; rendering
before asset spend; device profiling before performance claims; netcode before
any multiplayer marketing.

## Progress log

- **2026-11-08** — Stage 0 source changes landed: save-load event (C1), co-op
  status page (C2), canon roster unlock and removal of the invented sixth hero
  (C3), no baked backend endpoint and HTTPS-only device URLs (C4), blank NEW GAME
  no longer uploads over the cloud copy (C5 partial), URP `PostProcessData`
  repair (H1), in-game credits and StreamingAssets notices (H2), reload reset on
  weapon switch (H3), touch weapon switch (H4), and touch edge retention (M4).
- **2026-11-08, later pass** — Both scenes rebuilt and inspected in editor Play.
  Menu renders with full UI (12 active buttons, 10 persistent listeners; Continue
  and Co-op wired at runtime). VerticalCity renders with a real HUD
  (JAHred Lv. 3, mission "Missing Players", 32/32 ammo; hostile count 13 at
  00:05 as spawns streamed in). Player feet
  land at Y≈0.31 on the Y=0 floor, body centre Y≈1.14; camera now pitches through
  a `CinemachineRotationComposer` aim stage and frames an over-the-shoulder shot
  with feet and shadow visible. Two play-mode teardown null references were fixed
  (`PlayerCameraController`, `DesktopInputProvider`); the final runs logged zero
  console errors. City facades gained four-sided glass and lit window panels
  anchored to each building's main mass. Offline Roslyn gate still passes.
  **Android remains untested.** See `TECHNICAL_AUDIT.md` §1.1.

---

## Stage 0 — Stabilisation and truth (in progress)

**Objective:** stop silent data loss and remove claims the build cannot back.

- C1 inventory load-order wipe: `SaveLoaded` event; inventory re-hydrates.
- C2 co-op honesty: status-only page, no fake invites, remove NETWORKED badge.
- C3 roster: unlock/migrate the five canon operatives; remove invented sixth.
- C4 configurable HTTPS API base; no localhost baked in scenes.
- C5 partial: NEW GAME must not upload a blank save over the cloud copy.
- H1 post-processing: assign `PostProcessData`, idempotent bootstrap, verify ACES/bloom.
- H2 credits: in-game credits page + `StreamingAssets` copy; Quaternius note.
- H3 reload/switch soft-lock; H4 mobile weapon switch.
- Compile gate, rebuild both scenes, Play capture, zero runtime errors.
- Commit + push checkpoint.

**Status:** compile gate, scene rebuild and Play capture are done (2026-11-08,
see the progress log). Still unverified: returning-save inventory after a real
menu load, reload/switch/fire via scripted input, and the credits page in Play.
Those are the remaining Stage 0 acceptance checks.

**Acceptance:** returning save shows its inventory after menu load; every roster
row selectable; co-op page states it is unavailable; player can reload, switch
and fire; bloom/ACES visible in a real Play capture; credits reachable in UI.

**Blocked on:** none.

---

## Stage 1 — Save and account integrity

**Objective:** make progression durable locally and in the cloud.

- Multiple save slots (local + backend composite key), slot picker, delete.
- Write → fsync/backup → verify; recover from `.bak`; corrupt-file quarantine.
- Download-before-upload with `lastSavedUtcTicks` comparison and explicit conflict
  resolution (keep local / keep cloud / duplicate).
- Refresh token and silent re-auth; sign-out that clears tokens only.
- Server: transactional save validation, per-user rate limits, structured errors.
- Tests: load-order, slot isolation, conflict matrix, corruption recovery.

**Acceptance:** power-loss simulation (kill process mid-save) never loses the last
good save; two devices converge deterministically; automated tests cover the matrix.

**Blocked on:** provisioned Supabase project + backend `.env` (user-owned secrets).

---

## Stage 2 — Rendering foundation

**Objective:** make the existing stylised art look intentionally cinematic.

- Linear colour space + light-intensity audit; screenshot comparison.
- Repair/verify post-processing (ACES, bloom, grade, vignette) on both scenes.
- Reflection probe coverage, reflection quality per tier, SSAO/decals as budget allows.
- Sky/atmosphere and fog pass; exposure and colour-grade per region.
- Earth shader upgrade: day/night terminator, ocean specular, cloud shading,
  atmosphere rim, optional night lights (using the existing CC BY maps).
- LODGroups and occlusion culling for VerticalCity; draw-call reduction.
- Capture matrix: 16:9, 20:9, 21:9, 4:3 Game view.

**Acceptance:** measurable draw-call reduction; no visible pop at tier boundaries;
before/after captures for every change; menu and city match the intended palette.

**Blocked on:** none for the shader/rendering work.

---

## Stage 3 — First region, actually playable

**Objective:** one complete 20–30 minute loop in Vertical City.

- Reachability pass: maze and facility entrances reachable from the menu flow.
- 8 shipped missions verified end-to-end; rewards granted exactly once.
- Traversal: doors, terminals, caches, respawn, fast-travel node.
- Combat loop: hit reaction, cover, death, respawn, ammo economy, weapon switching.
- Performance budget: target frame time and memory per quality tier defined, then
  measured (editor first, device in Stage 9).
- PlayMode tests for mission completion, save/reload of progression, respawn.

**Acceptance:** a fresh save can complete the region without console errors; quit and
continue restores position, inventory, missions; tests green.

**Blocked on:** none.

---

## Stage 4 — Characters and combat depth

**Objective:** make the roster mechanically distinct and the combat readable.

- Apply `MaxShield`, `DefenseMultiplier`, mobility and ability ranks in gameplay.
- Persist skill ranks; respec rule; UI reflects real values.
- Full animation state coverage (idle/walk/run/sprint/jump/fall/land/crouch/dodge/
  climb/weapon handling/reload/melee/ability/victory/defeat) with smooth blends.
- Customisation studio (cosmetic slots, colours, unlock rules) on the shared rig.
- Hit feedback, audio, tracers, impacts, damage numbers consistent across weapons.

**Acceptance:** each operative's stats change time-to-kill/defence measurably; no
animation pops when chaining actions; customisation persists across slots.

**Blocked on:** realistic character assets (see External requirements). The
mechanical work can proceed on the current rig.

---

## Stage 5 — Enemy AI, missions, bosses

- Behaviour coordination, cover, retreat, sound investigation, class roles.
- Boss framework: phases, telegraphs, weaknesses, rewards, victory sequence.
- Christian Loyd implemented as the antagonist arc (not a playable roster row).
- Encounter tests: aggro, line-of-sight, retreat, phase transitions.

**Acceptance:** encounters are winnable with strategy and fail under brute force;
boss phases verified in PlayMode tests; no navmesh stalls.

**Blocked on:** boss art/animation assets (Stage 4 decision).

---

## Stage 6 — Real multiplayer (only when infrastructure is approved)

**Objective:** remove the "CO-OP unavailable" status by making it true.

- Choose transport (e.g. Netcode for GameObjects + relay, or Photon Fusion) based on
  cost, server authority, mobile NAT traversal and maintainability.
- Server-authoritative movement/combat for competitive modes; client prediction.
- Gathering lobby: real replicated players, emotes, names, party, ready state.
- Matchmaking, private rooms, region selection, countdown, results.
- Sync tests: latency/loss simulation, rejoin, host migration policy.

**Acceptance:** two physical devices on different networks see each other move,
shoot and complete a match; authoritative results reject a tampered client.

**Blocked on:** explicit approval of the service and its cost; backend session API.

---

## Stage 7 — Social and progression

- Friends, requests, presence, party invites, messaging, block/report.
- XP/levels/season track, daily/weekly missions, leaderboards with server validation.
- Anti-cheat: server-side reward and score validation, anomaly flags, audit log.
- Notification and moderation surfaces.

**Acceptance:** social actions work across accounts and survive relaunch; leaderboards
reject impossible scores; every reward is idempotent.

**Blocked on:** Stage 6 infrastructure; moderation policy.

---

## Stage 8 — Production backend and security hardening

- RLS verified per table with negative tests; least-privilege keys; secret rotation.
- CORS allow-list, per-route rate limits, body limits after auth, schema validation.
- Error normalisation (no Supabase internals), request IDs, structured logs.
- Account recovery, session revocation, audit trail, abuse detection.
- HTTPS only; no localhost anywhere; environment-specific configuration.
- CI: lint, unit/integration tests, migration dry-run, dependency audit.

**Acceptance:** security test suite green; no secret in client or repo; failed auth
never leaks internals; recovery flow verified.

**Blocked on:** hosted environment and credentials (user-supplied).

---

## Stage 9 — Android performance and device QA

- Build release candidate (AAB + APK) with a real keystore.
- Measure on at least one low-end, one mid-range and one high-end physical device:
  frame time percentiles, memory, thermals, battery, load time, input latency.
- Tune LOW/MEDIUM/HIGH/ULTRA monotonically; adaptive quality only if it measurably
  helps; ASTC textures; mesh/texture budgets; object pooling audit.
- Test aspect ratios, notches, safe areas, touch hit targets, pause/resume, low memory.
- Record real results in `Docs/DEVICE_MATRIX.md`.

**Acceptance:** published per-tier targets met on named devices, or targets revised
honestly; no crash/ANR in a 30-minute session; input works on all tested screens.

**Blocked on:** physical devices and a signing keystore.

---

## Stage 10 — Release readiness

- Full regression pass over every screen and button; automated UI tests where possible.
- Accessibility: remapping, text scale, colour-blind-safe accents, captions.
- Store assets, privacy policy, age rating, licence page, versioning.
- Final audit update with known limitations; tag release; archive artifacts.

**Acceptance:** definition of done met for every shipped feature; known issues listed;
rollback build available.

**Blocked on:** Stage 9; store accounts.

---

## External requirements and approvals

| Item | Needed for | Decision |
|---|---|---|
| Hosted backend + Supabase | Stages 1, 7, 8 | User must provision; no secrets in repo |
| Multiplayer service | Stage 6 | Cost/terms approval required |
| Realistic art packs / character creator | visual target, Stages 4–5 | Paid assets need approval; prefer free licensed |
| Physical Android devices | Stage 9 | None available to the agent |
| Release keystore | Stage 9 | User-owned |

## Current visual reality

The current build is a stylised low-poly sci-fi prototype. It is not photoreal and
will not become photoreal by shader work alone. Stage 2 improves lighting, colour and
the Earth; Stages 4–5 require new realistic assets. No claim of "AAA photorealism" will
be made until those assets exist and a device build is profiled.
