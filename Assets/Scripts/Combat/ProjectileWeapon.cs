using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class ProjectileWeapon : Weapon
    {
        [SerializeField] private ObjectPool projectilePool;

        public override void TryFire(Transform firePoint, Vector3 direction)
        {
            if (!CanFire || firePoint == null || definition.projectilePrefab == null) return;

            RegisterShot();
            SpawnMuzzleFlash(firePoint);
            Vector3 shotDir = ApplySpread(direction);
            ReportGunshot(firePoint.position, shotDir);

            float dmg = DamageWithCrit(out bool crit);
            var payload = new DamageInfo(dmg, firePoint.position, shotDir,
                gameObject, definition.damageType, crit);

            Projectile projectile;
            if (projectilePool != null)
            {
                projectile = projectilePool.Get<Projectile>();
                projectile.transform.SetPositionAndRotation(firePoint.position,
                    Quaternion.LookRotation(shotDir));
            }
            else
            {
                var instance = Instantiate(definition.projectilePrefab,
                    firePoint.position, Quaternion.LookRotation(shotDir));
                projectile = instance.GetComponent<Projectile>();
                if (projectile == null)
                {
                    Destroy(instance);
                    return;
                }
            }

            projectile.Pool = projectilePool;
            projectile.Owner = gameObject;
            projectile.Launch(payload, shotDir);
        }
    }
}
