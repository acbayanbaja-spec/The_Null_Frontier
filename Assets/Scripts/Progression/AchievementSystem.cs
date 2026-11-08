using System;
using System.Collections.Generic;
using NullFrontier.Core;
using NullFrontier.Items;
using NullFrontier.Missions;
using UnityEngine;

namespace NullFrontier.Progression
{
    /// <summary>Where a badge reads its number from. Every option is a signal the game
    /// already emits or a field the save already carries - nothing here is counted by a
    /// timer that only exists to fill the screen.</summary>
    public enum AchievementMetric
    {
        HostilesEliminated,
        ContractsCompleted,
        SalvageCollected,
        ObjectivesReached,
        CoOpMatches,
        RescuesReturned,
        LevelReached
    }

    [Serializable]
    public class AchievementDefinition
    {
        public string id;
        public string title;
        public string blurb;
        public AchievementMetric metric;
        public int target;
        public string rewardItemId;
        public int rewardQuantity;
    }

    /// <summary>
    /// Tracks lifetime progress toward the badge catalog and pays out the reward once the
    /// player asks for it. Unlocks are derived from the counters on every read, so a save
    /// written before a badge existed still resolves correctly, and a counter that reached
    /// its target while the app was closed cannot be missed.
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        public static AchievementSystem Instance { get; private set; }

        public event Action OnAchievementsChanged;

        public static readonly AchievementDefinition[] Catalog =
        {
            new AchievementDefinition
            {
                id = "first_blood", title = "FIRST CONTACT",
                blurb = "Drop your first hostile in Vertical City.",
                metric = AchievementMetric.HostilesEliminated, target = 1,
                rewardItemId = "stim", rewardQuantity = 1
            },
            new AchievementDefinition
            {
                id = "clean_sweep", title = "CLEAN SWEEP",
                blurb = "Eliminate 25 hostiles.",
                metric = AchievementMetric.HostilesEliminated, target = 25,
                rewardItemId = "med_gel", rewardQuantity = 2
            },
            new AchievementDefinition
            {
                id = "signed_on", title = "SIGNED ON",
                blurb = "Close out one field contract.",
                metric = AchievementMetric.ContractsCompleted, target = 1,
                rewardItemId = "shield_cell", rewardQuantity = 1
            },
            new AchievementDefinition
            {
                id = "long_service", title = "LONG SERVICE",
                blurb = "Close out three field contracts.",
                metric = AchievementMetric.ContractsCompleted, target = 3,
                rewardItemId = "data_cache", rewardQuantity = 1
            },
            new AchievementDefinition
            {
                id = "salvager", title = "SALVAGER",
                blurb = "Collect 5 objective items.",
                metric = AchievementMetric.SalvageCollected, target = 5,
                rewardItemId = "scrap_alloy", rewardQuantity = 5
            },
            new AchievementDefinition
            {
                id = "surveyor", title = "SURVEYOR",
                blurb = "Reach 2 marked locations.",
                metric = AchievementMetric.ObjectivesReached, target = 2,
                rewardItemId = "shield_cell", rewardQuantity = 2
            },
            new AchievementDefinition
            {
                id = "rank_five", title = "FIELD VETERAN",
                blurb = "Reach operative level 5.",
                metric = AchievementMetric.LevelReached, target = 5,
                rewardItemId = "acc_targeter", rewardQuantity = 1
            },
            new AchievementDefinition
            {
                id = "coming_back", title = "COMING BACK",
                blurb = "Return from the respawn point 3 times.",
                metric = AchievementMetric.RescuesReturned, target = 3,
                rewardItemId = "stim", rewardQuantity = 2
            }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.GameModeChanged += HandleMode;
            GameEvents.PlayerRespawned += HandleRespawn;
            MissionManager.OnMissionEvent += HandleMissionEvent;
        }

        private void OnDisable()
        {
            GameEvents.GameModeChanged -= HandleMode;
            GameEvents.PlayerRespawned -= HandleRespawn;
            MissionManager.OnMissionEvent -= HandleMissionEvent;
        }

        private void HandleMode(GameMode mode)
        {
            if (mode == GameMode.Cooperative) Add(AchievementMetric.CoOpMatches, 1);
        }

        private void HandleRespawn() => Add(AchievementMetric.RescuesReturned, 1);

        private void HandleMissionEvent(MissionManager.MissionEventType type, string targetId,
            int count)
        {
            switch (type)
            {
                case MissionManager.MissionEventType.Kill:
                    Add(AchievementMetric.HostilesEliminated, count);
                    break;
                case MissionManager.MissionEventType.Collect:
                    Add(AchievementMetric.SalvageCollected, count);
                    break;
                case MissionManager.MissionEventType.Reach:
                    Add(AchievementMetric.ObjectivesReached, count);
                    break;
            }
        }

        /// <summary>Row handed to the badge and reward panels. Everything on it is either
        /// a counter or a real item from the database.</summary>
        public sealed class Row
        {
            public AchievementDefinition Definition;
            public int Progress;
            public bool Unlocked;
            public bool Claimed;
            public string Reward;
            public bool Claimable => Unlocked && !Claimed;
        }

        private readonly List<Row> _rows = new();

