# NULL FRONTIER

Third-person 3D open-world science-fiction survival adventure for Android, built on Unity 6 (URP) with a Node.js/Express/Supabase backend for accounts, cloud saves, and leaderboards.

> **Status: functional greybox scaffold.** All core systems are implemented in code and
> scenes are generator-built placeholder environments. Characters/environments are primitive
> greybox shapes — production 3D art is the main remaining content task.

## 1. Project architecture

```
Null_Frontier_app/
├── Assets/
│   ├── Editor/                 # ProjectBootstrap (URP/quality tiers), SceneBuilder
│   │                           # (generates MainMenu + VerticalCity scenes), AndroidBuildSettings
│   └── Scripts/
│       ├── Core/               # GameManager, SaveSystem, BackendClient, SceneLoader,
│       │                       # Health, IDamageable/DamageInfo, ObjectPool, GameEvents
│       ├── Characters/         # CharacterDefinition + CharacterRegistry (6 roster members)
│       ├── Player/             # PlayerController, camera rig, stamina, input providers
│       │                       # (desktop + mobile), PlayerCharacter (death/respawn)
│       ├── Combat/             # Weapon definitions, hitscan/projectile/melee weapons,
│       │                       # 6 abilities, weapon loadout, PlayerCombat
│       ├── AI/                 # EnemyBase state machine (patrol/investigate/chase/attack/
│       │                       # retreat/stun), SecurityDrone, DigitalSoldier, MazeStalker
│       ├── Missions/           # MissionManager, 9 seeded missions, world markers
│       ├── Maze/               # MazeGenerator (guaranteed solution), builder, timed escape
│       ├── Items/              # InventorySystem, 15 seeded items, consumables
│       ├── Progression/        # XP/levels/skill points, PlayerStats, skill tree
│       └── UI/                 # Main menu, character select, HUD, pause, settings,
│                               # loading screen, auth panel, joystick/touch buttons
├── Packages/manifest.json      # URP 17, Input System, Cinemachine 3, AI Navigation, Addressables
├── ProjectSettings/            # ProjectVersion, TagManager (CoverPoint tag)
└── server/                     # Express 4 + @supabase/supabase-js backend
    ├── db/schema.sql           # Tables + RLS + profile trigger
    └── src/                    # auth, saves, leaderboards routes; JWT middleware
```

Design notes:

- **Input**: `PlayerInputState` snapshot filled per frame by an `IInputProvider`
  (desktop keyboard/mouse/gamepad via Input System, or mobile joystick + touch buttons).
  Gameplay code never touches raw input.
- **Damage**: everything damageable implements `IDamageable` (`Health` for player/enemies).
  `DamageInfo` carries amount/crit/point/direction/source/type.
- **Missions**: world systems raise `MissionManager.NotifyEvent(type, targetId)`;
  the manager tracks objectives, grants XP/items, persists completion.
- **Saves**: `SaveSystem` writes JSON atomically (temp file + `.bak`), quarantines corrupt
  files as `.corrupt`, never overwrites good data with empty data, and mirrors to the
  backend when authenticated.

## 2. Technologies and dependencies

| Layer | Tech |
|---|---|
| Engine | Unity 6000.0.x LTS, URP 17, Cinemachine 3, Input System, AI Navigation, Addressables |
| Language | C# (.NET Standard 2.1) |
| Backend | Node.js 20+, Express 4, zod, helmet, cors, express-rate-limit |
| Database | Supabase (PostgreSQL + Auth + RLS) |
| Deploy targets | Android APK/AAB; backend on Render |

Install Unity with the **Android Build Support** module (includes SDK/NDK).

## 3. Features implemented

- Main menu flow: title, new game, continue (disabled without save), settings, multiplayer gate
- Character selection from the 6-member roster with per-character stats and abilities
- Third-person controller: walk/run/sprint (stamina), jump, crouch, dodge with i-frames
- Cinemachine third-person camera with aim FOV
- Mobile controls: floating joystick, touch buttons, right-half camera drag
- Combat: hitscan + projectile + melee weapons, crits, recoil, ammo/reload, 3-hit combos
- Abilities: tactical dash, holographic decoy, energy shield, enhanced scanning, EMP pulse, temp overdrive
- Enemy AI: patrol/investigate/chase/attack/retreat, line-of-sight + hearing, stun, cover usage, 3 enemy types
- 9 missions with objective tracking and rewards
- Procedural maze generation with guaranteed solution + timed escape mode + leaderboard submit
- Inventory (stacking, equipment slots, rarity) with 15 seeded items and consumables
- Progression: XP curve, levels, skill points, 5-skill skill tree, stat recalculation from gear
- Save system: local JSON (atomic + backup + corruption quarantine) + optional cloud save
- Settings: graphics tier (Low/Medium/High/Ultra), resolution scale, FPS cap, volumes
- Backend: signup/signin (Supabase Auth JWT), cloud save, leaderboards, rate-limited, validated, RLS-enforced

