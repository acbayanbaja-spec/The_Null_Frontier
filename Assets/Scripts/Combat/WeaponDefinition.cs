using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Combat
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "NullFrontier/Weapon Definition")]
    public class WeaponDefinition : ScriptableObject
    {
        public string weaponId = "weapon";
        public string displayName = "Weapon";
        public float damage = 20f;
        public float fireRate = 6f;
        public float range = 60f;
        public int magazineSize = 30;
        public float reloadTime = 1.6f;
        public float spreadDegrees = 1f;
        public float recoilKick = 0.6f;
        public DamageType damageType = DamageType.Ballistic;
        public bool isHitscan = true;
        public GameObject projectilePrefab;
        public GameObject muzzleFlashPrefab;
        public GameObject impactEffectPrefab;
        public string ammoType = "light";
        [Range(0f, 1f)] public float criticalChance = 0.1f;
        public float criticalMultiplier = 2f;
    }
}
