using System;
using System.Collections;
using NullFrontier.AI;
using NullFrontier.Characters;
using NullFrontier.Core;
using NullFrontier.Progression;
using UnityEngine;

namespace NullFrontier.Combat
{
    public abstract class Weapon : MonoBehaviour
    {
        [SerializeField] protected WeaponDefinition definition;
        [SerializeField] protected float alertRadius = 20f;

        public WeaponDefinition Definition => definition;
        public int CurrentAmmo { get; protected set; }
        public bool IsReloading { get; protected set; }

        // Aiming tightens spread; PlayerCombat sets this from AimHeld each frame.
        public float SpreadScale { get; set; } = 1f;

        public event Action<int, int> OnAmmoChanged;
        public event Action OnFired;
        public event Action OnReloaded;

        protected float NextFireTime;
        private Vector2 _pendingRecoil;

        protected virtual void Awake()
        {
            if (definition != null) CurrentAmmo = definition.magazineSize;
        }

        public bool CanFire =>
            definition != null && !IsReloading && CurrentAmmo > 0f && Time.time >= NextFireTime;

        public abstract void TryFire(Transform firePoint, Vector3 direction);

        public void Reload()
        {
            if (definition == null || IsReloading || CurrentAmmo >= definition.magazineSize) return;
            IsReloading = true;
            StartCoroutine(ReloadRoutine());
        }

        protected virtual IEnumerator ReloadRoutine()
        {
            yield return new WaitForSeconds(definition.reloadTime);
            CurrentAmmo = definition.magazineSize;
            IsReloading = false;
            OnReloaded?.Invoke();
            OnAmmoChanged?.Invoke(CurrentAmmo, definition.magazineSize);
        }

        protected void RegisterShot()
        {
            CurrentAmmo--;
            NextFireTime = Time.time + SecondsPerAttack();
            _pendingRecoil += new Vector2(
                UnityEngine.Random.Range(-0.5f, 0.5f) * definition.recoilKick,
                definition.recoilKick);
            OnFired?.Invoke();
            OnAmmoChanged?.Invoke(CurrentAmmo, definition.magazineSize);
        }

        protected void RegisterTimedSwing()
        {
            NextFireTime = Time.time + SecondsPerAttack();
            _pendingRecoil += new Vector2(0f, definition.recoilKick);
            OnFired?.Invoke();
        }

        protected float SecondsPerAttack()
        {
            float rate = definition.fireRate * TempOverdrive.FireRateScale;
            return rate > 0f ? 1f / rate : 0f;
        }

        public Vector2 ConsumeRecoil()
        {
            Vector2 kick = _pendingRecoil;
            _pendingRecoil = Vector2.zero;
            return kick;
        }

        public virtual float DamageWithCrit(out bool crit)
        {
            crit = UnityEngine.Random.value < definition.criticalChance;
            // PlayerStats attack scaling is centralized here so hitscan, projectile and melee share it.
            var stats = PlayerStats.Instance;
            float statsScale = stats != null ? stats.AttackMultiplier : 1f;
            return definition.damage * statsScale * (crit ? definition.criticalMultiplier : 1f);
        }

        protected Vector3 ApplySpread(Vector3 direction)
        {
            float spread = definition.spreadDegrees * SpreadScale;
            if (spread <= 0f) return direction.normalized;
            return Quaternion.Euler(
                UnityEngine.Random.Range(-spread, spread),
                UnityEngine.Random.Range(-spread, spread),
                0f) * direction.normalized;
        }

        protected void SpawnMuzzleFlash(Transform firePoint)
        {
            if (firePoint == null) return;

            AudioDirector.Instance?.PlayShot(firePoint.position);
            if (_rig == null) _rig = GetComponentInParent<CharacterVisualRig>();
            if (_rig != null) _rig.PlayAttack();

            if (definition.muzzleFlashPrefab != null)
            {
                var flash = Instantiate(definition.muzzleFlashPrefab, firePoint.position, firePoint.rotation);
                Destroy(flash, 0.1f);
                return;
            }

            // Authored art may not exist yet: an emissive bead plus bloom reads as a
            // muzzle flare without needing a particle prefab. The 60 ms lifetime is
            // also what caps how many can be on screen at once.
            Material flare = FlareMaterial;
            if (flare == null) return;

            var bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var collider = bead.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            bead.name = "MuzzleFlare";
            bead.transform.position = firePoint.position + firePoint.forward * 0.12f;
            bead.transform.localScale = Vector3.one * 0.22f;
            bead.GetComponent<Renderer>().sharedMaterial = flare;
            Destroy(bead, 0.06f);
        }

        private CharacterVisualRig _rig;

        private static Material _flareMaterial;
        private static bool _flareMissing;

        private static Material FlareMaterial
        {
            get
            {
                if (_flareMaterial != null) return _flareMaterial;
                if (_flareMissing) return null;

                // "Standard" does not exist in a URP player and Shader.Find returns null
                // for it, which used to hand back a material with no shader at all.
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    _flareMissing = true;
                    Debug.LogError("[NullFrontier] URP Lit is unavailable: muzzle flares " +
                                   "are disabled.");
                    return null;
                }

                _flareMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                _flareMaterial.color = new Color(1f, 0.86f, 0.55f);
                _flareMaterial.SetColor("_EmissionColor", new Color(1f, 0.78f, 0.42f) * 7f);
                _flareMaterial.EnableKeyword("_EMISSION");
                _flareMaterial.SetFloat("_Smoothness", 1f);
                return _flareMaterial;
            }
        }

        protected void ReportGunshot(Vector3 origin, Vector3 direction)
        {
            float range = definition != null ? definition.range : 40f;
            EnemyBase.AlertNearby(origin, alertRadius, origin + direction.normalized * range);
        }
    }
}
