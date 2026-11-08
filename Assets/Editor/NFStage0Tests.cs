using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NullFrontier.Characters;
using NullFrontier.Combat;
using NullFrontier.Core;
using NullFrontier.Items;
using NullFrontier.Missions;
using NullFrontier.Player;
using NullFrontier.Progression;
using NullFrontier.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Temporary Stage 0 acceptance driver. It plays the menu through the real buttons,
    /// walks the roster, deploys, then exercises switch/fire/reload through the Input
    /// System, saves and quits to the menu, and continues the save. Every claim is
    /// written as PASS/FAIL to a log; the player's own save is backed up and restored.
    /// </summary>
    [InitializeOnLoad]
    public static class NFStage0Tests
    {
        private static readonly string TempDir = Path.GetTempPath();
        private static readonly string Trigger = Path.Combine(TempDir, "nf_stage0_trigger.txt");
        private static readonly string LogPath = Path.Combine(TempDir, "nf_stage0_status.txt");
        private static readonly string ShotDir = TempDir;
        private const string MarkerItem = "data_cache";
        private const string TestCharacterId = "igris";
        private const string TestCharacterName = "John Steve";

        private const string PhaseKey = "NFStage0.Phase";
        private const string StepKey = "NFStage0.Step";
        private const string NextKey = "NFStage0.Next";
        private const string PassKey = "NFStage0.Pass";
        private const string FailKey = "NFStage0.Fail";
        private const string ErrorsKey = "NFStage0.Errors";
        private const string HadSaveKey = "NFStage0.HadSave";
        private const string HadBakKey = "NFStage0.HadBak";
        private const string PhaseStartKey = "NFStage0.PhaseStart";
        private const string StartIndexKey = "NFStage0.StartIndex";
        private const string AmmoKey = "NFStage0.AmmoBefore";
        private const string AmmoAfterKey = "NFStage0.AmmoAfterFire";
        private const string ReloadKey = "NFStage0.ReloadTime";
        private const string LastTickKey = "NFStage0.LastTick";
        private const string MarkerQtyKey = "NFStage0.MarkerQty";
        private const string MarkerBeforeKey = "NFStage0.MarkerBefore";
        private const string PosXKey = "NFStage0.PosX";
        private const string PosYKey = "NFStage0.PosY";
        private const string PosZKey = "NFStage0.PosZ";
        private const string ApproachXKey = "NFStage0.ApproachX";
        private const string ApproachYKey = "NFStage0.ApproachY";
        private const string ApproachZKey = "NFStage0.ApproachZ";
        private const string LookYawKey = "NFStage0.LookYaw";
        private const string JumpYKey = "NFStage0.JumpBaseY";
        private const string DodgeXKey = "NFStage0.DodgeX";
        private const string DodgeZKey = "NFStage0.DodgeZ";
        private const string HealthKey = "NFStage0.HealthBefore";
        private const string ShieldKey = "NFStage0.ShieldBefore";
        private const string LevelKey = "NFStage0.SavedLevel";
        private const string XpKey = "NFStage0.SavedXp";

        private static bool _busy;

        static NFStage0Tests()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Application.logMessageReceived += OnLog;
        }

        private static int Phase { get => SessionState.GetInt(PhaseKey, 0); set => SessionState.SetInt(PhaseKey, value); }
        private static int Step { get => SessionState.GetInt(StepKey, 0); set => SessionState.SetInt(StepKey, value); }
        private static float NextAt { get => SessionState.GetFloat(NextKey, 0f); set => SessionState.SetFloat(NextKey, value); }
        private static int Passed { get => SessionState.GetInt(PassKey, 0); set => SessionState.SetInt(PassKey, value); }
        private static int Failed { get => SessionState.GetInt(FailKey, 0); set => SessionState.SetInt(FailKey, value); }
        private static string Errors { get => SessionState.GetString(ErrorsKey, string.Empty); set => SessionState.SetString(ErrorsKey, value); }
        private static bool HadSave { get => SessionState.GetBool(HadSaveKey, false); set => SessionState.SetBool(HadSaveKey, value); }
        private static bool HadBak { get => SessionState.GetBool(HadBakKey, false); set => SessionState.SetBool(HadBakKey, value); }
        private static float PhaseStart { get => SessionState.GetFloat(PhaseStartKey, 0f); set => SessionState.SetFloat(PhaseStartKey, value); }
        private static int StartIndex { get => SessionState.GetInt(StartIndexKey, -1); set => SessionState.SetInt(StartIndexKey, value); }
        private static int AmmoBefore { get => SessionState.GetInt(AmmoKey, -1); set => SessionState.SetInt(AmmoKey, value); }
        private static int AmmoAfter { get => SessionState.GetInt(AmmoAfterKey, -1); set => SessionState.SetInt(AmmoAfterKey, value); }
        private static float ReloadTime { get => SessionState.GetFloat(ReloadKey, 0f); set => SessionState.SetFloat(ReloadKey, value); }
        private static float LastTickAt { get => SessionState.GetFloat(LastTickKey, 0f); set => SessionState.SetFloat(LastTickKey, value); }

        private static string SaveFile => Path.Combine(Application.persistentDataPath, "nullfrontier_save.json");
        private static string BakFile => SaveFile + ".bak";
        private static string SaveBackup => SaveFile + ".stage0backup";
        private static string BakBackup => BakFile + ".stage0backup";

        private static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition.StartsWith("[NFStage0Tests]")) return;
            if (condition.Contains("Exception thrown while invoking")) return;

            string errors = Errors;
            if (errors.Length < 16000) Errors = errors + "[" + type + "] " + condition + "\n";
        }

        private static void Tick()
        {
            // A locked or heavily de-prioritised desktop starves EditorApplication.update,
            // which used to look like a hung run. Record the gap so the log shows it.
            float tickNow = (float)EditorApplication.timeSinceStartup;
            float lastTick = LastTickAt;
            LastTickAt = tickNow;
            if (lastTick > 0f && tickNow - lastTick > 10f && Phase > 0)
                Write($"editor stalled {tickNow - lastTick:0.0}s at phase={Phase} step={Step} " +
                      $"playing={EditorApplication.isPlaying}");

            if (_busy || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (File.Exists(Trigger))
                {
                    File.Delete(Trigger);
                    Run();
                }
                return;
            }

            // A trigger raised while a Play session is already running takes the session
            // over instead of being ignored: stopping first avoids starting a new run in
            // the middle of an existing play-mode transition.
            if (File.Exists(Trigger))
            {
                File.Delete(Trigger);
                Write($"trigger during Play; stopping at phase={Phase} step={Step}");
                EditorApplication.isPlaying = false;
                return;
            }

            if (EditorApplication.isPlaying) Watch();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // Every transition is logged: an unexplained EnteredEditMode is otherwise
            // indistinguishable from the harness deliberately stopping the run.
            Write($"playmode {change} phase={Phase} step={Step}");

            if (change != PlayModeStateChange.EnteredEditMode) return;
            if (Phase <= 0) return;

            if (Step < 99)
                Write($"play exited at phase={Phase} step={Step} pass={Passed} fail={Failed}");
            FinishRun();
        }

        private static void Run()
        {
            _busy = true;
            try
            {
                SessionState.EraseInt(PhaseKey);
                SessionState.EraseInt(StepKey);
                SessionState.EraseFloat(NextKey);
                Passed = 0;
                Failed = 0;
                Errors = string.Empty;
                HadSave = File.Exists(SaveFile);
                HadBak = File.Exists(BakFile);
                if (HadSave) File.Copy(SaveFile, SaveBackup, true);
                if (HadBak) File.Copy(BakFile, BakBackup, true);

                Write("=== Stage 0 run start ===");
                Write($"save present before run: {HadSave} (backed up: {HadSave})");

                if (SceneManager.GetActiveScene().path != "Assets/Scenes/MainMenu.unity")
                    EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);

                Phase = 1;
                Step = 0;
                NextAt = 0f;
                PhaseStart = 0f;
                FocusGameView();
                EditorApplication.isPlaying = true;
            }
            catch (Exception e)
            {
                Phase = 0;
                Write("EXCEPTION " + e);
            }
            finally
            {
                _busy = false;
            }
        }

        private static void Watch()
        {
            EditorApplication.QueuePlayerLoopUpdate();

            int phase = Phase;
            if (phase <= 0) return;

            // The editor throttles the player loop when the Game view loses focus, which
            // would stretch a fifteen-second run into minutes or freeze the input test.
            Application.runInBackground = true;

            float now = Time.realtimeSinceStartup;
            if (PhaseStart <= 0f) PhaseStart = now;
            if (now < NextAt) return;

            try
            {
                switch (phase)
                {
                    case 1: MenuStep(now); break;
                    case 2: WeaponStep(now); break;
                    case 3: ReturnMenuStep(now); break;
                    case 4: ContinueStep(now); break;
                    case 5: CreditsFinalStep(now); break;
                }
            }
            catch (Exception e)
            {
                Check("harness.exception", false, e.GetType().Name + ": " + e.Message);
                Write("EXCEPTION " + e);
                EditorApplication.isPlaying = false;
            }
        }

        // ------------------------------------------------------------------ phase 1

        private static void MenuStep(float now)
        {
            var menu = FindFirst<MainMenuController>();
            if (menu == null)
            {
                Timeout("MainMenuController", now);
                return;
            }

            var screens = ScreenManager.Instance;
            switch (Step)
            {
                case 0:
                    Write("phase 1: main menu");
                    LogInitialMenuState();
                    Click("Help", "Column");
                    Advance(1, 0.5f, now);
                    break;

                case 1:
                    Check("menu.help", IsOpen("Help"), ScreenDetail(screens));
                    Click("Credits", "Help");
                    Advance(2, 0.5f, now);
                    break;

                case 2:
                    Check("menu.credits", IsOpen("Credits"), ScreenDetail(screens));
                    CheckCreditsText();
                    // The first screenshot after entering Play can still hold the
                    // pre-Play frame, so the visible credits evidence is captured at the
                    // end of the run once the Game view is warm.
                    Show("Title");
                    Advance(3, 0.5f, now);
                    break;

                case 3:
                    Check("menu.return.title", IsOpen("Title"), ScreenDetail(screens));
                    Click("Settings", "Column");
                    Advance(4, 0.5f, now);
                    break;

                case 4:
                    Check("menu.settings", IsOpen("Settings"), ScreenDetail(screens));
                    Show("Title");
                    Click("Settings", "HexDock");
                    Advance(5, 0.5f, now);
                    break;

                case 5:
                    Check("menu.settings.hex", IsOpen("Settings"), ScreenDetail(screens));
                    Show("Title");
                    Click("Multiplayer", "Column");
                    Advance(6, 0.5f, now);
                    break;

                case 6:
                    Check("menu.coop", IsOpen("Multiplayer"), ScreenDetail(screens));
                    Show("Title");
                    Click("Account", "Column");
                    Advance(7, 0.5f, now);
                    break;

                case 7:
                    Check("menu.account", IsOpen("Account"), ScreenDetail(screens));
                    Show("Title");
                    Click("Rewards", "HexDock");
                    Advance(8, 0.5f, now);
                    break;

                case 8:
                    Check("menu.rewards", IsOpen("Rewards"), ScreenDetail(screens));
                    Show("Title");
                    Click("Badges", "HexDock");
                    Advance(9, 0.5f, now);
                    break;

                case 9:
                    Check("menu.badges", IsOpen("Badges"), ScreenDetail(screens));
                    Show("Title");
                    Click("Character", "Column");
                    Advance(10, 0.9f, now);
                    break;

                case 10:
                    Check("menu.operatives", IsOpen("CharacterSelect"), ScreenDetail(screens));
                    VerifyRoster();
                    Show("Title");
                    Advance(11, 0.4f, now);
                    break;

                case 11:
                    Check("menu.return.title.2", IsOpen("Title"), ScreenDetail(screens));
                    Click("NewGame", "Column");
                    Advance(12, 0.9f, now);
                    break;

                case 12:
                    Check("newgame.roster", IsOpen("CharacterSelect"), ScreenDetail(screens));
                    Check("newgame.save", SaveSystem.Instance != null && SaveSystem.Instance.HasLocalSave,
                        "HasLocalSave=" + (SaveSystem.Instance != null && SaveSystem.Instance.HasLocalSave));
                    var card = FindCard(TestCharacterName);
                    Check("newgame.card", card != null, TestCharacterName);
                    card?.onClick.Invoke();
                    Advance(13, 0.25f, now);
                    break;

                case 13:
                    var deploy = FindButton("Deploy", "Detail");
                    Check("newgame.deployReady", deploy != null && deploy.interactable,
                        "interactable=" + (deploy != null && deploy.interactable));
                    Click("Deploy", "Detail");
                    Write("deploy clicked; waiting for VerticalCity");
                    Phase = 2;
                    Step = 0;
                    PhaseStart = now;
                    NextAt = now + 0.5f;
                    break;
            }
        }

        private static void LogInitialMenuState()
        {
            var visible = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(b => b.gameObject.activeInHierarchy)
                .ToList();
            Write($"title buttons active: {visible.Count}");
            foreach (var button in visible)
            {
                var label = button.GetComponentInChildren<Text>();
                Write($"  button '{button.name}' label='{label?.text}' interactable={button.interactable} " +
                      $"persistent={button.onClick.GetPersistentEventCount()}");
            }

            var save = SaveSystem.Instance;
            Write($"local save={save != null && save.HasLocalSave}; " +
                  $"continue interactable={FindButton("Continue", "Column")?.interactable}");
        }

        private static void VerifyRoster()
        {
            // Runtime clones are renamed "Card(Clone)", and the deactivated template
            // under Templates is also called "Card": prefix matching is what isolates
            // the five live roster tiles.
            var cards = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(b => b.name.StartsWith("Card") && b.gameObject.activeInHierarchy)
                .ToList();
            Check("roster.count", cards.Count == 5, cards.Count + " cards");

            var ui = FindFirst<CharacterSelectUI>();
            var selectedField = typeof(CharacterSelectUI).GetField("_selected",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var confirmField = typeof(CharacterSelectUI).GetField("confirmButton",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var confirm = confirmField?.GetValue(ui) as Button;

            var seen = new List<string>();
            foreach (var card in cards)
            {
                var label = card.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(t => t.name == "Label");
                string display = label != null ? label.text : "?";
                card.onClick.Invoke();
                var def = selectedField?.GetValue(ui) as CharacterDefinition;
                Check("roster." + display, def != null && def.DisplayName == display,
                    def != null ? def.Id : "no selection");
                if (def != null) seen.Add(def.Id);
            }

            string[] canon = { "iidreuspade", "andi", "igris", "joy", "ursula" };
            bool all = canon.All(id => seen.Contains(id));
            Check("roster.allFive", all && cards.All(c => c.interactable), string.Join(",", seen));
            Check("roster.confirm", confirm != null && confirm.interactable,
                "interactable=" + (confirm != null && confirm.interactable));
        }

        private static void CheckCreditsText()
        {
            var credits = ScreenManager.Instance != null
                ? GameObject.Find("Credits")
                : null;
            string body = credits != null
                ? string.Join("\n", credits.GetComponentsInChildren<Text>(true).Select(t => t.text))
                : string.Empty;
            Check("credits.earthLicence",
                body.Contains("Solar System Scope") && body.Contains("CC BY 4.0"),
                body.Length + " chars");
            Check("credits.kenney", body.Contains("Kenney") && body.Contains("CC0"), "attribution present");
        }

        // ------------------------------------------------------------------ phase 2

        private static void WeaponStep(float now)
        {
            var player = FindPlayer();
            var loadout = player != null ? player.GetComponentInChildren<WeaponLoadout>(true) : null;
            var inventory = InventorySystem.Instance;

            if (loadout == null || inventory == null || SceneManager.GetActiveScene().name != "VerticalCity")
            {
                Timeout("VerticalCity player + loadout", now);
                return;
            }

            switch (Step)
            {
                case 0:
                    Write("phase 2: city");
                    var game = GameManager.Instance;
                    Check("deploy.character", game != null && game.SelectedCharacterId == TestCharacterId,
                        "SelectedCharacterId=" + (game != null ? game.SelectedCharacterId : "null"));
                    LogWeapons(loadout);

                    // The pickup is authored as a trigger on the cache itself, so putting
                    // the capsule on the cache and taking one step is what a player crossing
                    // the trigger does; the collect itself is a real E press.
                    var pickup = FindPickup();
                    if (pickup == null)
                    {
                        Check("pickup.found", false, "no unspent Pickup_" + MarkerItem + " in scene");
                        Advance(2, 0.2f, now);
                        break;
                    }

                    SavePosition(player.transform.position);
                    MarkerBefore = inventory.Count(MarkerItem);
                    Vector3 stand = pickup.transform.position;
                    stand.y = player.transform.position.y;
                    Teleport(player, stand);
                    Write($"teleported player onto cache approach at {stand} (setup for the walk-in)");
                    PressKey(Key.W);
                    Advance(1, 0.15f, now);
                    break;

                case 1:
                    ReleaseKeys();
                    bool inReach = InteractableObject.NearbyPrompt.ToUpperInvariant().Contains("DATA CACHE");
                    Check("pickup.prompt", inReach, "NearbyPrompt=" + InteractableObject.NearbyPrompt);
                    PressKey(Key.E);
                    Advance(2, 0.35f, now);
                    break;

                case 2:
                    ReleaseKeys();
                    int afterPickup = inventory.Count(MarkerItem);
                    Check("pickup.collect", afterPickup == MarkerBefore + 1,
                        $"{MarkerItem} {MarkerBefore} -> {afterPickup} via E on world cache");
                    MarkerQty = afterPickup == MarkerBefore + 1 ? afterPickup : -1;
                    Teleport(player, StoredPos);
                    Write("teleported player back to the city spawn line");
                    Advance(3, 0.35f, now);
                    break;

                case 3:
                    SaveApproach(player.transform.position);
                    PressKey(Key.W);
                    Advance(4, 0.5f, now);
                    break;

                case 4:
                    ReleaseKeys();
                    Check("movement.forward",
                        Vector3.Distance(ApproachPos, player.transform.position) > 0.6f,
                        "moved from " + ApproachPos + " to " + player.transform.position);
                    LookYaw = ReadYaw();
                    QueueLook(new Vector2(60f, 0f));
                    Advance(5, 0.25f, now);
                    break;

                case 5:
                    QueueLook(Vector2.zero);
                    float yawNow = ReadYaw();
                    Check("camera.look", Mathf.Abs(Mathf.DeltaAngle(LookYaw, yawNow)) > 1.5f,
                        $"yaw {LookYaw:0.0} -> {yawNow:0.0}");
                    JumpBaseY = player.transform.position.y;
                    PressKey(Key.Space);
                    Advance(6, 0.2f, now);
                    break;

                case 6:
                    ReleaseKeys();
                    float jumpY = player.transform.position.y;
                    var mover = player.GetComponent<PlayerController>();
                    bool airborne = mover == null || !mover.IsGrounded;
                    Check("movement.jump", jumpY > JumpBaseY + 0.15f || airborne,
                        $"y {JumpBaseY:0.00} -> {jumpY:0.00}, grounded={mover != null && mover.IsGrounded}");
                    Advance(7, 0.9f, now);
                    break;

                case 7:
                    DodgeStart = player.transform.position;
                    PressKey(Key.LeftAlt);
                    Advance(8, 0.15f, now);
                    break;

                case 8:
                    var controller = player.GetComponent<PlayerController>();
                    float dodgeDistance = Vector3.Distance(
                        new Vector3(DodgeStart.x, 0f, DodgeStart.z),
                        new Vector3(player.transform.position.x, 0f, player.transform.position.z));
                    Check("movement.dodge",
                        controller != null && controller.IsDodging && dodgeDistance > 0.4f,
                        $"IsDodging={controller?.IsDodging}, dash={dodgeDistance:0.00} m");
                    ReleaseKeys();
                    Advance(9, 0.45f, now);
                    break;

                case 9:
                    var health = player.GetComponent<Health>();
                    HealthBefore = health != null ? health.CurrentHealth : -1f;
                    ShieldBefore = health != null ? health.CurrentShield : -1f;
                    if (health != null)
                    {
                        Vector3 point = player.transform.position + Vector3.up * 1.05f;
                        health.TakeDamage(new DamageInfo(12f, point, player.transform.forward, player));
                    }
                    Write("applied 12 ballistic damage through Health.TakeDamage (no enemy in range guaranteed)");
                    Advance(10, 0.2f, now);
                    break;

                case 10:
                    var hurt = player.GetComponent<Health>();
                    float totalNow = (hurt?.CurrentHealth ?? 0f) + (hurt?.CurrentShield ?? 0f);
                    float totalBefore = Mathf.Max(0f, HealthBefore) + Mathf.Max(0f, ShieldBefore);
                    Check("health.damage", hurt != null && totalNow < totalBefore - 1f,
                        $"health {HealthBefore:0}/{ShieldBefore:0} shield -> {hurt?.CurrentHealth:0}/{hurt?.CurrentShield:0}, total {totalBefore:0} -> {totalNow:0}");
                    ProgressionSystem.Instance?.AddXp(15f);
                    Write("granted 15 XP through ProgressionSystem for the save/continue check");
                    var progression = ProgressionSystem.Instance;
                    SavedLevel = progression != null ? progression.Level : 1;
                    SavedXp = progression != null ? (int)progression.Xp : 0;
                    SaveSystem.Instance.SaveNow();
                    bool inFile = File.Exists(SaveFile) && File.ReadAllText(SaveFile).Contains(MarkerItem);
                    Check("inventory.saved", inFile, SaveFile + " contains " + MarkerItem);
                    StartIndex = loadout.CurrentIndex;
                    PressKey(Key.Tab);
                    Advance(11, 0.4f, now);
                    break;

                case 11:
                    ReleaseKeys();
                    var switched = FindFirst<WeaponLoadout>(player);
                    Check("weapon.switch",
                        switched != null && switched.CurrentIndex != StartIndex &&
                        switched.CurrentWeapon != null && switched.CurrentWeapon.gameObject.activeSelf,
                        $"index {StartIndex} -> {switched?.CurrentIndex}, active={switched?.CurrentWeapon?.gameObject.activeSelf}");
                    Advance(12, 0.35f, now);
                    break;

                case 12:
                    var combatLoadout = FindFirst<WeaponLoadout>(player);
                    if (combatLoadout?.CurrentWeapon != null)
                    {
                        AmmoBefore = combatLoadout.CurrentWeapon.CurrentAmmo;
                        ReloadTime = combatLoadout.CurrentWeapon.Definition != null
                            ? combatLoadout.CurrentWeapon.Definition.reloadTime
                            : 1.6f;
                        PressMouse();
                    }
                    Advance(13, 0.6f, now);
                    break;

                case 13:
                    ReleaseMouse();
                    var firedLoadout = FindFirst<WeaponLoadout>(player);
                    int ammoNow = firedLoadout?.CurrentWeapon != null ? firedLoadout.CurrentWeapon.CurrentAmmo : -1;
                    Check("weapon.fire", AmmoBefore >= 0 && ammoNow >= 0 && ammoNow < AmmoBefore,
                        $"ammo {AmmoBefore} -> {ammoNow}");
                    AmmoAfter = ammoNow;
                    ReleaseMouse();
                    Advance(14, 0.45f, now);
                    break;

                case 14:
                    var settledLoadout = FindFirst<WeaponLoadout>(player);
                    int settledAmmo = settledLoadout?.CurrentWeapon != null
                        ? settledLoadout.CurrentWeapon.CurrentAmmo : -2;
                    Check("weapon.fireReleased", settledAmmo == AmmoAfter,
                        $"ammo {AmmoAfter} -> {settledAmmo} once the trigger was released");
                    PressKey(Key.R);
                    Advance(15, 0.4f, now);
                    break;

                case 15:
                    var reloadLoadout = FindFirst<WeaponLoadout>(player);
                    bool reloading = reloadLoadout?.CurrentWeapon != null && reloadLoadout.CurrentWeapon.IsReloading;
                    Check("weapon.reload.start", reloading, "IsReloading=" + reloading);
                    ReleaseKeys();
                    Advance(16, Mathf.Max(0.5f, ReloadTime + 0.8f), now);
                    break;

                case 16:
                    var reloadedLoadout = FindFirst<WeaponLoadout>(player);
                    var weapon = reloadedLoadout?.CurrentWeapon;
                    bool full = weapon != null && !weapon.IsReloading &&
                                weapon.Definition != null && weapon.CurrentAmmo == weapon.Definition.magazineSize;
                    Check("weapon.reload.complete", full,
                        weapon != null ? $"ammo {weapon.CurrentAmmo}/{weapon.Definition.magazineSize}, reloading={weapon.IsReloading}" : "no weapon");
                    Capture("nf_stage0_city.png");
                    Advance(17, 0.7f, now);
                    break;

                case 17:
                    Click("Pause", null);
                    Advance(18, 0.5f, now);
                    break;

                case 18:
                    Check("pause.open", IsOpen("Pause") && Mathf.Approximately(Time.timeScale, 0f),
                        "screen=" + ScreenDetail(ScreenManager.Instance) + " timescale=" + Time.timeScale);
                    Click("Quit", "Pause");
                    Write("save & quit clicked; returning to menu");
                    Phase = 3;
                    Step = 0;
                    PhaseStart = now;
                    NextAt = now + 1.2f;
                    break;
            }
        }

        private static InteractableObject FindPickup()
        {
            foreach (var marker in InteractableObject.All)
                if (marker != null && !marker.IsComplete && marker.name == "Pickup_" + MarkerItem)
                    return marker;
            return null;
        }

        private static void Teleport(GameObject player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = position;
            if (controller != null) controller.enabled = true;
        }

        private static float ReadYaw()
        {
            var cam = FindFirst<PlayerCameraController>();
            var field = typeof(PlayerCameraController)
                .GetField("_yaw", BindingFlags.Instance | BindingFlags.NonPublic);
            return cam != null && field != null ? (float)field.GetValue(cam) : 0f;
        }

        private static void QueueLook(Vector2 delta)
        {
            if (Mouse.current == null) return;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState
            {
                position = Mouse.current.position.ReadValue(),
                delta = delta,
                buttons = 0
            });
        }

        private static void SavePosition(Vector3 position)
        {
            SessionState.SetFloat(PosXKey, position.x);
            SessionState.SetFloat(PosYKey, position.y);
            SessionState.SetFloat(PosZKey, position.z);
        }

        private static Vector3 StoredPos => new Vector3(
            SessionState.GetFloat(PosXKey, 0f),
            SessionState.GetFloat(PosYKey, 0f),
            SessionState.GetFloat(PosZKey, 0f));

        private static void SaveApproach(Vector3 position)
        {
            SessionState.SetFloat(ApproachXKey, position.x);
            SessionState.SetFloat(ApproachYKey, position.y);
            SessionState.SetFloat(ApproachZKey, position.z);
        }

        private static Vector3 ApproachPos => new Vector3(
            SessionState.GetFloat(ApproachXKey, 0f),
            SessionState.GetFloat(ApproachYKey, 0f),
            SessionState.GetFloat(ApproachZKey, 0f));

        private static int MarkerQty
        {
            get => SessionState.GetInt(MarkerQtyKey, 0);
            set => SessionState.SetInt(MarkerQtyKey, value);
        }

        private static int MarkerBefore
        {
            get => SessionState.GetInt(MarkerBeforeKey, 0);
            set => SessionState.SetInt(MarkerBeforeKey, value);
        }

        private static float LookYaw
        {
            get => SessionState.GetFloat(LookYawKey, 0f);
            set => SessionState.SetFloat(LookYawKey, value);
        }

        private static float JumpBaseY
        {
            get => SessionState.GetFloat(JumpYKey, 0f);
            set => SessionState.SetFloat(JumpYKey, value);
        }

        private static Vector3 DodgeStart
        {
            get => new Vector3(
                SessionState.GetFloat(DodgeXKey, 0f),
                0f,
                SessionState.GetFloat(DodgeZKey, 0f));
            set
            {
                SessionState.SetFloat(DodgeXKey, value.x);
                SessionState.SetFloat(DodgeZKey, value.z);
            }
        }

        private static float HealthBefore
        {
            get => SessionState.GetFloat(HealthKey, 0f);
            set => SessionState.SetFloat(HealthKey, value);
        }

        private static float ShieldBefore
        {
            get => SessionState.GetFloat(ShieldKey, 0f);
            set => SessionState.SetFloat(ShieldKey, value);
        }

        private static int SavedLevel
        {
            get => SessionState.GetInt(LevelKey, -1);
            set => SessionState.SetInt(LevelKey, value);
        }

        private static long SavedXp
        {
            get => SessionState.GetInt(XpKey, -1);
            set => SessionState.SetInt(XpKey, (int)value);
        }

        private static void LogWeapons(WeaponLoadout loadout)
        {
            Write($"loadout index={loadout.CurrentIndex} weapon={loadout.CurrentWeapon?.GetType().Name} " +
                  $"ammo={loadout.CurrentWeapon?.CurrentAmmo}");
        }

        // ------------------------------------------------------------------ phase 3

        private static void ReturnMenuStep(float now)
        {
            if (SceneManager.GetActiveScene().name != "MainMenu" || FindFirst<MainMenuController>() == null)
            {
                Timeout("MainMenu after quit", now);
                return;
            }

            var save = SaveSystem.Instance;
            int savedQty = 0;
            if (save?.Data?.inventory != null)
            {
                foreach (var entry in save.Data.inventory)
                    if (entry != null && entry.itemId == MarkerItem) savedQty = entry.quantity;
            }

            Check("return.saveLoaded", save != null && save.HasLocalSave && savedQty == MarkerQty,
                $"HasLocalSave={save?.HasLocalSave}, {MarkerItem}={savedQty}, expected={MarkerQty}");
            Check("return.progression",
                save?.Data != null && save.Data.level == SavedLevel && save.Data.xp == SavedXp,
                $"level={save?.Data?.level}/{SavedLevel} xp={save?.Data?.xp}/{SavedXp}");
            var continueButton = FindButton("Continue", "Column");
            Check("return.continueEnabled", continueButton != null && continueButton.interactable,
                "interactable=" + continueButton?.interactable);
            Capture("nf_stage0_menu_after_quit.png");
            continueButton?.onClick.Invoke();
            Write("continue clicked");
            Phase = 4;
            Step = 0;
            PhaseStart = now;
            NextAt = now + 1.5f;
        }

        // ------------------------------------------------------------------ phase 4

        private static void ContinueStep(float now)
        {
            var player = FindPlayer();
            var inventory = InventorySystem.Instance;
            if (player == null || inventory == null || SceneManager.GetActiveScene().name != "VerticalCity")
            {
                Timeout("continued city", now);
                return;
            }

            switch (Step)
            {
                case 0:
                    Write("phase 4: continued city");
                    Check("continue.inventory", inventory.Count(MarkerItem) == MarkerQty,
                        MarkerItem + "=" + inventory.Count(MarkerItem));
                    Check("continue.character",
                        GameManager.Instance != null &&
                        GameManager.Instance.SelectedCharacterId == TestCharacterId,
                        "SelectedCharacterId=" + GameManager.Instance?.SelectedCharacterId);
                    var progression = ProgressionSystem.Instance;
                    Check("continue.progression",
                        progression != null && progression.Level == SavedLevel && progression.Xp == SavedXp,
                        $"level={progression?.Level}/{SavedLevel} xp={progression?.Xp}/{SavedXp}");
                    Capture("nf_stage0_continue.png");
                    Advance(1, 1.0f, now);
                    break;

                case 1:
                    Click("Pause", null);
                    Advance(2, 0.5f, now);
                    break;

                case 2:
                    Check("continue.pause", IsOpen("Pause"), ScreenDetail(ScreenManager.Instance));
                    Click("Quit", "Pause");
                    Write("continue pause quit clicked; going to the credits page for final evidence");
                    Phase = 5;
                    Step = 0;
                    PhaseStart = now;
                    NextAt = now + 1.2f;
                    break;
            }
        }

        // ------------------------------------------------------------------ phase 5

        private static void CreditsFinalStep(float now)
        {
            if (SceneManager.GetActiveScene().name != "MainMenu" || FindFirst<MainMenuController>() == null)
            {
                Timeout("MainMenu for final credits", now);
                return;
            }

            switch (Step)
            {
                case 0:
                    Click("Help", "Column");
                    Advance(1, 0.5f, now);
                    break;

                case 1:
                    Check("menu.helpFinal", IsOpen("Help"), ScreenDetail(ScreenManager.Instance));
                    Click("Credits", "Help");
                    Advance(2, 0.7f, now);
                    break;

                case 2:
                    Check("credits.final", IsOpen("Credits") && ScreenManager.Instance != null &&
                        ScreenManager.Instance.IsOpen("Credits") &&
                        ScreenManager.Instance.HasScreen("Credits"),
                        ScreenDetail(ScreenManager.Instance));
                    CheckCreditsText();
                    Advance(3, 1.2f, now);
                    break;

                case 3:
                    Capture("nf_stage0_credits_final.png");
                    Advance(4, 0.4f, now);
                    break;

                case 4:
                    Write($"SUMMARY pass={Passed} fail={Failed}");
                    EditorApplication.isPlaying = false;
                    break;
            }
        }

        // ------------------------------------------------------------------ finish

        private static void FinishRun()
        {
            FlushErrors();
            RestoreSave();
            Write($"SUMMARY pass={Passed} fail={Failed}");
            Write("android device: NOT TESTED");
            Write("=== Stage 0 run complete ===");
            Phase = 0;
            Step = 0;
        }

        private static void RestoreSave()
        {
            try
            {
                if (HadSave && File.Exists(SaveBackup))
                {
                    File.Copy(SaveBackup, SaveFile, true);
                    File.Delete(SaveBackup);
                }
                else if (File.Exists(SaveFile))
                {
                    File.Delete(SaveFile);
                }

                if (HadBak && File.Exists(BakBackup))
                {
                    File.Copy(BakBackup, BakFile, true);
                    File.Delete(BakBackup);
                }
                else if (File.Exists(BakFile))
                {
                    File.Delete(BakFile);
                }

                Write("save restored to pre-run state");
            }
            catch (Exception e)
            {
                Write("save restore failed: " + e.Message);
            }
        }

        private static void FlushErrors()
        {
            string errors = Errors;
            if (errors.Trim().Length == 0)
            {
                Write("console errors during run: none");
                return;
            }

            Write("console errors during run:");
            foreach (string line in errors.Split('\n'))
                if (line.Trim().Length > 0) Write("  " + line.Trim());
        }

        // ------------------------------------------------------------------ helpers

        private static void Advance(int step, float delay, float now)
        {
            Step = step;
            NextAt = now + delay;
        }

        private static void Timeout(string what, float now)
        {
            if (now - PhaseStart < 20f) return;
            Check("timeout." + what, false,
                "waited 20 s in scene=" + SceneManager.GetActiveScene().name);
            EditorApplication.isPlaying = false;
        }

        private static void Check(string label, bool ok, string detail)
        {
            if (ok) Passed++; else Failed++;
            Write($"CHECK {(ok ? "PASS" : "FAIL")} {label} :: {detail}");
        }

        private static bool IsOpen(string screen) =>
            ScreenManager.Instance != null && ScreenManager.Instance.IsOpen(screen);

        private static string ScreenDetail(ScreenManager manager) =>
            manager == null ? "no ScreenManager" : "top=" + ScreenName(manager);

        private static string ScreenName(ScreenManager manager)
        {
            var field = typeof(ScreenManager).GetField("_stack", BindingFlags.Instance | BindingFlags.NonPublic);
            var stack = field?.GetValue(manager) as Stack<string>;
            return stack != null && stack.Count > 0 ? stack.Peek() : "none";
        }

        private static void Show(string screen) => ScreenManager.Instance?.Show(screen);

        private static Button FindButton(string name, string ancestor)
        {
            foreach (var button in AllButtons())
            {
                if (button.name != name) continue;
                if (ancestor != null && !HasAncestor(button.transform, ancestor)) continue;
                return button;
            }
            return null;
        }

        private static bool HasAncestor(Transform node, string name)
        {
            for (var t = node; t != null; t = t.parent)
                if (t.name == name) return true;
            return false;
        }

        private static IEnumerable<Button> AllButtons() =>
            Resources.FindObjectsOfTypeAll<Button>()
                .Where(b => b != null && b.gameObject.scene.IsValid() && b.gameObject.scene.isLoaded);

        private static Button FindCard(string displayName)
        {
            foreach (var card in AllButtons())
            {
                if (!card.name.StartsWith("Card") || !card.gameObject.activeInHierarchy) continue;
                var label = card.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(t => t.name == "Label");
                if (label != null && label.text == displayName) return card;
            }
            return null;
        }

        private static void Click(string name, string ancestor)
        {
            var button = FindButton(name, ancestor);
            if (button == null)
            {
                Check("click." + name, false, "button not found (ancestor=" + ancestor + ")");
                return;
            }
            button.onClick.Invoke();
        }

        private static GameObject FindPlayer()
        {
            try { return GameObject.FindWithTag("Player"); }
            catch { return null; }
        }

        private static T FindFirst<T>(GameObject owner = null) where T : Component
        {
            if (owner != null) return owner.GetComponentInChildren<T>(true);
            return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }

        private static void PressKey(Key key)
        {
            if (Keyboard.current == null) return;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        }

        private static void ReleaseKeys()
        {
            if (Keyboard.current == null) return;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        }

        private static void PressMouse()
        {
            if (Mouse.current == null) return;
            var state = new MouseState { position = Mouse.current.position.ReadValue(), buttons = 1 };
            InputSystem.QueueStateEvent(Mouse.current, state);
        }

        private static void ReleaseMouse()
        {
            if (Mouse.current == null) return;
            var state = new MouseState { position = Mouse.current.position.ReadValue(), buttons = 0 };
            InputSystem.QueueStateEvent(Mouse.current, state);
        }

        private static void Capture(string file)
        {
            FocusGameView();
            EditorApplication.QueuePlayerLoopUpdate();
            string path = Path.Combine(ShotDir, file);
            ScreenCapture.CaptureScreenshot(path);
            Write("captured " + path);
        }

        private static void FocusGameView()
        {
            var type = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (type == null) return;
            var window = EditorWindow.GetWindow(type, false, null, false);
            window.Show();
            window.Repaint();
            window.Focus();
        }

        private static void Write(string line)
        {
            try
            {
                File.AppendAllText(LogPath,
                    $"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}");
            }
            catch
            {
                // Diagnostics must never break the run.
            }
            Debug.Log("[NFStage0Tests] " + line);
        }
    }
}
