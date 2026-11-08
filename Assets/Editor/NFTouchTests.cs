using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NullFrontier.Combat;
using NullFrontier.Core;
using NullFrontier.Player;
using NullFrontier.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Temporary mobile-input acceptance driver. It plays VerticalCity with a virtual
    /// Touchscreen device and real EnhancedTouch events, so the deck, the joystick and
    /// the touch look drag run through the same device path an Android build uses.
    /// The provider is selected by hand because Android's isMobilePlatform flag cannot
    /// be faked in the editor: the desktop provider is disabled and the mobile one is
    /// assigned to the controller and camera exactly as Start would have.
    /// A virtual device is still not a phone, so the log never claims a device test.
    /// </summary>
    [InitializeOnLoad]
    public static class NFTouchTests
    {
        private static readonly string TempDir = Path.GetTempPath();
        private static readonly string Trigger = Path.Combine(TempDir, "nf_touch_trigger.txt");
        private static readonly string LogPath = Path.Combine(TempDir, "nf_touch_status.txt");
        private const string PhaseKey = "NFTouch.Phase";
        private const string StepKey = "NFTouch.Step";
        private const string NextKey = "NFTouch.Next";
        private const string PassKey = "NFTouch.Pass";
        private const string FailKey = "NFTouch.Fail";
        private const string ErrorsKey = "NFTouch.Errors";
        private const string PhaseStartKey = "NFTouch.PhaseStart";
        private const string AmmoKey = "NFTouch.AmmoBefore";
        private const string IndexKey = "NFTouch.WeaponIndex";
        private const string ReloadKey = "NFTouch.ReloadWait";
        private const string JumpKey = "NFTouch.JumpBaseY";
        private const string MoveKey = "NFTouch.MoveStart";
        private const string MoveZKey = "NFTouch.MoveStartZ";
        private const string YawKey = "NFTouch.YawBefore";

        private const int TouchId = 1;
        private static bool _busy;
        private static Touchscreen _touchscreen;

        static NFTouchTests()
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
        private static float PhaseStart { get => SessionState.GetFloat(PhaseStartKey, 0f); set => SessionState.SetFloat(PhaseStartKey, value); }
        private static int AmmoBefore { get => SessionState.GetInt(AmmoKey, -1); set => SessionState.SetInt(AmmoKey, value); }
        private static int WeaponIndex { get => SessionState.GetInt(IndexKey, -1); set => SessionState.SetInt(IndexKey, value); }
        private static float ReloadWait { get => SessionState.GetFloat(ReloadKey, 1.6f); set => SessionState.SetFloat(ReloadKey, value); }
        private static float JumpBaseY { get => SessionState.GetFloat(JumpKey, 0f); set => SessionState.SetFloat(JumpKey, value); }
        private static float MoveStartX { get => SessionState.GetFloat(MoveKey, 0f); set => SessionState.SetFloat(MoveKey, value); }
        private static float MoveStartZ { get => SessionState.GetFloat(MoveZKey, 0f); set => SessionState.SetFloat(MoveZKey, value); }
        private static float YawBefore { get => SessionState.GetFloat(YawKey, 0f); set => SessionState.SetFloat(YawKey, value); }

        private static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition.StartsWith("[NFTouchTests]")) return;
            if (condition.Contains("Exception thrown while invoking")) return;

            string errors = Errors;
            if (errors.Length < 12000) Errors = errors + "[" + type + "] " + condition + "\n";
        }

        private static void Tick()
        {
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
            Write($"playmode {change} phase={Phase} step={Step}");
            if (change != PlayModeStateChange.EnteredEditMode) return;
            if (Phase <= 0) return;

            if (Step < 99)
                Write($"play exited at phase={Phase} step={Step} pass={Passed} fail={Failed}");

            Write($"SUMMARY pass={Passed} fail={Failed}");
            Write($"console errors during run: {(Errors.Length == 0 ? "none" : Errors)}");
            Write("android device: NOT TESTED - virtual Touchscreen device in the editor, no hardware attached");
            Write("=== touch run complete ===");
            Phase = 0;
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

                Write("=== mobile input run start ===");
                if (SceneManager.GetActiveScene().path != "Assets/Scenes/VerticalCity.unity")
                    EditorSceneManager.OpenScene("Assets/Scenes/VerticalCity.unity", OpenSceneMode.Single);

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
            if (Phase <= 0) return;

            Application.runInBackground = true;

            float now = Time.realtimeSinceStartup;
            if (PhaseStart <= 0f) PhaseStart = now;
            if (now < NextAt) return;

            try
            {
                StepTouch(now);
            }
            catch (Exception e)
            {
                Check("harness.exception", false, e.GetType().Name + ": " + e.Message);
                Write("EXCEPTION " + e);
                EditorApplication.isPlaying = false;
            }
        }

        private static void StepTouch(float now)
        {
            var player = FindPlayer();
            var loadout = player != null ? player.GetComponentInChildren<WeaponLoadout>(true) : null;
            if (player == null || loadout == null)
            {
                if (now - PhaseStart > 30f) Fail("VerticalCity player", now);
                return;
            }

            switch (Step)
            {
                case 0:
                    SetupMobileProvider(player);
                    Advance(1, 0.5f, now);
                    break;

                case 1:
                    var deck = UnityEngine.Object.FindObjectsByType<TouchButton>(FindObjectsSortMode.None)
                        .Where(b => b.gameObject.activeInHierarchy).ToList();
                    Check("deck.buttons", deck.Count == 10, deck.Count + " active TouchButtons");
                    Check("deck.joystick", FindFirst<VirtualJoystick>() != null, "VirtualJoystick present");
                    Check("deck.provider", MobileProvider != null, "MobileInputProvider resolved");
                    Capture("nf_touch_deck.png");
                    AmmoBefore = loadout.CurrentWeapon != null ? loadout.CurrentWeapon.CurrentAmmo : -1;
                    TouchDown(KeyPoint("Fire"));
                    Write($"touch down on FIRE at {KeyPoint("Fire")}");
                    Advance(2, 0.7f, now);
                    break;

                case 2:
                    TouchUp(KeyPoint("Fire"));
                    int afterFire = loadout.CurrentWeapon != null ? loadout.CurrentWeapon.CurrentAmmo : -1;
                    Check("touch.fire", AmmoBefore >= 0 && afterFire >= 0 && afterFire < AmmoBefore,
                        $"ammo {AmmoBefore} -> {afterFire} while the FIRE key was held");
                    WeaponIndex = loadout.CurrentIndex;
                    // A release and the next press are queued a frame apart: two touches
                    // sharing an id inside one update makes the second one disappear.
                    Advance(3, 0.15f, now);
                    break;

                case 3:
                    TouchDown(KeyPoint("Switch"));
                    Write($"touch down on SWAP at {KeyPoint("Switch")}");
                    Advance(4, 0.2f, now);
                    break;

                case 4:
                    TouchUp(KeyPoint("Switch"));
                    var switched = player.GetComponentInChildren<WeaponLoadout>(true);
                    Check("touch.switch",
                        switched != null && switched.CurrentIndex != WeaponIndex &&
                        switched.CurrentWeapon != null && switched.CurrentWeapon.gameObject.activeSelf,
                        $"index {WeaponIndex} -> {switched?.CurrentIndex}");
                    AmmoBefore = switched?.CurrentWeapon != null ? switched.CurrentWeapon.CurrentAmmo : -1;
                    Advance(5, 0.15f, now);
                    break;

                case 5:
                    if (AmmoBefore >= 0) TouchDown(KeyPoint("Fire"));
                    Advance(6, 0.7f, now);
                    break;

                case 6:
                    TouchUp(KeyPoint("Fire"));
                    var newWeapon = FindFirst<WeaponLoadout>(player)?.CurrentWeapon;
                    int newAmmo = newWeapon != null ? newWeapon.CurrentAmmo : -1;
                    Check("touch.fireAfterSwitch", AmmoBefore >= 0 && newAmmo >= 0 && newAmmo < AmmoBefore,
                        $"switched weapon ammo {AmmoBefore} -> {newAmmo}");
                    Check("touch.fireNewDefinition", newWeapon != null && newWeapon.Definition != null,
                        newWeapon?.Definition != null ? newWeapon.Definition.displayName : "no definition");
                    AmmoBefore = newAmmo;
                    ReloadWait = newWeapon?.Definition != null ? newWeapon.Definition.reloadTime : 1.6f;
                    Advance(7, 0.15f, now);
                    break;

                case 7:
                    TouchDown(KeyPoint("Reload"));
                    Write($"touch down on RELOAD at {KeyPoint("Reload")} (ammo {AmmoBefore})");
                    Advance(8, 0.2f, now);
                    break;

                case 8:
                    TouchUp(KeyPoint("Reload"));
                    Advance(9, ReloadWait + 0.6f, now);
                    break;

                case 9:
                    var reloaded = FindFirst<WeaponLoadout>(player)?.CurrentWeapon;
                    Check("touch.reload",
                        reloaded != null && reloaded.Definition != null &&
                        reloaded.CurrentAmmo == reloaded.Definition.magazineSize && AmmoBefore < reloaded.Definition.magazineSize,
                        $"ammo {AmmoBefore} -> {reloaded?.CurrentAmmo}/{reloaded?.Definition?.magazineSize} after the RELOAD key");
                    JumpBaseY = player.transform.position.y;
                    Advance(10, 0.15f, now);
                    break;

                case 10:
                    TouchDown(KeyPoint("Jump"));
                    Advance(11, 0.25f, now);
                    break;

                case 11:
                    TouchUp(KeyPoint("Jump"));
                    var mover = player.GetComponent<PlayerController>();
                    Check("touch.jump",
                        player.transform.position.y > JumpBaseY + 0.12f || (mover != null && !mover.IsGrounded),
                        $"y {JumpBaseY:0.00} -> {player.transform.position.y:0.00}, grounded={mover != null && mover.IsGrounded}");
                    Advance(12, 0.6f, now);
                    break;

                case 12:
                    MoveStartX = player.transform.position.x;
                    MoveStartZ = player.transform.position.z;
                    _stickOrigin = new Vector2(Screen.width * 0.18f, Screen.height * 0.3f);
                    TouchDown(_stickOrigin);
                    Write($"touch down inside the move zone at {_stickOrigin}");
                    Advance(13, 0.15f, now);
                    break;

                case 13:
                    SendTouch(_stickOrigin + new Vector2(0f, 150f), InputTouchPhase.Moved);
                    Advance(14, 0.6f, now);
                    break;

                case 14:
                    var stick = FindFirst<VirtualJoystick>();
                    Check("touch.joystickHeld",
                        stick != null && stick.IsHeld && stick.Value.magnitude > 0.3f,
                        $"IsHeld={stick?.IsHeld}, value={stick?.Value}");
                    float moved = Vector2.Distance(
                        new Vector2(MoveStartX, MoveStartZ),
                        new Vector2(player.transform.position.x, player.transform.position.z));
                    Check("touch.joystickMove", moved > 0.3f,
                        $"walked {moved:0.00} m with the left thumbstick");
                    TouchUp(_stickOrigin + new Vector2(0f, 150f));
                    Advance(15, 0.25f, now);
                    break;

                case 15:
                    var released = FindFirst<VirtualJoystick>();
                    Check("touch.joystickRelease",
                        released != null && !released.IsHeld && released.Value.magnitude < 0.01f,
                        $"IsHeld={released?.IsHeld}, value={released?.Value}");
                    YawBefore = ReadYaw();
                    Advance(16, 0.15f, now);
                    break;

                case 16:
                    LookDown();
                    Advance(17, 0.15f, now);
                    break;

                case 17:
                    LookMove();
                    Advance(18, 0.3f, now);
                    break;

                case 18:
                    TouchUp(_lookPos);
                    float yawNow = ReadYaw();
                    Check("touch.look", Mathf.Abs(Mathf.DeltaAngle(YawBefore, yawNow)) > 1.5f,
                        $"yaw {YawBefore:0.0} -> {yawNow:0.0} from a {LookDrag}px drag on the right half");
                    Advance(19, 0.2f, now);
                    break;

                case 19:
                    Capture("nf_touch_final.png");
                    Step = 99;
                    EditorApplication.isPlaying = false;
                    break;
            }
        }

        // ---------------------------------------------------------------- touch plumbing

        private static MobileInputProvider MobileProvider
        {
            get
            {
                var player = FindPlayer();
                return player != null ? player.GetComponent<MobileInputProvider>() : null;
            }
        }

        private static void SetupMobileProvider(GameObject player)
        {
            // The editor reports isMobilePlatform == false, so the desktop provider wins
            // the Start-time race no matter what a device would do. Add a virtual
            // Touchscreen first, then hand the mobile provider to both controllers the
            // way Start would have on a phone.
            if (Touchscreen.current == null) _touchscreen = InputSystem.AddDevice<Touchscreen>();
            else _touchscreen = Touchscreen.current;
            EnhancedTouchSupport.Enable();

            var desktop = player.GetComponent<DesktopInputProvider>();
            if (desktop != null) desktop.enabled = false;

            var mobile = player.GetComponent<MobileInputProvider>();
            var controller = player.GetComponent<PlayerController>();
            var camera = player.GetComponent<PlayerCameraController>();
            int injected = InjectProviderAll(mobile);

            Write($"virtual Touchscreen added: {_touchscreen != null}; desktop provider enabled={desktop != null && desktop.enabled}");
            Write($"provider injected into {injected} consumers; " +
                  $"{ReadProvider(controller)?.GetType().Name ?? "null"} (player) / " +
                  $"{ReadProvider(camera)?.GetType().Name ?? "null"} (camera)");
        }

        /// <summary>
        /// PlayerController, PlayerCameraController, PlayerCombat and AbilityController
        /// each cache their own provider in Start, so swapping only one of them would
        /// leave firing on the disabled desktop provider.
        /// </summary>
        private static int InjectProviderAll(IInputProvider provider)
        {
            int count = 0;
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                var field = behaviour.GetType().GetField("_provider", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null || !typeof(IInputProvider).IsAssignableFrom(field.FieldType)) continue;
                field.SetValue(behaviour, provider);
                count++;
            }
            return count;
        }

        private static IInputProvider ReadProvider(Component owner)
        {
            var field = owner?.GetType().GetField("_provider", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(owner) as IInputProvider;
        }

        private static void TouchDown(Vector2 pos) => SendTouch(pos, InputTouchPhase.Began);

        private static void TouchUp(Vector2 pos) => SendTouch(pos, InputTouchPhase.Ended);

        private static void SendTouch(Vector2 pos, InputTouchPhase phase)
        {
            if (_touchscreen == null) return;
            InputSystem.QueueStateEvent(_touchscreen, new TouchState
            {
                touchId = TouchId,
                position = pos,
                phase = phase,
                pressure = phase == InputTouchPhase.Ended ? 0f : 1f
            });
        }

        private static Vector2 KeyPoint(string keyName)
        {
            var button = UnityEngine.Object.FindObjectsByType<TouchButton>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.gameObject.name == keyName && b.gameObject.activeInHierarchy);
            if (button == null)
            {
                Check("key." + keyName, false, "TouchButton not found");
                return new Vector2(Screen.width * 0.8f, Screen.height * 0.3f);
            }

            var rt = button.transform as RectTransform;
            Vector3 world = rt.TransformPoint(rt.rect.center);
            return RectTransformUtility.WorldToScreenPoint(null, world);
        }

        private static Vector2 _stickOrigin;
        private static Vector2 _lookPos;
        private const int LookDrag = 120;

        private static void LookDown()
        {
            // The look drag must start where no HUD graphic answers the pointer, or the
            // provider's IsOverUi guard drops it before the delta is read.
            _lookPos = FreeLookPoint();
            TouchDown(_lookPos);
            Write($"look touch down at {_lookPos} (screen {Screen.width}x{Screen.height})");
        }

        private static void LookMove()
        {
            _lookPos += new Vector2(LookDrag, 0f);
            SendTouch(_lookPos, InputTouchPhase.Moved);
        }

        private static Vector2 FreeLookPoint()
        {
            var candidates = new[]
            {
                new Vector2(0.72f, 0.62f), new Vector2(0.8f, 0.72f), new Vector2(0.86f, 0.55f),
                new Vector2(0.66f, 0.78f), new Vector2(0.92f, 0.42f)
            };

            foreach (var candidate in candidates)
            {
                var pos = new Vector2(Screen.width * candidate.x, Screen.height * candidate.y);
                if (!IsOverUi(pos)) return pos;
            }

            Write("every look candidate sits under a HUD graphic; using the first one");
            return new Vector2(Screen.width * candidates[0].x, Screen.height * candidates[0].y);
        }

        private static bool IsOverUi(Vector2 screenPoint)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPoint };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            return hits.Any(h => h.gameObject.GetComponentInParent<Canvas>() != null);
        }

        private static float ReadYaw()
        {
            var camera = FindFirst<PlayerCameraController>();
            if (camera == null) return 0f;
            var field = typeof(PlayerCameraController).GetField("_yaw", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? (float)field.GetValue(camera) : 0f;
        }

        private static void Advance(int step, float delay, float now)
        {
            Step = step;
            NextAt = now + delay;
        }

        private static void Fail(string what, float now)
        {
            Check("timeout." + what, false, "waited 30 s in scene=" + SceneManager.GetActiveScene().name);
            EditorApplication.isPlaying = false;
        }

        private static void Check(string label, bool ok, string detail)
        {
            if (ok) Passed++; else Failed++;
            Write($"CHECK {(ok ? "PASS" : "FAIL")} {label} :: {detail}");
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

        private static void Capture(string file)
        {
            FocusGameView();
            EditorApplication.QueuePlayerLoopUpdate();
            string path = Path.Combine(TempDir, file);
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
            Debug.Log("[NFTouchTests] " + line);
        }
    }
}
