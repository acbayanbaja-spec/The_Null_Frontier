using NullFrontier.Combat;
using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Characters
{
    /// <summary>
    /// Keeps a rigged model honest about how it is being moved. Works for the
    /// player (CharacterController) and for enemies (NavMeshAgent) alike because
    /// it only watches the transform, never the mover. Also owns the death pose
    /// and the damage flash.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class CharacterVisualRig : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float speedSmoothTime = 0.12f;
        [SerializeField] private float flashDuration = 0.22f;
        [SerializeField] private Color flashColor = new Color(1f, 0.35f, 0.3f);
        [SerializeField] private float footstepIntervalAtFullSpeed = 0.42f;

        [Tooltip("Machines spark and come apart; living things bleed.")]
        [SerializeField] private bool biological = true;

        private static readonly Color Blood = new Color(0.34f, 0.02f, 0.035f);
        private static readonly Color Coolant = new Color(0.95f, 0.45f, 0.1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Health _health;
        private Renderer[] _renderers;
        private Color[] _baseColors;
        private int[] _colorPropertyIds;
        private MaterialPropertyBlock _block;
        private Vector3 _lastPosition;
        private float _smoothedSpeed;
        private float _speedVelocity;
        private float _flashTimer;
        private float _footstepTimer;
        private bool _hasAnimator;
        private bool _dead;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _hasAnimator = animator != null && animator.runtimeAnimatorController != null;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _lastPosition = transform.position;
            _health = FindHealth();
            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
                _health.OnDied += HandleDied;
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnDamaged -= HandleDamaged;
                _health.OnDied -= HandleDied;
            }
        }

        private void OnEnable() => _lastPosition = transform.position;

        private void Update()
        {
            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            delta.y = 0f;

            float measuredSpeed = delta.magnitude / Mathf.Max(0.0001f, Time.deltaTime);
            if (_dead) measuredSpeed = 0f;
            _smoothedSpeed = Mathf.SmoothDamp(_smoothedSpeed, measuredSpeed, ref _speedVelocity, speedSmoothTime);

            if (_hasAnimator)
            {
                animator.SetFloat("Speed", _smoothedSpeed);
                animator.SetBool("Grounded", true);
            }

            UpdateFootsteps(measuredSpeed);

            if (_flashTimer > 0f)
            {
                _flashTimer = Mathf.Max(0f, _flashTimer - Time.deltaTime);
                ApplyFlash(_flashTimer / Mathf.Max(0.0001f, flashDuration));
            }
        }

        /// <summary>Calls the ranged/melee attack take on the current model.</summary>
        public void PlayAttack()
        {
            if (!_hasAnimator || _dead) return;
            animator.SetTrigger("Shoot");
        }

        /// <summary>Tints the damage flash toward this character's accent colour.</summary>
        public void SetFlashColor(Color color)
        {
            if (color == Color.black) return;
            flashColor = color;
        }

        /// <summary>Re-caches renderers after a model is mounted underneath this rig.</summary>
        public void RefreshRenderers()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _hasAnimator = animator != null && animator.runtimeAnimatorController != null;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseColors = null;
            _block = null;
        }

        private void HandleDamaged(DamageInfo info)
        {
            if (_dead) return;
            _flashTimer = flashDuration;

            // The damage itself is instant, so without this the player never sees why a
            // body dropped. Where the round landed is already in the damage, and the shot
            // direction says which way the body should be thrown.
            Vector3 where = info.Point.sqrMagnitude > 0.0001f
                ? info.Point : transform.position + Vector3.up * 1.05f;
            float severity = Mathf.Clamp01(info.Amount / 45f);
            CombatEffects.Wound(where, info.Direction, severity,
                biological ? Blood : Coolant);

            if (!_hasAnimator) return;
            if (info.Amount >= 1f) animator.SetTrigger("Hit");
        }

        private void HandleDied()
        {
            _dead = true;
            _flashTimer = 0f;
            ClearFlash();

            Vector3 centre = transform.position + Vector3.up * 0.95f;
            CombatEffects.Death(centre, biological ? Blood : Coolant, biological);

            if (_hasAnimator)
            {
                animator.SetFloat("Speed", 0f);
                animator.SetTrigger("Die");
            }
        }

        private void UpdateFootsteps(float speed)
        {
            if (speed < 0.4f) { _footstepTimer = 0f; return; }
            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer > 0f) return;
            _footstepTimer = footstepIntervalAtFullSpeed * Mathf.Clamp01(5.5f / Mathf.Max(0.5f, speed));
            AudioDirector.Instance?.PlayFootstep(transform.position, speed);
        }

        private void ApplyFlash(float fraction)
        {
            if (_renderers == null || _renderers.Length == 0) return;
            if (_baseColors == null) CacheBaseColors();
            if (_block == null) _block = new MaterialPropertyBlock();

            // Not emission: the _EMISSION keyword is baked into the material, so a
            // property block writing _EmissionColor would silently do nothing.
            Color hot = flashColor * (1f + 1.6f * fraction);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null || _colorPropertyIds[i] < 0) continue;
                renderer.GetPropertyBlock(_block);
                _block.SetColor(_colorPropertyIds[i], Color.Lerp(_baseColors[i], hot, fraction));
                renderer.SetPropertyBlock(_block);
            }
        }

        private void CacheBaseColors()
        {
            _baseColors = new Color[_renderers.Length];
            _colorPropertyIds = new int[_renderers.Length];
            var probe = new MaterialPropertyBlock();
            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                _baseColors[i] = Color.white;
                _colorPropertyIds[i] = -1;
                if (renderer == null) continue;

                // Only write a property the shader actually declares; a missing one is
                // logged as a warning every frame of the flash.
                var material = renderer.sharedMaterial;
                if (material == null) continue;
                int property = material.HasProperty(BaseColorId) ? BaseColorId
                    : material.HasProperty(ColorId) ? ColorId : -1;
                if (property < 0) continue;

                renderer.GetPropertyBlock(probe);
                _colorPropertyIds[i] = property;
                _baseColors[i] = probe.HasProperty(property)
                    ? probe.GetColor(property)
                    : material.GetColor(property);
            }
        }

        private void ClearFlash()
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.Clear();
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_block);
            _baseColors = null;
        }

        private Health FindHealth()
        {
            var own = GetComponent<Health>();
            if (own != null) return own;
            own = GetComponentInParent<Health>();
            return own != null ? own : GetComponentInChildren<Health>(true);
        }
    }
}
