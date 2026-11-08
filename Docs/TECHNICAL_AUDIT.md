# NULL FRONTIER — Technical Audit

- **Commit audited:** `c5cd93b` (working tree clean at start, 2026-11-08)
- **Project:** `C:\Null_Frontier_app`
- **Engine:** Unity 6000.0.85f2, Universal Render Pipeline 17.0.4
- **Platform audited:** Editor on Windows (DX11). Android has **not** been tested on a physical device.
- **Method:** full read of 85 runtime scripts (9,825 lines) and 15 editor scripts, both
  scene files, URP settings, `ProjectSettings`, `Packages/manifest.json`, the Express
  backend and its SQL, the imported art packs, plus one real Play-mode capture of the
  main menu. No device profiling was possible.

This document records what exists, what is verified, and what is broken. It deliberately
does not repeat marketing language. Anything not listed under "Verified" is unverified.

---

## 1. Verified at this commit

| Item | Status | Evidence |
|---|---|---|
| Both scenes rebuild and save | Verified | `SceneBuilder.BuildAll()` log: "MainMenu scene built", "VerticalCity scene built" |
| Main menu renders in Play mode | Verified (editor, 1380×610 Game view) | `nf_game_play_v2.png`, `nf_game_play_v2b.png` |
| Runtime UI + menu character appear at runtime | Verified | Same captures (UI and character are spawned at runtime, not baked) |
| Earth day + cloud textures load | Verified visually | Textured planet visible through the gate |
| Portal ring, arcs, starfield, rain, wet street | Verified visually | Same captures |
| Profile card and menu no longer overlap | Verified | First capture had JAHRED/CONTINUE overlap; `v2` shows the card as the column header |
| No runtime errors during menu Play | Verified | Play error log empty after the `_BaseColorAddSubDiff` fix |
| C# compiles | Verified via offline Roslyn gate | `Temp/nfcheck/check.sh` → Assembly-CSharp / Assembly-CSharp-Editor, no `error CS` |
| Git checkpoint pushed | Verified | `c5cd93b`, `ls-remote` HEAD matches local |
| Inventory, combat, missions, maze, AI, save loads | **Not** tested this session | No gameplay session after the menu check |
| Any Android build | **Not** tested on a device | Existing `Builds/*.apk` are older debug builds |
| Multiplayer networking | **Does not exist** | See C2 |

### 1.1 Later verification pass (2026-11-08, editor Play)

An instrumented editor driver rebuilt both scenes, played them in sequence, captured the
real Game view, and logged scene state each run. The surviving captures are committed
under `Docs/Captures/`:

- `main_menu_2026-11-08.png` — menu Game view with full UI.
- `vertical_city_2026-11-08.png` — city Game view with HUD and framed player.

| Item | Result |
|---|---|
| `SceneBuilder.BuildAll()` | Returns success; both scenes save |
| Menu Play | 12 buttons present, active, interactable; 10 with persistent listeners, Continue/Co-op wired at runtime |
| Menu capture | Full UI over the live night stage; portrait, JAHred Lv. 3, six menu entries, logo, planet |
| City Play | Live HUD values (JAHred Lv. 3, mission "Missing Players", 32/32 ammo; hostile count 13 at 00:05 as spawns streamed in), enemy visible, player casts a shadow |
| Player placement | Visual feet at world Y≈0.31 on a Y=0 floor; body bounds centred at Y≈1.14 |
| Third-person camera | Rotation composer added; lens now pitches and frames the whole operative |
| Console during Play | No errors or exceptions after the teardown null-reference guards |
| C# compile | Offline Roslyn gate clean (`Assembly-CSharp`, `Assembly-CSharp-Editor`) |
| Android device | **Still not tested** — no APK install, no frame-time data |

---

## 2. Architecture map

