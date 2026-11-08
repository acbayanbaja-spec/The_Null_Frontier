using System;
using System.Collections.Generic;
using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Items
{
    /// <summary>
    /// Runtime inventory keyed by item id, mirrored to SaveSystem.Data.inventory.
    /// Equipped items are recorded in SaveSystem.Data.equipped; equipping does not
    /// consume a copy from the inventory in this scaffold.
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        public static InventorySystem Instance { get; private set; }

        public static readonly string[] Slots =
        {
            "weapon_primary", "weapon_secondary", "armor", "outfit", "accessory", "consumable_quick"
        };

        public event Action OnInventoryChanged;

        private readonly Dictionary<string, int> _items = new();

        public IReadOnlyList<SaveSystem.InventoryEntry> Entries
        {
            get
            {
                var list = new List<SaveSystem.InventoryEntry>(_items.Count);
                foreach (var kv in _items)
                {
                    list.Add(new SaveSystem.InventoryEntry { itemId = kv.Key, quantity = kv.Value });
                }
                return list;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadFromSave();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void LoadFromSave()
        {
            _items.Clear();
            var data = SaveSystem.Instance?.Data;
            if (data?.inventory == null) return;
            foreach (var entry in data.inventory)
            {
                if (entry == null || string.IsNullOrEmpty(entry.itemId) || entry.quantity <= 0) continue;
                _items[entry.itemId] = entry.quantity;
            }
        }

        public bool AddItem(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return false;
            var def = ItemDatabase.Get(itemId);
            if (def == null)
            {
                Debug.LogWarning($"[InventorySystem] Unknown item '{itemId}'.");
                return false;
            }

            int current = Count(itemId);
            if (def.maxStack > 0 && current + quantity > def.maxStack)
            {
                Debug.LogWarning($"[InventorySystem] '{itemId}' exceeds maxStack {def.maxStack}; clamping.");
                quantity = Mathf.Max(0, def.maxStack - current);
                if (quantity == 0) return true;
            }

            _items[itemId] = current + quantity;
            Commit();
            return true;
        }

        public bool RemoveItem(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return false;
            if (Count(itemId) < quantity) return false;

            int remaining = _items[itemId] - quantity;
            if (remaining <= 0) _items.Remove(itemId);
            else _items[itemId] = remaining;

            Commit();
            return true;
        }

        public int Count(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            return _items.TryGetValue(itemId, out int q) ? q : 0;
        }

        public bool IsEquipped(string itemId)
        {
            var data = SaveSystem.Instance?.Data;
            if (data?.equipped == null || string.IsNullOrEmpty(itemId)) return false;
            foreach (var e in data.equipped)
            {
                if (e != null && e.itemId == itemId) return true;
            }
            return false;
        }

        public bool Equip(string itemId, string slot)
        {
            if (Count(itemId) <= 0 || !IsValidSlot(slot)) return false;

            Unequip(slot);

            var data = SaveSystem.Instance?.Data;
            if (data == null) return false;
            data.equipped ??= new List<SaveSystem.EquippedEntry>();
            data.equipped.Add(new SaveSystem.EquippedEntry { slot = slot, itemId = itemId });

            Commit();
            return true;
        }

        public bool Unequip(string slot)
        {
            var data = SaveSystem.Instance?.Data;
            if (data?.equipped == null || !IsValidSlot(slot)) return false;

            int removed = data.equipped.RemoveAll(e => e != null && e.slot == slot);
            if (removed == 0) return false;

            Commit();
            return true;
        }

        public IEnumerable<(ItemDefinition def, string slot)> EquippedItems
        {
            get
            {
                var data = SaveSystem.Instance?.Data;
                if (data?.equipped == null) yield break;
                foreach (var e in data.equipped)
                {
                    if (e == null || string.IsNullOrEmpty(e.itemId)) continue;
                    var def = ItemDatabase.Get(e.itemId);
                    if (def != null) yield return (def, e.slot);
                }
            }
        }

        public static bool IsValidSlot(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return false;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] == slot) return true;
            }
            return false;
        }

        private void Commit()
        {
            var data = SaveSystem.Instance?.Data;
            if (data != null)
            {
                data.inventory ??= new List<SaveSystem.InventoryEntry>();
                data.inventory.Clear();
                data.inventory.AddRange(Entries);
                GameEvents.RaiseSaveRequested();
            }
            OnInventoryChanged?.Invoke();
        }
    }
}
