using System.Collections;
using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class EnergyShield : Ability
    {
        public const string Id = "energy_shield";
        public override string AbilityId => Id;

        [SerializeField] private float shieldAmount = 50f;
        [SerializeField] private float shieldDuration = 6f;

        private Health _health;
        private Coroutine _active;
        private float _absorbRemaining;
        private float _healthBeforeHit;

        private void Awake()
        {
            _health = GetComponentInParent<Health>();
        }

        protected override void Update()
        {
            base.Update();
            if (_active != null && _health != null)
                _healthBeforeHit = _health.CurrentHealth;
        }

        public override bool Activate()
        {
            if (_health == null || !_health.IsAlive) return false;
            if (_active != null) StopCoroutine(_active);
            if (!base.Activate()) return false;

            _active = StartCoroutine(ShieldRoutine());
            return true;
        }

        private IEnumerator ShieldRoutine()
        {
            _absorbRemaining = shieldAmount;
            _healthBeforeHit = _health.CurrentHealth;
            _health.OnDamaged += AbsorbDamage;
            float elapsed = 0f;
            while (elapsed < shieldDuration && _health != null && _health.IsAlive)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            _health.OnDamaged -= AbsorbDamage;
            _active = null;
        }

        private void AbsorbDamage(DamageInfo damage)
        {
            // Health exposes no shield setter, so absorption refunds the health actually lost.
            float actualLoss = _healthBeforeHit - _health.CurrentHealth;
            float refund = Mathf.Min(actualLoss, _absorbRemaining);
            if (refund <= 0f) return;
            _absorbRemaining -= refund;
            _health.Heal(refund);
            _healthBeforeHit = _health.CurrentHealth;
        }
    }
}