## 4. Features still pending

- **All production art**: realistic character models, animations, environments, VFX, audio
  (current visuals are labelled greybox primitives)
- Boss encounters (boss framework not written)
- Regions B–E content production (Shifting Maze mechanics exist; Forest/Abandoned/Core scenes not built)
- Character customization studio (roster/select exists; customization UI not built)
- Multiplayer beyond accounts/saves/leaderboards (no Netcode/Photon integration yet)
- Friends/chat/party social features (DB tables exist, routes/UI pending)
- World map UI and fast travel
- Daily/weekly missions
- Device performance auto-detection
- Unity automated test suite (EditMode/PlayMode tests)

## 5. Tests performed and actual results

**Backend (executed on this machine, 2026-11-05):**

| Test | Result |
|---|---|
| `npm install` | PASS — 83 packages |
| `GET /api/health` (no config) | PASS — `200 {"ok":true}` |
| Auth/saves/leaderboard routes without Supabase env | PASS — graceful `503 {"error":"backend_not_configured"}` |
| Bad request body | PASS — `400` with field-level zod messages |
| Unknown route | PASS — `404 {"error":"not_found"}` |
| Invalid bearer token | PASS — `401 {"error":"unauthorized"}` |
| Wrong password (stub creds) | PASS — `401 {"error":"invalid_credentials"}` |

**Unity client: NOT YET COMPILED.** No Unity Editor was present when the code was written;
first compile happens on project open (see Known issues). Scene generation, URP bootstrap,
and APK build are scripted but must be executed inside the Editor.

## 6. Known issues

- C# has not passed a compiler yet — expect a small number of fix-on-first-open errors.
- Greybox scenes: enemies/weapons need prefab wiring in the Editor (prefab references are
  serialized and null-safe until assigned).
- Skill ranks are runtime-only (skill points persist; ranks do not).
- `shieldBonus` item stat is seeded but unused until a shield-max system lands.
- Drone hover assumes NavMesh bake accommodates flying units.
- `DigitalSoldier` cover search needs objects tagged `CoverPoint` (tag added to TagManager).

## 7. Running the project

### Unity client

1. Install Unity Hub → add **Unity 6000.0.x LTS** with **Android Build Support**.
2. Open `C:\Null_Frontier_app` in Hub. Wait for package resolution.
3. On first open, `ProjectBootstrap` creates URP assets + quality tiers automatically.
4. Menu **Null Frontier → Build All Scenes** (creates `Assets/Scenes/MainMenu.unity`
   and `Assets/Scenes/VerticalCity.unity` and adds them to Build Settings).
5. Open `MainMenu` and press Play.

### Backend

```bash
cd server
cp .env.example .env       # fill SUPABASE_URL and SUPABASE_ANON_KEY
npm install
npm start                  # http://localhost:3000
```

Without `.env` the server boots and answers `/api/health`; everything else returns 503.

## 8. Database configuration

1. Create a project at supabase.com → Project Settings → API → copy **Project URL** and **anon public** key.
2. Supabase SQL Editor → paste `server/db/schema.sql` → run. Creates `profiles`,
   `player_saves`, `leaderboard_entries`, `friends`, `achievements` with RLS policies
   and the new-user profile trigger.
3. Authentication → Providers: Email enabled. If "Confirm email" is on, signups return
   `403 email_confirmation_required` until confirmed.

## 9. Building and installing the APK

In the Editor: **Null Frontier → Build → Android APK** → output at `Builds/NullFrontier.apk`.
Copy to the phone, allow "install from unknown sources", install.

Debug keystore is used automatically — configure a real keystore
(PlayerSettings → Publishing Settings) before any store release. AAB:
**Null Frontier → Build → Android AAB**.

## 10. Deploying the backend (Render)

1. Push the repo; create a Render Web Service with **Root Directory = `server`**.
2. Build Command `npm install`, Start Command `npm start`.
3. Env vars: `SUPABASE_URL`, `SUPABASE_ANON_KEY`, `CORS_ORIGIN` (`*` is acceptable —
   auth is Bearer-based). `PORT` is injected by Render.
4. Verify `https://<app>.onrender.com/api/health`, then point the client's
   `BackendClient.apiBaseUrl` at it.
