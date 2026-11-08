using System.Collections.Generic;
using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Progression
{
    public enum SkillId
    {
        Vitality,
        Endurance,
        Firepower,
        Mobility,
        Shielding
    }

    /// <summary>
    /// Skill points come from ProgressionSystem level-ups. Each rank grants +5% to
    /// one stat; PlayerStats.Recalculate() picks the ranks up from here.
    /// </summary>
    public class SkillTree : MonoBehaviour
    {
        public static SkillTree Instance { get; private set; }

        private const float BonusPerRank = 0.05f;

        private readonly Dictionary<SkillId, int> _ranks = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int GetRank(SkillId id) => _ranks.TryGetValue(id, out int r) ? r : 0;

        public bool SpendSkillPoint(SkillId id)
        {
            var save = SaveSystem.Instance;
            if (save == null || save.Data.skillPoints <= 0) return false;

            save.Data.skillPoints--;
            _ranks[id] = GetRank(id) + 1;

            var stats = PlayerStats.Instance;
            if (stats != null) stats.Recalculate();

            GameEvents.RaiseSaveRequested();
            return true;
        }
    }
}
