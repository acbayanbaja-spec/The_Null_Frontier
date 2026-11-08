using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Items
{
    /// <summary>
    /// Temporary damage absorption (shield cell effect). Health applies shield before
    /// raising OnDamaged, so this refunds the health portion of absorbed hits via
    /// Heal, which is clamped to max — safe against overheal.
    /// </summary>
    public class ShieldBoost : MonoBehaviour
    {
        private float _pool;
        private Health _health;

        public float Remaining => _pool;

        public void Activate(float pool)
        {
            _pool = Mathf.Max(0f, pool);
            _health = GetComponent<Health>();
            if (_health != null && _pool > 0f) _health.OnDamaged += HandleDamaged;
            else Detach();
        }

        private void HandleDamaged(DamageInfo damage)
        {
            if (_health == null || _pool <= 0f) return;
            float refund = Mathf.Min(_pool, damage.Amount);
            _pool -= refund;
            _health.Heal(refund);
            if (_pool <= 0f) Detach();
        }

        private void Detach()
        {
            if (_health != null) _health.OnDamaged -= HandleDamaged;
            Destroy(this);
        }

        private void OnDisable()
        {
            if (_health != null) _health.OnDamaged -= HandleDamaged;
        }
    }
}
