using System.Collections;
using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class HitscanWeapon : Weapon
    {
        [SerializeField] private ObjectPool impactEffectPool;

        public override void TryFire(Transform firePoint, Vector3 direction)
        {
            if (!CanFire || firePoint == null) return;

            RegisterShot();
            SpawnMuzzleFlash(firePoint);
            Vector3 shotDir = ApplySpread(direction);
            ReportGunshot(firePoint.position, shotDir);

            Color tint = TraceTint(definition.damageType);
            Vector3 start = firePoint.position + shotDir * 0.06f;
            Vector3 end = start + shotDir * Mathf.Max(1f, definition.range);

            if (Physics.Raycast(firePoint.position, shotDir, out RaycastHit hit,
                    definition.range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;

                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    float dmg = DamageWithCrit(out bool crit);
                    damageable.TakeDamage(new DamageInfo(dmg, hit.point, shotDir,
                        gameObject, definition.damageType, crit));
                }

                CombatEffects.Impact(hit.point, hit.normal, tint);
                SpawnImpactEffect(hit.point, hit.normal);
            }

            // The round is drawn even on a miss: a shot that fires nothing across the
            // screen is indistinguishable from a shot that hit nothing.
            CombatEffects.Shot(start, end, tint, TraceWidth(definition.damageType));
        }

        /// <summary>Energy rounds glow in their own colour, powder rounds in hot brass.</summary>
        private static Color TraceTint(DamageType type)
        {
            switch (type)
            {
                case DamageType.Energy: return new Color(0.35f, 0.9f, 1f);
                case DamageType.Explosive: return new Color(1f, 0.55f, 0.15f);
                default: return new Color(1f, 0.82f, 0.45f);
            }
        }

        // A real tracer is about 15 mm wide and reads as a single pixel on a phone at 30 m,
        // which is why rounds looked like nothing at all. These widths are chosen for the
        // screen, not the armoury.
        private static float TraceWidth(DamageType type) =>
            type == DamageType.Energy ? 0.075f : 0.055f;

        private void SpawnImpactEffect(Vector3 point, Vector3 normal)
        {
            if (definition == null || definition.impactEffectPrefab == null) return;

            if (impactEffectPool != null)
            {
                Component fx = impactEffectPool.Get<Component>();
                fx.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
                var particles = fx.GetComponent<ParticleSystem>();
                if (particles != null) particles.Play();
                StartCoroutine(ReleaseEffect(fx));
                return;
            }

            var spawned = Instantiate(definition.impactEffectPrefab, point, Quaternion.LookRotation(normal));
            var ps = spawned.GetComponent<ParticleSystem>();
            Destroy(spawned, ps != null ? ps.main.duration : 2f);
        }

        private IEnumerator ReleaseEffect(Component fx)
        {
            yield return new WaitForSeconds(2f);
            if (fx != null && impactEffectPool != null) impactEffectPool.Release(fx);
        }
    }
}
