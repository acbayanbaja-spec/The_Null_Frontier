using NullFrontier.Combat;
using NullFrontier.Core;
using UnityEditor;
using UnityEngine;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Generates the WeaponDefinition assets the player's loadout equips. The ids match
    /// ItemDatabase so a weapon in the inventory and a weapon in the hand are the same
    /// thing. Stats follow the arcade convention: the sidearm is accurate and slow to
    /// empty, the rifle wins volume of fire, the shotgun trades range for stopping power.
    /// </summary>
    public static class WeaponAssets
    {
        public const string Dir = "Assets/Data/Weapons";

        [System.Serializable]
        private struct Spec
        {
            public string weaponId;
            public string displayName;
            public float damage;
            public float fireRate;
            public float range;
            public int magazine;
            public float reload;
            public float spread;
            public float recoil;
            public DamageType damageType;
            public string ammoType;
            public float critChance;
            public float critMultiplier;
        }

        private static readonly Spec[] Specs =
        {
            new Spec { weaponId = "nf_pistol", displayName = "NF-9 Sidearm",
                damage = 26f, fireRate = 4.5f, range = 55f, magazine = 14, reload = 1.25f,
                spread = 0.5f, recoil = 0.5f, damageType = DamageType.Ballistic,
                ammoType = "light", critChance = 0.12f, critMultiplier = 2f },

            new Spec { weaponId = "nf_ar", displayName = "NF-76 Assault Rifle",
                damage = 17f, fireRate = 9f, range = 70f, magazine = 32, reload = 1.9f,
                spread = 1.3f, recoil = 0.42f, damageType = DamageType.Ballistic,
                ammoType = "light", critChance = 0.08f, critMultiplier = 1.8f },

            new Spec { weaponId = "nf_energy", displayName = "Arc Light",
                damage = 22f, fireRate = 6f, range = 65f, magazine = 24, reload = 2.2f,
                spread = 0.2f, recoil = 0.3f, damageType = DamageType.Energy,
                ammoType = "cell", critChance = 0.1f, critMultiplier = 2.2f },

            new Spec { weaponId = "nf_shotgun", displayName = "Breach Scattergun",
                damage = 13f, fireRate = 1.3f, range = 22f, magazine = 6, reload = 2.6f,
                spread = 4.5f, recoil = 1.9f, damageType = DamageType.Ballistic,
                ammoType = "shell", critChance = 0.05f, critMultiplier = 1.6f },
        };

        [MenuItem("Null Frontier/Rebuild Weapon Assets")]
        public static void RebuildAll()
        {
            ArtAssets.EnsureFolder(Dir);
            foreach (var spec in Specs) Create(spec);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NullFrontier] {Specs.Length} weapon definitions written to {Dir}.");

            // Stats without a model are a weapon the player cannot see, so the two are
            // never allowed to be rebuilt apart.
            WeaponModels.RebuildAll();
        }

        /// <summary>Load the definition for an id, generating it on first use.</summary>
        public static WeaponDefinition Get(string weaponId)
        {
            string path = $"{Dir}/{weaponId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (asset != null) return asset;

            foreach (var spec in Specs)
            {
                if (spec.weaponId != weaponId) continue;
                ArtAssets.EnsureFolder(Dir);
                return Create(spec);
            }
            Debug.LogError($"[NullFrontier] No weapon spec for '{weaponId}'.");
            return null;
        }

        private static WeaponDefinition Create(Spec spec)
        {
            string path = $"{Dir}/{spec.weaponId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.weaponId = spec.weaponId;
            asset.displayName = spec.displayName;
            asset.damage = spec.damage;
            asset.fireRate = spec.fireRate;
            asset.range = spec.range;
            asset.magazineSize = spec.magazine;
            asset.reloadTime = spec.reload;
            asset.spreadDegrees = spec.spread;
            asset.recoilKick = spec.recoil;
            asset.damageType = spec.damageType;
            asset.isHitscan = true;
            asset.ammoType = spec.ammoType;
            asset.criticalChance = spec.critChance;
            asset.criticalMultiplier = spec.critMultiplier;

            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
