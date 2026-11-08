using UnityEngine;

namespace NullFrontier.Core
{
    /// <summary>
    /// Keeps world objectives moving while nothing else happens. A static neon cube is
    /// level geometry: the player cannot tell it from a wall light across a street. A
    /// slow bob, a yaw drift and a breathing glow make an interactive object read as
    /// interactive from twenty metres away, and cost three transform writes per object.
    /// </summary>
    public class AmbientMotion : MonoBehaviour
    {
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSeconds = 2.6f;
        [SerializeField] private float spinDegreesPerSecond = 26f;
        [SerializeField] private float glowSeconds = 2.0f;
        [SerializeField] private float glowAmount = 0.55f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseId = Shader.PropertyToID("_BaseColor");

        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _baseEmission;
        private int _colorId;
        private float _phase;
        private float _spin;

        private void Awake()
        {
            _basePosition = transform.position;
            _baseRotation = transform.rotation;

            // Objects on the same street must not breathe in step - a shared phase turns
            // a row of caches into one flashing block.
            _phase = _basePosition.x * 0.37f + _basePosition.z * 0.53f + _basePosition.y;

            _renderer = GetComponent<Renderer>();
            if (_renderer == null || glowAmount <= 0f) return;

            // Unlit neon stores its glow in the base colour, Lit neon in the emission
            // colour; scale whichever one the material actually carries.
            var shared = _renderer.sharedMaterial;
            _colorId = shared != null && shared.HasProperty(EmissionId) ? EmissionId : BaseId;
            _baseEmission = shared != null ? shared.GetColor(_colorId) : Color.clear;
            if (Mathf.Max(_baseEmission.r, Mathf.Max(_baseEmission.g, _baseEmission.b)) <= 0f)
                _renderer = null;
        }

        private void OnDisable()
        {
            transform.SetPositionAndRotation(_basePosition, _baseRotation);
        }

        private void LateUpdate()
        {
            float t = Time.time + _phase;

            float bob = bobHeight <= 0f ? 0f
                : bobHeight * Mathf.Sin(2f * Mathf.PI * t / Mathf.Max(0.1f, bobSeconds));
            _spin += spinDegreesPerSecond * Time.deltaTime;

            transform.SetPositionAndRotation(_basePosition + Vector3.up * bob,
                _baseRotation * Quaternion.Euler(0f, _spin, 0f));

            if (_renderer == null || glowAmount <= 0f) return;

            float breath = 1f + glowAmount * Mathf.Sin(2f * Mathf.PI * t / Mathf.Max(0.1f, glowSeconds));
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.SetColor(_colorId, _baseEmission * breath);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
