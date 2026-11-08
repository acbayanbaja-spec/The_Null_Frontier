using NullFrontier.Core;
using NullFrontier.Player;
using UnityEngine;

namespace NullFrontier.Items
{
    /// <summary>
    /// Player component exposing UseConsumable(itemId) for UI buttons. Resolves the
    /// item from ItemDatabase, applies its effect, then consumes one copy.
    /// </summary>
    public class ConsumableUse : MonoBehaviour
    {
        [SerializeField] private float medGelHeal = 50f;
        [SerializeField] private float shieldCellAmount = 50f;
        [SerializeField] private float stimAmount = 50f;

        private Health _health;
        private Stamina _stamina;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _stamina = GetComponent<Stamina>();
        }

        public void UseConsumable(string itemId)
        {
            var def = ItemDatabase.Get(itemId);
            if (def == null || def.category != ItemCategory.Consumable) return;

            bool used = itemId switch
            {
                "med_gel" => UseMedGel(),
                "shield_cell" => UseShieldCell(),
                "stim" => UseStim(),
                _ => false
            };

            if (used) InventorySystem.Instance?.RemoveItem(itemId, 1);
        }

        private bool UseMedGel()
        {
            if (_health == null || !_health.IsAlive) return false;
            _health.Heal(medGelHeal);
            return true;
        }

        private bool UseShieldCell()
        {
            if (_health == null || !_health.IsAlive) return false;
            var boost = gameObject.AddComponent<ShieldBoost>();
            boost.Activate(shieldCellAmount);
            return true;
        }

        private bool UseStim()
        {
            if (_stamina == null) return false;
            _stamina.Drain(-stimAmount);
            return true;
        }
    }
}
