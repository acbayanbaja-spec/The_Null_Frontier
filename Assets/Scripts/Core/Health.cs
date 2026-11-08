using UnityEngine;

namespace NullFrontier.Core
{
    /// <summary>
    /// Health + shield pool for anything damageable (player, enemies, destructibles).
    /// Raises UnityEvents and GameEvents so UI, AI and missions can react.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxShield;
        [SerializeField] private float shieldRegenDelay = 3f;
        [SerializeField] private float shieldRegenRate = 10f;

        public float CurrentHealth { get; private set; }
        public float CurrentShield { get; private set; }
        public float MaxHealth => maxHealth;
        public float MaxShield => maxShield;
        public bool IsAlive => CurrentHealth > 0f;
        public Transform Transform => transform;

        public System.Action<DamageInfo> OnDamaged;
        public System.Action<float> OnHealed;
        public System.Action OnDied;

        private float _timeSinceDamage = float.MaxValue;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            CurrentShield = maxShield;
        }

        private void Update()
        {
            if (maxShield <= 0f || !IsAlive) return;

            _timeSinceDamage += Time.deltaTime;
            if (_timeSinceDamage >= shieldRegenDelay && CurrentShield < maxShield)
            {
                CurrentShield = Mathf.Min(maxShield, CurrentShield + shieldRegenRate * Time.deltaTime);
            }
        }

        public void TakeDamage(DamageInfo damage)
        {
            if (!IsAlive) return;

            _timeSinceDamage = 0f;
            float remaining = damage.Amount;

            if (CurrentShield > 0f)
            {
                float absorbed = Mathf.Min(CurrentShield, remaining);
                CurrentShield -= absorbed;
                remaining -= absorbed;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - remaining);
            OnDamaged?.Invoke(damage);

            if (CurrentHealth <= 0f)
            {
                OnDied?.Invoke();
                GameEvents.RaiseSaveRequested();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            OnHealed?.Invoke(amount);
        }

        public void SetMaxHealth(float value, bool refill)
        {
            maxHealth = value;
            if (refill) CurrentHealth = maxHealth;
        }
    }
}
