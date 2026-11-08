using NullFrontier.Characters;
using UnityEngine;

namespace NullFrontier.Combat
{
    /// <summary>
    /// Puts the equipped weapon in the operative's hand and gives the firing code a real
    /// muzzle to shoot from.
    ///
    /// The loadout used to equip empty GameObjects: the player could fire a gun that had
    /// never been rendered, from a FirePoint welded to the chest that did not follow the
    /// animated arm. The meshes are authored by Assets/Editor/WeaponModels.cs.
    ///
    /// The gun is parented to the hand bone so it travels with the walk and shoot
    /// animations, but its rotation is written in world space from the aim direction every
    /// frame. Free rig bones have no agreed orientation - this one is "Fist.R" and points
    /// wherever the exporter left it - so aligning to aim is the only way the barrel is
    /// guaranteed to point where the player is pointing.
    /// </summary>
    [DefaultExecutionOrder(240)]
    public class WeaponVisualMount : MonoBehaviour
    {
        [SerializeField] private string weaponFolder = "Weapons";
        [SerializeField] private Transform defaultSocket;
        // Where the gun sits relative to the hand once it is aimed: pushed along the aim
        // line and dropped below it. Written in world axes rather than bone axes because
        // a free rig bone has no agreed orientation, and a rifle held at the raw hand
        // position of a close over-the-shoulder rig fills the sight line.
        [SerializeField] private float aimPush = 0.05f;
        [SerializeField] private float dropBelowAim = 0.08f;
        [SerializeField] private float sideOffset = 0.06f;
        // Nose the barrel down a few degrees. A gun welded perfectly level cuts straight
        // across the sight line; the slight drop is also how a rifle looks carried.
        [SerializeField] private float pitchDegrees = -5f;
        // Trim on the gun's real-world size: 1 = exactly as authored by WeaponModels,
        // which draws a 0.75 m rifle and a 0.22 m pistol.
        [SerializeField] private float sizeMultiplier = 1f;

        private static readonly string[] SocketNames =
        {
            "Fist.R", "Fist_R", "Hand.R", "Hand_R", "RightHand", "WeaponSocket"
        };

        private WeaponLoadout _loadout;
        private Camera _camera;
        private Transform _socket;
        private GameObject _instance;
        private Transform _muzzle;
        private string _loadedId;
        private bool _warned;

        /// <summary>Barrel tip of the weapon now in hand, or null while the hands are empty.</summary>
        public Transform Muzzle => _muzzle;

        private void Awake()
        {
            _loadout = GetComponent<WeaponLoadout>();
            _camera = Camera.main;
        }

        private void Start()
        {
            if (_loadout != null) _loadout.OnWeaponChanged += Apply;
            Apply(_loadout != null ? _loadout.CurrentWeapon : null);
        }

        private void OnDestroy()
        {
            if (_loadout != null) _loadout.OnWeaponChanged -= Apply;
        }

        private void LateUpdate()
        {
            if (_instance == null || _socket == null) return;

            Vector3 aim = _camera != null ? _camera.transform.forward : transform.forward;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.0001f) return;
            aim.Normalize();

            // Roll is left out on purpose: the camera can bank, and a gun that banks with
            // it looks like it is being thrown rather than aimed.
            _instance.transform.rotation = Quaternion.LookRotation(aim, Vector3.up)
                * Quaternion.Euler(pitchDegrees, 0f, 0f);

            // Re-read from the hand every frame so the walk animation still carries the
            // gun, but place it along the aim line instead of at the raw bone position.
            _instance.transform.position = _socket.position
                + aim * aimPush
                + Vector3.Cross(Vector3.up, aim) * sideOffset
                - Vector3.up * dropBelowAim;

            MatchAuthoredSize(_instance.transform, _socket, sizeMultiplier);
        }

        /// <summary>
        /// Undoes the bone chain's scale so a socketed prop stays the size it was authored
        /// at. The imported rigs compensate a centimetre FBX by scaling the armature node
        /// 100x under a Visual root scaled ~0.53, which leaves a hand bone at a world scale
        /// of about 54 - a 0.75 m rifle parented to it rendered as a 40 m beam that filled
        /// the screen and put its muzzle 25 m in front of the shooter.
        ///
        /// Called every frame rather than once at equip because it is three multiplies and
        /// it keeps the gun honest if an animation ever scales a bone.
        /// </summary>
        public static void MatchAuthoredSize(Transform instance, Transform socket, float multiplier)
        {
            Vector3 bone = socket.lossyScale;
            float scale = Mathf.Max(Mathf.Abs(bone.x), Mathf.Max(Mathf.Abs(bone.y), Mathf.Abs(bone.z)));
            if (scale < 0.0001f) scale = 1f;
            instance.localScale = Vector3.one * (multiplier / scale);
        }

        private void Apply(Weapon weapon)
        {
            var definition = weapon != null ? weapon.Definition : null;
            string id = definition != null ? definition.weaponId : null;

            if (id == _loadedId && _instance != null) return;
            _loadedId = id;

            if (_instance != null) Destroy(_instance);
            _instance = null;
            _muzzle = null;
            if (string.IsNullOrEmpty(id)) return;

            var prefab = Resources.Load<GameObject>($"{weaponFolder}/{id}");
            if (prefab == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning($"[NullFrontier] No weapon model at Resources/{weaponFolder}/{id}. " +
                                     "Run Null Frontier > Rebuild Weapon Models, then Build All Scenes.");
                }
                return;
            }

            _socket = FindSocket();
            _instance = Instantiate(prefab, _socket);
            _instance.name = prefab.name;
            _muzzle = _instance.transform.Find("Muzzle");
        }

        private Transform FindSocket()
        {
            Transform fallback = defaultSocket != null ? defaultSocket : transform;

            // The rigged model is a child of the controller and is replaced when the
            // character changes, so the hand is looked up per equip rather than cached.
            foreach (var candidate in GetComponentsInChildren<Transform>(true))
            {
                for (int i = 0; i < SocketNames.Length; i++)
                    if (string.Equals(candidate.name, SocketNames[i], System.StringComparison.OrdinalIgnoreCase))
                        return candidate;
            }

            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning("[NullFrontier] No hand bone on the character visual: the weapon " +
                                 "is held at the chest socket instead. Check the rig's bone names.");
            }
            return fallback;
        }
    }
}
