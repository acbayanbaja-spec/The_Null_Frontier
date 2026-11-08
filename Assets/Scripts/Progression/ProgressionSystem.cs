using System;
using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Progression
{
    /// <summary>
    /// XP / level / skill-point authority. Reads and writes SaveSystem.Data, levels
    /// follow xpNeeded = 100 * level^1.35, and every level-up grants a skill point.
    /// </summary>
    public class ProgressionSystem : MonoBehaviour
    {
        public static ProgressionSystem Instance { get; private set; }

        public event Action<int> OnLevelUp;

        public int Level => SaveSystem.Instance?.Data.level ?? 1;
        public long Xp => SaveSystem.Instance?.Data.xp ?? 0;
        public int SkillPoints => SaveSystem.Instance?.Data.skillPoints ?? 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (SaveSystem.Instance == null)
            {
                var existing = FindFirstObjectByType<SaveSystem>();
                if (existing == null)
                {
                    var go = new GameObject(nameof(SaveSystem));
                    existing = go.AddComponent<SaveSystem>();
                }
                existing.Load();
            }
        }

        public static long XpNeededForLevel(int level)
        {
            if (level < 1) level = 1;
            return (long)(100.0 * Math.Pow(level, 1.35));
        }

        public void AddXp(float amount)
        {
            var save = SaveSystem.Instance;
            if (save == null || amount <= 0f) return;

            var data = save.Data;
            data.xp += (long)amount;

            while (data.xp >= XpNeededForLevel(data.level))
            {
                data.xp -= XpNeededForLevel(data.level);
                data.level++;
                data.skillPoints++;
                OnLevelUp?.Invoke(data.level);
            }

            GameEvents.RaiseSaveRequested();
        }
    }
}
