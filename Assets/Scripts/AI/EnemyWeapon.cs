using UnityEngine;

namespace NullFrontier.AI
{
    /// <summary>
    /// Puts a gun in a bot's hand.
    ///
    /// The enemies shot from an invisible point at chest height: the player could see a
    /// rifle-shaped nothing, hear a shot and take damage with nothing travelling between
    /// them. This mounts one of the same weapon models the player uses on the rig's hand
    /// bone and exposes the barrel tip so the tracer starts where the muzzle is.
    ///
    /// The gun is aimed from the body's own forward rather than from a camera: a bot turns
    /// its NavMesh agent at the target, so the model and the round agree by construction.
    /// </summary>
    public class EnemyWeapon : MonoBehaviour
    {
        [SerializeField] private string weaponFolder = "Weapons";
        [SerializeField] private string weaponId;
        [SerializeField] private float aimPush = 0.04f;
        [SerializeField] private float sideOffset = 0.05f;
        [SerializeField] private float dropBelowAim = 0.05f;
        [SerializeField] private float pitchDegrees = -4f;
        [SerializeField] private float sizeMultiplier = 1f;

        private static readonly string[] SocketNames =
        {
            "Fist.R", "Fist_R", "Hand.R", "Hand_R", "RightHand", "WeaponSocket"
        };

        private Transform _socket;
        private GameObject _instance;
        private Transform _muzzle;
        private bool _warned;

        /// <summary>Barrel tip of the gun in hand, or null while the hands are empty.</summary>
        public Transform Muzzle => _muzzle;

        private void Awake()
        {
            if (string.IsNullOrEmpty(weaponId)) return;

            var prefab = Resources.Load<GameObject>($"{weaponFolder}/{weaponId}");
            if (prefab == null)
            {
                Warn($"No weapon model at Resources/{weaponFolder}/{weaponId}. " +
                     "Run Null Frontier > Rebuild Weapon Models.");
                return;
            }

            _socket = FindSocket();
            _instance = Instantiate(prefab, _socket);
            _instance.name = prefab.name;
            _muzzle = _instance.transform.Find("Muzzle");
        }

        private void LateUpdate()
        {
            if (_instance == null || _socket == null) return;

            Vector3 aim = transform.forward;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.0001f) return;
            aim.Normalize();

            _instance.transform.rotation = Quaternion.LookRotation(aim, Vector3.up)
                * Quaternion.Euler(pitchDegrees, 0f, 0f);
            _instance.transform.position = _socket.position
                + aim * aimPush
                + Vector3.Cross(Vector3.up, aim) * sideOffset
                - Vector3.up * dropBelowAim;

            // Same bone-scale trap as the player's rifle: without this every bot carries a
            // beam tens of metres long, and its muzzle - the start of the tracer the player
            // is meant to see - sits far outside the barrel.
            NullFrontier.Combat.WeaponVisualMount.MatchAuthoredSize(
                _instance.transform, _socket, sizeMultiplier);
        }

        private Transform FindSocket()
        {
            foreach (var candidate in GetComponentsInChildren<Transform>(true))
            {
                for (int i = 0; i < SocketNames.Length; i++)
                {
                    if (string.Equals(candidate.name, SocketNames[i],
                            System.StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }

            Warn("No hand bone on this bot: the gun is held at the body origin.");
            return transform;
        }

        private void Warn(string message)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[NullFrontier] {name}: {message}");
        }
    }
}
