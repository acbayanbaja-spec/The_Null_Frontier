using NullFrontier.Characters;
using NullFrontier.Core;
using NullFrontier.Items;
using NullFrontier.Player;
using UnityEngine;

namespace NullFrontier.Progression
{
    /// <summary>
    /// Aggregates the selected character's base multipliers with serialized designer
    /// bonuses, equipped gear, and skill-tree ranks. Recalculate() is the single
    /// refresh point; other systems read the computed properties.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        public static PlayerStats Instance { get; private set; }

        [SerializeField] private float bonusAttack;
        [SerializeField] private float bonusDefense;
        [SerializeField] private float bonusMoveSpeed;

        public float AttackMultiplier { get; private set; } = 1f;
        public float DefenseMultiplier { get; private set; } = 1f;
        public float MoveSpeedBonus { get; private set; }

        private Health _health;
        private Stamina _stamina;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _health = GetComponent<Health>();
            _stamina = GetComponent<Stamina>();
        }

        private void OnEnable()
        {
            var inv = InventorySystem.Instance;
            if (inv != null) inv.OnInventoryChanged += Recalculate;
            Recalculate();
        }

        private void OnDisable()
        {
            var inv = InventorySystem.Instance;
            if (inv != null) inv.OnInventoryChanged -= Recalculate;
            if (Instance == this) Instance = null;
        }

        public void Recalculate()
        {
            var def = CharacterRegistry.Get(GameManager.Instance?.SelectedCharacterId ?? "iidreuspade");

            float attack = def.AttackMultiplier + bonusAttack;
            float defense = def.DefenseMultiplier + bonusDefense;
            float move = bonusMoveSpeed;
            float healthBonus = 0f;
            float staminaBonus = 0f;

            var inv = InventorySystem.Instance;
            if (inv != null)
            {
                foreach (var (item, _) in inv.EquippedItems)
                {
                    if (item == null) continue;
                    attack += item.attackBonusPct;
                    defense += item.defenseBonusPct;
                    move += item.moveSpeedBonus;
                    healthBonus += item.healthBonus;
                    staminaBonus += item.staminaBonus;
                }
            }

            var skills = SkillTree.Instance;
            if (skills != null)
            {
                attack += 0.05f * skills.GetRank(SkillId.Firepower);
                defense += 0.05f * skills.GetRank(SkillId.Shielding);
                move += 0.05f * skills.GetRank(SkillId.Mobility) * def.MoveSpeed;
            }

            AttackMultiplier = attack;
            DefenseMultiplier = defense;
            MoveSpeedBonus = move;

            if (_health != null)
            {
                float vitality = skills != null ? 1f + 0.05f * skills.GetRank(SkillId.Vitality) : 1f;
                _health.SetMaxHealth(def.MaxHealth * vitality + healthBonus, false);
            }

            if (_stamina != null)
            {
                float endurance = skills != null ? 1f + 0.05f * skills.GetRank(SkillId.Endurance) : 1f;
                _stamina.SetMax(def.MaxStamina * endurance + staminaBonus, false);
            }
        }
    }
}
