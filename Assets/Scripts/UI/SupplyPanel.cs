using NullFrontier.Core;
using NullFrontier.Items;
using NullFrontier.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace NullFrontier.UI
{
    /// <summary>
    /// Supply and training drawer. Consumables are spent through the player's
    /// ConsumableUse so the item and its effect leave the inventory in one call; skill
    /// points go through SkillTree so PlayerStats recalculates in the same call.
    /// </summary>
    public class SupplyPanel : MonoBehaviour
    {
        private static readonly string[] ConsumableIds = { "med_gel", "shield_cell", "stim" };

        private static readonly SkillId[] SkillIds =
        {
            SkillId.Vitality, SkillId.Endurance, SkillId.Firepower, SkillId.Mobility, SkillId.Shielding
        };

        [Header("Consumables")]
        [SerializeField] private Text[] consumableCounts;
        [SerializeField] private Button[] consumableUses;

        [Header("Training")]
        [SerializeField] private Text skillPointsLabel;
        [SerializeField] private Button[] skillButtons;

        private void Awake()
        {
            if (consumableUses != null)
            {
                for (int i = 0; i < consumableUses.Length && i < ConsumableIds.Length; i++)
                {
                    if (consumableUses[i] == null) continue;
                    int index = i;
                    consumableUses[i].onClick.AddListener(() => Use(ConsumableIds[index]));
                }
            }

            if (skillButtons != null)
            {
                for (int i = 0; i < skillButtons.Length && i < SkillIds.Length; i++)
                {
                    if (skillButtons[i] == null) continue;
                    SkillId skill = SkillIds[i];
                    skillButtons[i].onClick.AddListener(() => Spend(skill));
                }
            }
        }

        private void OnEnable()
        {
            var inventory = InventorySystem.Instance;
            if (inventory != null) inventory.OnInventoryChanged += Refresh;

            var progression = ProgressionSystem.Instance;
            if (progression != null) progression.OnLevelUp += HandleLevelUp;

            Refresh();
        }

        private void HandleLevelUp(int level) => Refresh();

        private void OnDisable()
        {
            var inventory = InventorySystem.Instance;
            if (inventory != null) inventory.OnInventoryChanged -= Refresh;

            var progression = ProgressionSystem.Instance;
            if (progression != null) progression.OnLevelUp -= HandleLevelUp;
        }

        private void Use(string itemId)
        {
            var inventory = InventorySystem.Instance;
            if (inventory == null || inventory.Count(itemId) <= 0) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            var consumables = player != null ? player.GetComponent<ConsumableUse>() : null;
            if (consumables == null) return;

            int before = inventory.Count(itemId);
            consumables.UseConsumable(itemId);

            var audio = AudioDirector.Instance;
            if (inventory.Count(itemId) < before) audio?.PlayObjectivePing();
            else audio?.PlayUiClick();
            Refresh();
        }

        private void Spend(SkillId skill)
        {
            bool spent = SkillTree.Instance != null && SkillTree.Instance.SpendSkillPoint(skill);
            var audio = AudioDirector.Instance;
            if (spent) audio?.PlayMissionComplete();
            else audio?.PlayUiClick();
            Refresh();
        }

        private void Refresh()
        {
            var inventory = InventorySystem.Instance;
            if (consumableCounts != null)
            {
                for (int i = 0; i < consumableCounts.Length && i < ConsumableIds.Length; i++)
                {
                    string itemId = ConsumableIds[i];
                    int count = inventory != null ? inventory.Count(itemId) : 0;
                    var def = ItemDatabase.Get(itemId);

                    if (consumableCounts[i] != null)
                    {
                        string name = string.IsNullOrEmpty(def?.displayName) ? itemId : def.displayName;
                        consumableCounts[i].text = $"{name}   ·   x{count}";
                    }
                    if (consumableUses != null && i < consumableUses.Length
                        && consumableUses[i] != null)
                    {
                        consumableUses[i].interactable = count > 0;
                    }
                }
            }

            int points = ProgressionSystem.Instance != null
                ? ProgressionSystem.Instance.SkillPoints
                : 0;

            if (skillPointsLabel != null)
                skillPointsLabel.text = points > 0
                    ? $"SKILL POINTS AVAILABLE  {points}"
                    : "SKILL POINTS  ·  EARN THEM BY COMPLETING CONTRACTS";

            if (skillButtons == null) return;
            for (int i = 0; i < skillButtons.Length; i++)
                if (skillButtons[i] != null) skillButtons[i].interactable = points > 0;
        }
    }
}