```
Assets/Scripts
├── Core        Bootstrap, GameManager, SaveSystem, BackendClient, SceneLoader,
│               GameEvents, AudioBootstrap/AudioDirector, GraphicsQuality settings
├── Player      PlayerCharacter, PlayerController, Stamina, ThirdPersonCamera,
│               MobileInputProvider, CharacterVisualMount
├── Characters  CharacterDefinition/Registry, CharacterAbility
├── Combat      Weapon, WeaponLoadout, projectiles/tracers, damage, abilities,
│               enemy AI, spawners, boss logic
├── Items       ItemDefinition/Registry, InventorySystem, pickups, equip
├── Missions    MissionManager, objectives, mission board UI, 9 definitions
├── Maze        generation, shifting walls, escape objective
├── Progression XP/levels, achievements, stats, rewards
├── UI          ScreenManager, MainMenuController, HUD, Settings, CharacterSelect,
│               Auth, AchievementBoard, TouchButton, ScreenLink
└── AI          6 enemy-behaviour scripts

Assets/Editor    SceneBuilder (single source of truth for both scenes),
                 UiFactory (all runtime UI), ArtAssets, ProjectBootstrap,
                 CharacterPrefabs, WeaponAssets, CharacterPortraits, build pipeline
```

Both scenes contain the same bootstrap prefab set. `MainMenu` is the menu stage
(camera orbit, portal, Earth, character showcase). `VerticalCity` is the playable
region. `SceneBuilder.BuildAll()` regenerates both; scene files are committed.

Data flow:

```
SaveSystem.Data ──(inventory list)──> InventorySystem._items
      ▲                                      │
      └──────── GameEvents.SaveRequested ◄────┘ (Commit)
BackendClient.UploadCloudSave(Data) ──> Express /saves/cloud ──> Supabase
```

Two ordering gaps exist in this flow; see C1 and C5.

---

## 3. Findings

Severity is about player/data impact, not code style.

### CRITICAL

#### C1 — Inventory is wiped every time the menu opens

`InventorySystem.Awake()` calls `LoadFromSave()` (`Assets/Scripts/Items/InventorySystem.cs:39-47`).
`SaveSystem` never loads in `Awake`; the file is read later in
`MainMenuController.Start()` (`Assets/Scripts/UI/MainMenuController.cs:32-38`). Unity
guarantees every `Awake` runs before any `Start`, so the inventory hydrates from the
empty default `SaveData` and is then never refreshed.

**Impact:** a returning player sees an empty kit tray, and the next `Commit()` can write
that emptiness back into the save. This is silent data loss.
**Fix:** raise a `SaveLoaded` event from `SaveSystem.Load()`; have `InventorySystem`
re-hydrate on it, and load in `Start` only if the save is already loaded.

#### C2 — Co-op is presented as functional but no netcode exists

- Menu row "CO-OP" (`Assets/Editor/UiFactory.cs:1646`) opens a page whose body says
  "Co-op runs through the game backend: host it, sign in ... then invite from here"
  (`UiFactory.cs:2421-2439`). There is no host, invite, session or transport code in
  the client.
- The row is enabled whenever the REST auth token exists (`MainMenuController.cs:46-51`).
  REST auth is not multiplayer.
- The "NETWORKED" badge claims "Run a co-op match" (`AchievementSystem.cs:99-104`) but no
  code ever sets `GameMode.Cooperative`; the badge is unobtainable.

**Impact:** violates the explicit "do not pretend bots/multiplayer" rule and misleads
players. **Fix:** keep the entry (user asked to preserve CO-OP) but make its page an
honest status page: "online co-op is not implemented in this build", with no fake
invite controls; remove the unobtainable badge; stop gating the entry on auth.

#### C3 — Five of six canon operatives are permanently locked; one is invented

`SaveData.unlockedCharacters` defaults to `{ "iidreuspade" }` (`SaveSystem.cs:27`).
`CharacterSelectUI` disables every other row (`CharacterSelectUI.cs:78-124`). Nothing in
the codebase ever adds an id (grep: only those two files reference the list), so
`andi`, `igris`, `joy`, `ursula` can never be selected.

