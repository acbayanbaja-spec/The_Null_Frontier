using NullFrontier.Core;
using NullFrontier.Missions;
using NullFrontier.Player;
using UnityEngine;

namespace NullFrontier.UI
{
    /// <summary>
    /// In-game HUD: health/shield/stamina bars, active mission tracker, and hit
    /// feedback. Polls component state each frame — cheap and safe before the
    /// art-heavy UI pass.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Bars")]
        [SerializeField] private UnityEngine.UI.Slider healthBar;
        [SerializeField] private UnityEngine.UI.Slider shieldBar;
        [SerializeField] private UnityEngine.UI.Slider staminaBar;

        [Header("Mission tracker")]
        [SerializeField] private UnityEngine.UI.Text missionTitle;
        [SerializeField] private UnityEngine.UI.Text missionObjectives;

        [Header("Feedback")]
        [SerializeField] private CanvasGroup damageFlash;
        [SerializeField] private float damageFlashDecay = 3f;

        [Header("Prompts")]
        [SerializeField] private UnityEngine.UI.Text interactPrompt;

        [Header("Toast")]
        [SerializeField] private UnityEngine.UI.Text toastLabel;
        [SerializeField] private float toastSeconds = 2.4f;

        [Header("Weapon")]
        [SerializeField] private UnityEngine.UI.Text weaponName;
        [SerializeField] private UnityEngine.UI.Text ammoText;

        [Header("Tactical strip")]
        [SerializeField] private UnityEngine.UI.Text clockText;
        [SerializeField] private UnityEngine.UI.Text hostileText;
        [SerializeField] private UnityEngine.UI.Text levelText;
        [SerializeField] private UnityEngine.UI.Slider xpBar;

        [Header("Field kit")]
        [SerializeField] private UnityEngine.UI.Text[] kitCounts;

        // Same three consumables SupplyPanel offers, in the same order.
        private static readonly string[] KitIds = { "med_gel", "shield_cell", "stim" };

