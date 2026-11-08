using System.Collections.Generic;

namespace NullFrontier.Items
{
    public enum ItemCategory
    {
        Weapon,
        Armor,
        Outfit,
        Accessory,
        Consumable,
        CraftingMaterial,
        QuestItem,
        Collectible
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// Plain C# item definition; the database is seeded in code (mirrors
    /// CharacterRegistry) so the game works without any imported assets.
    /// </summary>
    public class ItemDefinition
    {
        public string id;
        public string displayName;
        public ItemCategory category;
        public ItemRarity rarity;
        public string description;
        public int maxStack = 99;

        public float healthBonus;
        public float staminaBonus;
        public float shieldBonus;
        public float attackBonusPct;
        public float defenseBonusPct;
        public float moveSpeedBonus;

        public ItemDefinition(string id, string displayName, ItemCategory category, ItemRarity rarity,
            string description, int maxStack = 99, float healthBonus = 0f, float staminaBonus = 0f,
            float shieldBonus = 0f, float attackBonusPct = 0f, float defenseBonusPct = 0f,
            float moveSpeedBonus = 0f)
        {
            this.id = id;
            this.displayName = displayName;
            this.category = category;
            this.rarity = rarity;
            this.description = description;
            this.maxStack = maxStack;
            this.healthBonus = healthBonus;
            this.staminaBonus = staminaBonus;
            this.shieldBonus = shieldBonus;
            this.attackBonusPct = attackBonusPct;
            this.defenseBonusPct = defenseBonusPct;
            this.moveSpeedBonus = moveSpeedBonus;
        }
    }

    public static class ItemDatabase
    {
        public static readonly IReadOnlyList<ItemDefinition> All = new List<ItemDefinition>
        {
            new("nf_pistol", "NF-9 Sidearm", ItemCategory.Weapon, ItemRarity.Common,
                "Standard issue kinetic pistol.", maxStack: 1, attackBonusPct: 0.05f),
            new("nf_ar", "NF-76 Assault Rifle", ItemCategory.Weapon, ItemRarity.Uncommon,
                "Auto-fed ballistic rifle.", maxStack: 1, attackBonusPct: 0.12f),
            new("nf_energy", "Arc Light Energy Rifle", ItemCategory.Weapon, ItemRarity.Rare,
                "Coherent energy emitter, chews shields.", maxStack: 1, attackBonusPct: 0.20f),
            new("nf_shotgun", "Breach Scattergun", ItemCategory.Weapon, ItemRarity.Uncommon,
                "Close-range burst weapon.", maxStack: 1, attackBonusPct: 0.15f),
            new("nf_sniper", "Longeye Railgun", ItemCategory.Weapon, ItemRarity.Epic,
                "Magnetically accelerated long-range platform.", maxStack: 1, attackBonusPct: 0.30f),
            new("nf_blade", "Mono Edge Blade", ItemCategory.Weapon, ItemRarity.Common,
                "Vibration-honed melee weapon.", maxStack: 1, attackBonusPct: 0.08f),
            new("armor_plate", "Frontier Plate Carrier", ItemCategory.Armor, ItemRarity.Uncommon,
                "Ballistic plates over a cooled under-layer.", maxStack: 1,
                defenseBonusPct: 0.15f, healthBonus: 20f),
            new("armor_exo", "Exo Rig Frame", ItemCategory.Armor, ItemRarity.Rare,
                "Powered exoskeleton frame.", maxStack: 1,
                defenseBonusPct: 0.25f, healthBonus: 35f, moveSpeedBonus: 0.3f),
            new("outfit_scout", "Scout Weave Outfit", ItemCategory.Outfit, ItemRarity.Common,
                "Lightweight mobility weave.", maxStack: 1, moveSpeedBonus: 0.5f, staminaBonus: 15f),
            new("acc_targeter", "Holo Targeter", ItemCategory.Accessory, ItemRarity.Rare,
                "Projects firing solutions onto the visor.", maxStack: 1, attackBonusPct: 0.10f),
            new("acc_barrier", "Pocket Barrier Cell", ItemCategory.Accessory, ItemRarity.Uncommon,
                "Personal shield capacitor.", maxStack: 1, shieldBonus: 25f, defenseBonusPct: 0.05f),
            new("med_gel", "Med Gel", ItemCategory.Consumable, ItemRarity.Common,
                "Restores 50 health.", maxStack: 5, healthBonus: 0f),
            new("shield_cell", "Shield Cell", ItemCategory.Consumable, ItemRarity.Common,
                "Absorbs up to 50 damage.", maxStack: 5),
            new("stim", "Adrenal Stim", ItemCategory.Consumable, ItemRarity.Common,
                "Restores 50 stamina.", maxStack: 5, staminaBonus: 0f),
            new("scrap_alloy", "Alloy Scrap", ItemCategory.CraftingMaterial, ItemRarity.Common,
                "Recovered structural alloy.", maxStack: 50),
            new("core_fragment", "Core Fragment", ItemCategory.QuestItem, ItemRarity.Legendary,
                "A shard of the Null Core. It hums.", maxStack: 1),
            new("data_cache", "Encrypted Data Cache", ItemCategory.Collectible, ItemRarity.Rare,
                "Lost player telemetry, worth a fortune.", maxStack: 10)
        };

        public static ItemDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].id == id) return All[i];
            }
            return null;
        }
    }
}