The sixth roster entry is `vanta` / "Vince", an invented character
(`CharacterDefinition.cs:76-78`). The canon list contains Christian Loyd as the main
antagonist, who is absent from the game.

**Impact:** most of the cast is dead content, and an unauthorised character is in canon.
**Fix:** default-unlock and migrate the five canon heroes; remove the invented entry.
Christian Loyd needs an antagonist/boss implementation, not a playable roster row
(Stage 5).

#### C4 — Production backend address is hard-coded to localhost

`BackendClient.cs:18` and `SceneBuilder.cs:37` both use `http://localhost:3000/api`, and
that value is baked into both scene files (`MainMenu.unity` ~10155,
`VerticalCity.unity` ~31987). A phone build points at itself, over cleartext HTTP.

**Impact:** account, cloud saves and any future online feature cannot work on a device;
cleartext traffic is blocked by modern Android network security anyway.
**Fix:** one configurable HTTPS base URL, no localhost outside development, with a
clear "offline" state when it is empty. Android's `insecureHttpOption` stays `0`.

#### C5 — Save/cloud integrity is not production-safe

- `StartNewSave()` writes an empty save immediately (`SaveSystem.cs:85-89`) and
  `WriteSave()` unconditionally uploads it (`SaveSystem.cs:172-184`), so choosing
  NEW GAME overwrites the only cloud save with blank data.
- There is exactly one save slot (`nullfrontier_save.json`; backend PK is `user_id`), no
  cloud download, no merge and no conflict resolution. `FetchCloudSave` is never called.
- `.bak` is written but never read; a corrupt primary falls back to defaults.
- Auth token is memory-only and there is no refresh token; every app launch requires a
  new sign-in.

**Impact:** lost progress and a cloud backup that can be silently destroyed.
**Fix:** explicit slot model, download-before-upload, `.bak` recovery, and an auth
refresh path. This is a staged item; the local load-order fix (C1) lands first.

### HIGH

#### H1 — Post-processing is enabled in the camera but has no resources

Camera post-processing is on (`MainMenu.unity:25652`), a global Volume with ACES, Bloom
and Color Adjustments exists, but `NullFrontier_Renderer.asset:31` has
`postProcessData: {fileID: 0}`. `ProjectBootstrap.cs:36` creates the renderer with
`ScriptableObject.CreateInstance<UniversalRendererData>()`, which does not assign URP's
`PostProcessData` asset. The pipeline therefore skips post-processing entirely.

`ProjectBootstrap.EnsureConfigured()` also returns early whenever a default pipeline is
already assigned (`ProjectBootstrap.cs:27-30`), so the broken asset is never repaired and
the quality tiers are never refreshed.

**Impact:** no ACES tonemapping, no bloom, no colour grade — a large part of the
requested cinematic look is missing. **Fix:** assign `PostProcessData` on the renderer,
make bootstrap repair idempotently, and verify bloom/ACES in a capture.

#### H2 — CC BY Earth attribution is not shipped

`Assets/Art/CREDITS.txt` correctly records Kenney (CC0), Quaternius (claimed CC0, but
the downloaded archive has no licence file) and Solar System Scope (CC BY 4.0, attribution
required). The file is not referenced by any scene, is not copied to `StreamingAssets`,
and the APK contents do not include it. There is no credits screen.

**Impact:** licence non-compliance for the CC BY Earth/cloud maps, plus unverifiable
provenance for the character pack. **Fix:** in-game credits page, `StreamingAssets`
copy, and a documented Quaternius provenance decision.

#### H3 — Reloading then switching weapons leaves the new weapon unable to fire

`Weapon.Reload()` starts a coroutine on the weapon component (`Weapon.cs:40-54`);
`WeaponLoadout` disables/deactivates the old weapon on switch (`WeaponLoadout.cs:59-65`).
Disabling the GameObject stops the coroutine before `IsReloading = false`, and the flag
is not reset by `OnEnable`, so the weapon is permanently stuck in "reloading".

