using NullFrontier.Core;
using NullFrontier.Player;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class MeleeWeapon : Weapon
    {
        [Header("Melee")]
        [SerializeField] private float attackRange = 2.2f;
        [SerializeField, Range(10f, 360f)] private float arcAngle = 100f;
        [SerializeField] private float comboWindow = 0.9f;
        [SerializeField] private float comboDamageBonus = 0.15f;
        [SerializeField] private float heavyDamageMultiplier = 2f;
        [SerializeField] private float staminaCost = 12f;
        [SerializeField] private LayerMask hitMask = ~0;

        public int ComboStage => _comboStage;

        private int _comboStage;
        private float _comboTimer;

        protected virtual void Update()
        {
            if (_comboTimer <= 0f) return;
            _comboTimer -= Time.deltaTime;
            if (_comboTimer <= 0f) _comboStage = 0;
        }

        public override void TryFire(Transform firePoint, Vector3 direction)
        {
            if (!CanSwing()) return;

            int stage = _comboTimer > 0f ? _comboStage : 0;
            bool finisher = stage == 2;
            float scale = finisher ? heavyDamageMultiplier : 1f + comboDamageBonus * stage;

            ExecuteSwing(FireOrigin(firePoint), direction, scale);

            _comboStage = finisher ? 0 : stage + 1;
            _comboTimer = _comboStage > 0 ? comboWindow : 0f;
        }

        public void TryHeavyAttack(Transform firePoint, Vector3 direction)
        {
            if (!CanSwing()) return;
            _comboStage = 0;
            _comboTimer = 0f;
            ExecuteSwing(FireOrigin(firePoint), direction, heavyDamageMultiplier);
        }

        private Vector3 FireOrigin(Transform firePoint)
        {
            return firePoint != null ? firePoint.position : transform.position + Vector3.up;
        }

        private bool CanSwing()
        {
            if (definition == null || IsReloading) return false;
            if (Time.time < NextFireTime) return false;
            var stamina = GetComponentInParent<Stamina>();
            return stamina == null || stamina.TrySpend(staminaCost);
        }

        private void ExecuteSwing(Vector3 origin, Vector3 direction, float damageScale)
        {
            RegisterTimedSwing();

            Vector3 flatDir = new Vector3(direction.x, 0f, direction.z);
            if (flatDir.sqrMagnitude < 0.001f) flatDir = transform.forward;
            flatDir.Normalize();

            float dmg = DamageWithCrit(out bool crit) * damageScale;
            Vector3 center = origin + flatDir * (attackRange * 0.5f);
            var hits = Physics.OverlapSphere(center, attackRange * 0.6f, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(transform)) continue;

                Vector3 to = hit.transform.position - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.001f && Vector3.Angle(flatDir, to) > arcAngle * 0.5f) continue;

                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                damageable.TakeDamage(new DamageInfo(dmg, hit.ClosestPoint(origin),
                    flatDir, gameObject, definition.damageType, crit));
            }

            ReportGunshot(origin, flatDir);
        }
    }
}
