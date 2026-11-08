using Unity.Cinemachine;
using UnityEngine;

namespace NullFrontier.Player
{
    /// <summary>
    /// Third-person camera rig. Owns the yaw rig (child of player) and pitch pivot;
    /// a Cinemachine 3 CinemachineCamera with ThirdPersonFollow sits on the pivot and
    /// handles following, shoulder offset and camera collision.
    /// Hierarchy this script expects (created by PlayerRigFactory if missing):
    ///   Player
    ///     CameraRig (yaw)
    ///       CameraPivot (pitch)
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] private Transform yawRig;
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private float sensitivity = 1f;
        [SerializeField] private float touchSensitivity = 0.5f;
        [SerializeField] private float pitchMin = -35f;
        [SerializeField] private float pitchMax = 70f;
        // The lens that CinemachineThirdPersonFollow is framed against: at 52 deg the
        // operative fills about half the frame at the rig's 3.8 m distance. Widening this
        // back to 60 makes the same rig read as a distant action-cam shot again.
        [SerializeField] private float aimFov = 40f;
        [SerializeField] private float normalFov = 52f;
        [SerializeField] private float fovLerpSpeed = 10f;

        /// <summary>
        /// The virtual camera that owns the lens. CinemachineBrain copies its Lens onto the
        /// Unity camera every frame, so writing camera.fieldOfView directly is overwritten
        /// and the aim punch-in never shows; the lens has to be driven at its source.
        /// </summary>
        [SerializeField] private CinemachineCamera lensSource;

        /// <summary>
        /// User-facing multiplier from the settings menu (0.2x - 3x). Static because the
        /// settings screen lives in a different scene from the rig that reads it.
        /// </summary>
        public static float LookSensitivity { get; set; } = 1f;

        private PlayerInputState _input;
        private IInputProvider _provider;
        private Camera _camera;
        private float _pitch;
        private float _yaw;

        private void Awake()
        {
            _input = new PlayerInputState();
            _camera = Camera.main;
        }

        private void Start()
        {
            _provider = FindInputProvider();
            if (yawRig == null && transform.childCount > 0) yawRig = transform.GetChild(0);
            if (pitchPivot == null && yawRig != null && yawRig.childCount > 0) pitchPivot = yawRig.GetChild(0);
            if (yawRig != null) _yaw = yawRig.eulerAngles.y;
        }

        private IInputProvider FindInputProvider()
        {
            var candidates = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var candidate in candidates)
            {
                if (candidate is IInputProvider p && p.IsActive) return p;
            }
            return GetComponent<IInputProvider>();
        }

        private void LateUpdate()
        {
            // A rig left over from an older scene build has no yaw/pitch transforms, and
            // throwing every frame buried the real fault under hundreds of identical
            // traces. Say it once, then stop.
            if (yawRig == null || pitchPivot == null)
            {
                Debug.LogError($"[NullFrontier] {name} has no camera rig, so look input is disabled. " +
                               "Rebuild the scene with Null Frontier > Build All Scenes.");
                enabled = false;
                return;
            }

            // Play-mode teardown destroys the provider pair mid-frame; a guard here keeps
            // the one remaining LateUpdate from flooding the console with null references.
            if (_input == null) return;
            _input.Reset();
            _provider?.Poll(_input);

            if (_input.LookDelta.sqrMagnitude > 0.0001f)
            {
                float sens = (Application.isMobilePlatform ? touchSensitivity : sensitivity)
                    * LookSensitivity;
                _yaw += _input.LookDelta.x * sens;
                _pitch = Mathf.Clamp(_pitch - _input.LookDelta.y * sens, pitchMin, pitchMax);
            }

            // The rig is a child of the player, so its world yaw must be assigned from the
            // look value rather than accumulated in local space. PlayerController reads
            // cameraRig.eulerAngles.y to turn camera-relative input into a heading and then
            // rotates the player toward it; while the rig's yaw contained the player's own
            // yaw, that heading chased itself by the leftover look angle every frame and the
            // operative walked in a circle for as long as W was held.
            yawRig.rotation = Quaternion.Euler(0f, _yaw, 0f);
            pitchPivot.localEulerAngles = new Vector3(_pitch, 0f, 0f);

            float targetFov = _input.AimHeld ? aimFov : normalFov;
            float ease = Time.deltaTime * fovLerpSpeed;
            if (lensSource != null)
            {
                var lens = lensSource.Lens;
                lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, ease);
                lensSource.Lens = lens;
            }
            else if (_camera != null)
            {
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, ease);
            }
        }
    }
}