**Impact:** soft-lock of the primary combat verb. **Fix:** cancel/reset reload in
`OnDisable`/`OnEnable`, or run reload on the loadout.

#### H4 — Mobile build has no weapon-switch control

`MobileInputProvider` wires fire/aim/reload/ability/dodge (`MobileInputProvider.cs:60-75`)
but no next/previous weapon action. Keyboard input has switch keys. A phone player can
only use the starting weapon.

**Impact:** loadout progression is inaccessible on the primary platform.
**Fix:** add a switch button to the touch deck and wire it to the same action.

#### H5 — Several stats are decorative

`MaxShield`, `DefenseMultiplier` and the mobility bonus are defined per character and
displayed, but not applied in damage or movement code. Skill ranks are not persisted.
**Impact:** character choice is cosmetic where it claims to be mechanical.
**Fix:** wire the stats into `PlayerStats`/damage and persist skill ranks, or label them
as coming soon. Prefer wiring.

### MEDIUM

| ID | Finding | Evidence |
|---|---|---|
| M1 | Quality tiers are non-monotonic: Low 1.0, Medium 1.0, High 0.84, Very High 0.92, Ultra 1.0 | `Assets/Settings/NullFrontier_URP_Tier*.asset` |
| M2 | Adaptive performance is inert; no LODGroups or occlusion culling in either scene | scene scan; 350 city renderers |
| M3 | High-detail tier still uses Gamma colour space | `ProjectSettings.asset:50` (`m_ActiveColorSpace: 0`) |
| M4 | Touch taps can be missed: `TouchButton` relies on `OnPointerUp` timing | `TouchButton.cs:16-25` |
| M5 | Screen history grows forever (`ScreenManager`/`ScreenLink` push without cap) | `ScreenLink.cs:14` |
| M6 | Backend: permissive CORS, rate limit only on auth, 2 MB body before auth, raw Supabase errors returned | `server/src/*` |
| M7 | No anti-cheat, audit log, or server-side reward validation | backend review |
| M8 | Duplicate-username trigger bug in schema; schema not applied to a live database | `server/db/schema.sql` |
| M9 | No refresh token; token lost on relaunch | `BackendClient.cs` |
| M10 | `server/.env` absent, so `/auth/*` returns 503 in this checkout | backend review |

### LOW

| ID | Finding |
|---|---|
| L1 | `README.md` is stale and does not describe the current build |
| L2 | No `.gitattributes`/LFS (files are small today, but binary APKs are committed) |
| L3 | Debug-signed APKs in `Builds/`; expected `Builds/NullFrontier.apk` absent |
| L4 | No tests, no test framework in `Packages/manifest.json`, no CI |
| L5 | `CREDITS.txt` names a licence file the Quaternius archive does not contain |

---

## 4. Backend and security audit

**Exists:** Express 4 REST API with Supabase. The mobile client uses the **anon key**
plus the caller's JWT; no service-role key is present in the Unity project (correct).
Endpoints: `/auth/signup`, `/auth/signin`, `/saves/cloud`, `/leaderboards`. RLS policies
are defined for the save, profile and leaderboard tables.

**Reasons this is not production-ready**

1. No download/merge path is used by the client, so the "cloud save" is upload-only.
2. Saves are overwritten unconditionally, including by a blank NEW GAME save (C5).
3. No refresh token, no server-side session revocation.
4. No server-side validation of rewards/achievements/leaderboard scores (anti-cheat).
5. CORS is permissive; only auth is rate-limited; JSON body limit is applied before
   authentication; Supabase error text is returned to the client.
6. No structured audit log or abuse detection.
7. The schema has not been applied to a provisioned Supabase project, and there is no
   `.env` in this checkout (so auth is offline/503 until configured).
