using UnityEngine;

namespace NullFrontier.Player
{
    /// <summary>
    /// Stamina pool used by sprinting, dodging and melee combos. Regenerates after
    /// a delay. Max value is seeded from the selected CharacterDefinition.
    /// </summary>
    public class Stamina : MonoBehaviour
    {
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float regenRate = 18f;
        [SerializeField] private float regenDelay = 1f;

        public float Current { get; private set; }
        public float Max => maxStamina;
        public bool HasStamina => Current > 0f;
        public System.Action<float, float> OnChanged;

        private float _timeSinceUse = float.MaxValue;

        private void Awake()
        {
            Current = maxStamina;
        }

        public void SetMax(float value, bool refill)
        {
            maxStamina = value;
            if (refill) Current = value;
            OnChanged?.Invoke(Current, maxStamina);
        }

        private void Update()
        {
            _timeSinceUse += Time.deltaTime;
            if (_timeSinceUse >= regenDelay && Current < maxStamina)
            {
                Current = Mathf.Min(maxStamina, Current + regenRate * Time.deltaTime);
                OnChanged?.Invoke(Current, maxStamina);
            }
        }

        public void Drain(float amount)
        {
            _timeSinceUse = 0f;
            Set(Current - amount);
        }

        public bool TrySpend(float amount)
        {
            if (Current < amount) return false;
            _timeSinceUse = 0f;
            Set(Current - amount);
            return true;
        }

        private void Set(float value)
        {
            Current = Mathf.Clamp(value, 0f, maxStamina);
            OnChanged?.Invoke(Current, maxStamina);
        }
    }
}