        public IReadOnlyList<Row> Snapshot()
        {
            _rows.Clear();
            for (int i = 0; i < Catalog.Length; i++)
            {
                var def = Catalog[i];
                _rows.Add(new Row
                {
                    Definition = def,
                    Progress = Count(def.metric),
                    Unlocked = IsUnlocked(def.id),
                    Claimed = IsClaimed(def.id),
                    Reward = RewardText(def)
                });
            }
            return _rows;
        }

        public int UnlockedCount
        {
            get
            {
                var unlocked = UnlockedIds();
                int n = 0;
                for (int i = 0; i < Catalog.Length; i++)
                    if (unlocked.Contains(Catalog[i].id)) n++;
                return n;
            }
        }

        public int TotalCount => Catalog.Length;

        /// <summary>Badges that are earned but whose supply drop is still sitting in the
        /// outbox. Drives the notification dot on the title screen.</summary>
        public int ClaimableCount
        {
            get
            {
                var unlocked = UnlockedIds();
                var claimed = ClaimedIds();
                int n = 0;
                for (int i = 0; i < Catalog.Length; i++)
                    if (unlocked.Contains(Catalog[i].id) && !claimed.Contains(Catalog[i].id)) n++;
                return n;
            }
        }

        /// <summary>Grants the badge's item into the real inventory. A claim that the
        /// inventory refuses - unknown item, stack full - stays unclaimed rather than
        /// reporting a payout that never landed.</summary>
        public bool Claim(string id)
        {
            var data = SaveSystem.Instance?.Data;
            if (data == null || string.IsNullOrEmpty(id)) return false;

            AchievementDefinition def = null;
            for (int i = 0; i < Catalog.Length; i++)
                if (Catalog[i].id == id) def = Catalog[i];
            if (def == null || !IsUnlocked(id) || IsClaimed(id)) return false;

            var inventory = InventorySystem.Instance;
            if (inventory == null || !inventory.AddItem(def.rewardItemId, def.rewardQuantity))
            {
                Debug.LogWarning($"[AchievementSystem] '{id}' reward '{def.rewardItemId}' " +
                                 "could not be added; it stays claimable.");
                return false;
            }

            data.claimedRewards ??= new List<string>();
            data.claimedRewards.Add(id);
            GameEvents.RaiseSaveRequested();
            OnAchievementsChanged?.Invoke();
            return true;
        }

        private void Add(AchievementMetric metric, int amount)
        {
            if (amount <= 0) return;
            var data = SaveSystem.Instance?.Data;
            if (data == null) return;

            data.counters ??= new List<SaveSystem.CounterEntry>();
            string key = Key(metric);
            for (int i = 0; i < data.counters.Count; i++)
            {
                if (data.counters[i] != null && data.counters[i].key == key)
                {
                    data.counters[i].value += amount;
                    CheckUnlocks();
                    return;
                }
            }

            data.counters.Add(new SaveSystem.CounterEntry { key = key, value = amount });
            CheckUnlocks();
        }

        /// <summary>Reads a badge's number: lifetime counters for the ones the save has to
        /// remember, live save fields for the ones it already tracks.</summary>
        public static int Count(AchievementMetric metric)
        {
            var data = SaveSystem.Instance?.Data;
            if (data == null) return 0;

            switch (metric)
            {
                case AchievementMetric.ContractsCompleted:
                    return data.completedMissions?.Count ?? 0;
                case AchievementMetric.LevelReached:
                    return data.level;
            }

            string key = Key(metric);
            if (data.counters == null) return 0;
            for (int i = 0; i < data.counters.Count; i++)
                if (data.counters[i] != null && data.counters[i].key == key)
                    return (int)data.counters[i].value;
            return 0;
        }

        private void CheckUnlocks()
        {
            var data = SaveSystem.Instance?.Data;
            if (data == null) return;

            data.achievements ??= new List<string>();
            bool changed = false;

            for (int i = 0; i < Catalog.Length; i++)
            {
                var def = Catalog[i];
                if (data.achievements.Contains(def.id)) continue;
                if (Count(def.metric) < def.target) continue;

                data.achievements.Add(def.id);
                changed = true;
                Debug.Log($"[AchievementSystem] Unlocked {def.title} - " +
                          $"{RewardText(def)} ready to claim.");
            }

            if (!changed) return;
            GameEvents.RaiseSaveRequested();
            OnAchievementsChanged?.Invoke();
        }

        private static List<string> UnlockedIds()
        {
            var data = SaveSystem.Instance?.Data;
            return data?.achievements ?? new List<string>();
        }

        private static List<string> ClaimedIds()
        {
            var data = SaveSystem.Instance?.Data;
            return data?.claimedRewards ?? new List<string>();
        }

        private static bool IsUnlocked(string id) => UnlockedIds().Contains(id);

        private static bool IsClaimed(string id) => ClaimedIds().Contains(id);

        private static string RewardText(AchievementDefinition def)
        {
            var item = ItemDatabase.Get(def.rewardItemId);
            string name = item != null ? item.displayName : def.rewardItemId;
            return $"{name} x{def.rewardQuantity}";
        }

        private static string Key(AchievementMetric metric) => metric.ToString();
    }
}