8. Scenes still bake `http://localhost:3000/api` (C4).

**Secrets:** the repo contains no service-role key, no `.env`, and no credentials. Keep
it that way; the server key belongs only in the host's environment.

---

## 5. Art, licensing, performance

- All 3D art is imported CC0/low-poly: Kenney City Kits (roads, industrial) and the
  Quaternius Ultimate Animated Character Pack. The Earth/cloud maps are Solar System
  Scope CC BY 4.0 (NASA Blue Marble derived).
- Materials are URP Lit except emission-dependent surfaces, which use URP Unlit because
  `_EMISSION` does not render in this project (documented workaround, not a fix).
- Textures are small (Earth 2K), meshes are low-poly, and both scenes have no LODGroups
  and no occlusion culling. 350 renderers in VerticalCity, 64 in MainMenu, one 128 px
  reflection probe in the menu.
- No frame-time, memory, draw-call, thermals or battery measurements exist for any
  device. The quality tiers are configuration, not tested profiles.
- The main-menu capture is 1380×610 (≈2.26:1). Phone aspect ratios and notches have not
  been tested.

**Honest visual assessment:** the menu now has a readable composition (player card,
six menu entries, animated portal + textured Earth, neon avenue) and the city has
four-sided glass facades with lit office windows, a re-graded daylight key and a
grounded, framed player. It is still a **stylised low-poly scene**, not photoreal. The
operative is the Quaternius humanoid with a procedural gear kit, not a realistic human;
the kit buildings are box masses. Reaching the "AAA realistic" target requires realistic
licensed assets (characters, city, station), art direction, and device profiling — see
the roadmap's external requirements. Any paid asset requires approval first.

---

## 6. Testing and CI

There is no `Assets/Tests` folder, no assembly definitions, and `com.unity.test-framework`
is only present transitively. Nothing is covered by automated tests. Verification so far
has been manual: compile gate, in-editor Play capture of the menu, and zero-error logs.

Every gameplay claim must be tested after the fixes in this audit. The first test
scaffold should cover save load ordering, roster unlocks, mission completion, weapon
reload/switch, graphics tier monotonicity, and build settings.

---

## 7. External requirements (blocking items)

| Need | Why | Status |
|---|---|---|
| Hosted HTTPS backend + Supabase project | account, cloud saves, leaderboards | Not provisioned; no `.env` |
| Realistic character/city/station assets | the "photoreal AAA" visual target | Not available under CC0; needs approval or a free realistic source decision |
| Physical Android device(s) | any performance or input claim | None tested |
| Code signing keystore | release APK/AAB | Debug signing only |
| Multiplayer service/transport | real co-op | Not chosen; no netcode in client |

Per the project rules, no paid service or asset will be introduced without approval.

---

## 8. Definition-of-done status

| Feature | Implemented | Works in Unity | Tested | Notes |
|---|---|---|---|---|
| Main menu stage | Yes | Yes (editor Play) | Manual only | Low-poly art; no device |
| Menu UI | Yes | Yes | Manual only | Wide aspect only |
| Movement/combat | Yes | Camera + visual verified in Play | No | No scripted input test; reload bug H3 |
| Enemy AI | Yes | Not re-verified | No | — |
| Missions | Yes | Not re-verified | No | 9 definitions |
| Maze | Yes | Unreachable from menu flow | No | — |
| Save/load | Partial | Broken ordering (C1) | No | Single slot |
| Backend | Partial | Offline (no `.env`, localhost) | No | Not production-safe |
| Multiplayer | **No** | **No** | No | UI claims otherwise (C2) |
| Progression | Partial | Not re-verified | No | Badge unobtainable |
| Android build | Yes (old debug) | Not device-tested | No | Stale artifacts |

Conclusion: a solid prototype with a visually developed menu, not a completed game. The
fastest meaningful improvements are the data-loss and honesty fixes, restoring
post-processing, and a working test gate — before adding content or assets.