        private Health _health;
        private Stamina _stamina;
        private MissionManager _missions;
        private float _toastUntil;
        private NullFrontier.Combat.PlayerCombat _combat;
        private NullFrontier.Combat.Weapon _trackedWeapon;
        private int _shownAmmo = -1;
        private int _shownMagazine = -1;
        private bool _shownReloading;
        private NullFrontier.Items.InventorySystem _inventory;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _health = player.GetComponent<Health>();
                _stamina = player.GetComponent<Stamina>();
                _combat = player.GetComponent<NullFrontier.Combat.PlayerCombat>()
                    ?? player.GetComponentInChildren<NullFrontier.Combat.PlayerCombat>(true);
            }

            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
            }

            _missions = MissionManager.Instance;
            if (_missions != null) _missions.OnMissionCompleted += HandleMissionCompleted;
            MissionManager.OnMissionEvent += HandleMissionEvent;

            _inventory = NullFrontier.Items.InventorySystem.Instance;
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged += RefreshKit;
                RefreshKit();
            }
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnDamaged -= HandleDamaged;
            if (_missions != null) _missions.OnMissionCompleted -= HandleMissionCompleted;
            MissionManager.OnMissionEvent -= HandleMissionEvent;
            if (_inventory != null) _inventory.OnInventoryChanged -= RefreshKit;
        }

        /// <summary>A kit the player never picked up reads as zero rather than a hole in
        /// the tray, because the number on screen is the number in the inventory.</summary>
        private void RefreshKit()
        {
            if (kitCounts == null || _inventory == null) return;
            for (int i = 0; i < kitCounts.Length && i < KitIds.Length; i++)
            {
                if (kitCounts[i] != null)
                    kitCounts[i].text = _inventory.Count(KitIds[i]).ToString();
            }
        }

        private void HandleDamaged(DamageInfo info) => FlashDamage();

        private void Update()
        {
            if (_health != null)
            {
                SetBar(healthBar, _health.CurrentHealth / Mathf.Max(1f, _health.MaxHealth));
                SetBar(shieldBar, _health.MaxShield > 0f
                    ? _health.CurrentShield / _health.MaxShield
                    : 0f);
            }
            if (_stamina != null)
            {
                SetBar(staminaBar, _stamina.Current / Mathf.Max(1f, _stamina.Max));
            }

            UpdateMissionTracker();
            UpdatePrompt();
            UpdateToast();
            UpdateWeapon();
            UpdateStatus();

            if (damageFlash != null && damageFlash.alpha > 0f)
            {
                damageFlash.alpha = Mathf.Max(0f, damageFlash.alpha - Time.deltaTime * damageFlashDecay);
            }
        }

        private string _lastPrompt;

        private void UpdatePrompt()
        {
            if (interactPrompt == null) return;
            string prompt = InteractableObject.NearbyPrompt;
            if (prompt == _lastPrompt) return;
            _lastPrompt = prompt;
            interactPrompt.text = prompt;
            interactPrompt.enabled = !string.IsNullOrEmpty(prompt);
        }

        private static void SetBar(UnityEngine.UI.Slider bar, float normalized)
        {
            if (bar != null) bar.value = Mathf.Clamp01(normalized);
        }

        private void UpdateMissionTracker()
        {
            if (missionTitle == null || missionObjectives == null) return;
            var manager = MissionManager.Instance;
            if (manager == null || manager.ActiveMission == null)
            {
                missionTitle.text = string.Empty;
                missionObjectives.text = string.Empty;
                return;
            }

            var mission = manager.ActiveMission;
            missionTitle.text = mission.title;

            var lines = new System.Text.StringBuilder();
            for (int i = 0; i < mission.objectives.Count; i++)
            {
                var objective = mission.objectives[i];
                bool complete = manager.IsObjectiveComplete(i);
                int progress = manager.GetProgress(i);
                string status = complete ? "[x]" : $"[{progress}/{objective.requiredCount}]";
                lines.AppendLine($"{status} {MissionText.Describe(objective)}");
            }
            missionObjectives.text = lines.ToString();
        }

        private void FlashDamage()
        {
            if (damageFlash != null) damageFlash.alpha = 0.6f;
        }

        // -------------------------------------------------------------------- weapon

        /// <summary>
        /// Polled rather than event-driven: changing weapon swaps the component that owns
        /// the ammo event, and the readout still has to show the new magazine next frame.
        /// </summary>
        private void UpdateWeapon()
        {
            if (weaponName == null && ammoText == null) return;

            var weapon = _combat != null ? _combat.CurrentWeapon : null;
            var definition = weapon != null ? weapon.Definition : null;

            if (weapon != _trackedWeapon)
            {
                _trackedWeapon = weapon;
                _shownAmmo = _shownMagazine = -1;
                if (weaponName != null)
                {
                    weaponName.text = definition != null
                        ? definition.displayName.ToUpperInvariant()
                        : "UNARMED";
                }
            }

            if (weapon == null || definition == null)
            {
                if (ammoText != null) ammoText.text = string.Empty;
                return;
            }

            int ammo = weapon.CurrentAmmo;
            int magazine = definition.magazineSize;
            bool reloading = weapon.IsReloading;
            if (ammo == _shownAmmo && magazine == _shownMagazine && reloading == _shownReloading) return;

            _shownAmmo = ammo;
            _shownMagazine = magazine;
            _shownReloading = reloading;

            if (ammoText == null) return;
            ammoText.text = reloading ? "RELOADING" : $"{ammo} / {magazine}";
            ammoText.color = ammo <= 0 ? new Color(1f, 0.36f, 0.33f)
                : ammo * 4 <= Mathf.Max(1, magazine) ? new Color(1f, 0.78f, 0.3f)
                : Color.white;
        }

        // ------------------------------------------------------------------ tactical strip

        private float _nextStatusAt;

        /// <summary>
        /// Quarter-second refresh: a hostile count that ticks every frame is a count that
        /// costs a scene query every frame, and nobody can read four digits a second.
        /// </summary>
        private void UpdateStatus()
        {
            if (Time.unscaledTime < _nextStatusAt) return;
            _nextStatusAt = Time.unscaledTime + 0.25f;

            if (clockText != null)
            {
                int elapsed = Mathf.FloorToInt(Time.timeSinceLevelLoad);
                clockText.text = $"{Mathf.Clamp(elapsed / 60, 0, 99):00}:{elapsed % 60:00}";
            }

            if (hostileText != null)
            {
                int live = 0;
                var hostiles = NullFrontier.AI.EnemyBase.Hostiles;
                for (int i = 0; i < hostiles.Count; i++)
                {
                    if (hostiles[i] != null && hostiles[i].IsAlive) live++;
                }
                hostileText.text = live.ToString();
            }

            var progression = NullFrontier.Progression.ProgressionSystem.Instance;
            if (progression == null) return;

            if (levelText != null) levelText.text = $"Lv. {progression.Level}";
            if (xpBar == null) return;

            long needed = NullFrontier.Progression.ProgressionSystem.XpNeededForLevel(progression.Level);
            xpBar.value = needed > 0 ? Mathf.Clamp01(progression.Xp / (float)needed) : 1f;
        }

        // ------------------------------------------------------------------ objective toast

        private void HandleMissionCompleted(MissionDefinition mission)
        {
            if (mission == null) return;
            ShowToast($"{mission.title}  ·  CONTRACT COMPLETE  (+{mission.xpReward} XP)");
            AudioDirector.Instance?.PlayMissionComplete();
        }

        /// <summary>
        /// Notifications arrive before the manager applies progress, so the toast names the
        /// objective without quoting a count - the tracker one line away holds the real numbers.
        /// </summary>
        private void HandleMissionEvent(MissionManager.MissionEventType type, string targetId, int count)
        {
            var mission = _missions != null ? _missions.ActiveMission : null;
            if (mission == null) return;

            for (int i = 0; i < mission.objectives.Count; i++)
            {
                var objective = mission.objectives[i];
                if (objective.type != type || objective.targetId != targetId) continue;
                ShowToast(MissionText.Describe(objective));
                AudioDirector.Instance?.PlayObjectivePing();
                return;
            }
        }

        private void ShowToast(string message)
        {
            if (toastLabel == null || string.IsNullOrEmpty(message)) return;
            toastLabel.text = message;
            toastLabel.enabled = true;
            var color = toastLabel.color;
            color.a = 1f;
            toastLabel.color = color;
            _toastUntil = Time.unscaledTime + Mathf.Max(0.5f, toastSeconds);
        }

        private void UpdateToast()
        {
            if (toastLabel == null || !toastLabel.enabled) return;
            float remaining = _toastUntil - Time.unscaledTime;
            if (remaining <= 0f)
            {
                toastLabel.enabled = false;
                return;
            }
            if (remaining >= 0.5f) return;
            var color = toastLabel.color;
            color.a = Mathf.Clamp01(remaining / 0.5f);
            toastLabel.color = color;
        }
    }
}
