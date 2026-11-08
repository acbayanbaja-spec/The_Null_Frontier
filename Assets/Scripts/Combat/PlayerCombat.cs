using NullFrontier.Core;
using NullFrontier.Player;
using UnityEngine;

namespace NullFrontier.Combat
{
    [RequireComponent(typeof(WeaponLoadout))]
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private Transform firePoint;
        [SerializeField] private float aimRange = 100f;
        [SerializeField, Range(0.05f, 1f)] private float aimSpreadScale = 0.4f;

        public Weapon CurrentWeapon => _loadout != null ? _loadout.CurrentWeapon : null;
        public Vector2 ConsumeRecoil() => CurrentWeapon != null ? CurrentWeapon.ConsumeRecoil() : Vector2.zero;

        private WeaponLoadout _loadout;
        private Health _health;
        private WeaponVisualMount _visuals;
        private IInputProvider _provider;
        private readonly PlayerInputState _input = new PlayerInputState();
        private Camera _cam;

        private void Awake()
        {
            _loadout = GetComponent<WeaponLoadout>();
            _health = GetComponent<Health>();
            _visuals = GetComponent<WeaponVisualMount>();
        }

        private void Start()
        {
            _cam = Camera.main;
            _provider = FindInputProvider();
        }

        private void Update()
        {
            if (_health != null && !_health.IsAlive) return;

            _input.Reset();
            _provider?.Poll(_input);

            var weapon = CurrentWeapon;
            if (weapon == null) return;

            weapon.SpreadScale = _input.AimHeld ? aimSpreadScale : 1f;

            if (_input.ReloadPressed) weapon.Reload();

            if (_input.FireHeld)
            {
                Transform muzzle = Muzzle;
                weapon.TryFire(muzzle, GetAimDirection(muzzle));
            }
        }

        /// <summary>
        /// The barrel tip of whatever is in hand. The chest FirePoint is only a fallback
        /// for a scene built before the weapon models existed - a round that starts in the
        /// sternum also starts inside whatever the player is standing against.
        /// </summary>
        private Transform Muzzle
        {
            get
            {
                if (_visuals != null && _visuals.Muzzle != null) return _visuals.Muzzle;
                return firePoint != null ? firePoint : transform;
            }
        }

        private Vector3 GetAimDirection(Transform muzzle)
        {
            Vector3 origin = muzzle.position;
            if (_cam == null) return muzzle.forward;

            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            Vector3 aimPoint;
            if (Physics.Raycast(ray, out RaycastHit hit, aimRange,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
            {
                aimPoint = hit.point;
            }
            else
            {
                aimPoint = ray.GetPoint(aimRange);
            }

            Vector3 dir = aimPoint - origin;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : ray.direction;
        }

        private IInputProvider FindInputProvider()
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IInputProvider provider && provider.IsActive)
                    return provider;
            }
            return GetComponent<IInputProvider>();
        }
    }
}
