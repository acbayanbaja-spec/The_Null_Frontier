using NullFrontier.Characters;
using NullFrontier.Combat;
using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Player
{
    /// <summary>
    /// Third-person character controller driven by CharacterController physics.
    /// Handles walk/run/sprint (stamina-gated), jump, gravity, crouch, dodge with
    /// i-frames, and camera-relative movement. Animation is fed through Animator
    /// parameters (Speed, MotionY, Grounded, Crouching) so any rig can be dropped in.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float runSpeed = 5.2f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float crouchMultiplier = 0.5f;
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float rotationSmoothTime = 0.08f;

        [Header("Dodge")]
        [SerializeField] private float dodgeSpeed = 9f;
        [SerializeField] private float dodgeDuration = 0.35f;
        [SerializeField] private float dodgeIFrames = 0.3f;

        [Header("Stamina costs per second / per action")]
        [SerializeField] private float sprintStaminaPerSecond = 12f;
        [SerializeField] private float dodgeStaminaCost = 20f;

        [Header("References")]
        [SerializeField] private Transform cameraRig;
        [SerializeField] private Animator animator;

        public bool IsDodging { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsGrounded { get; private set; }
        public Vector3 Velocity => _velocity;

        private CharacterController _controller;
        private PlayerInputState _input;
        private Stamina _stamina;
        private Health _health;

        private Vector3 _velocity;
        private float _verticalVelocity;
        private float _targetYaw;
        private float _dodgeTimer;
        private float _iFrameTimer;
        private float _speedSmoothVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _stamina = GetComponent<Stamina>();
            _health = GetComponent<Health>();
            _input = new PlayerInputState();
        }

        private void Start()
        {
            var def = CharacterRegistry.Get(GameManager.Instance?.SelectedCharacterId ?? "iidreuspade");
            runSpeed = def.MoveSpeed;
            _provider = FindInputProvider();
        }

        private IInputProvider _provider;

        private IInputProvider FindInputProvider()
        {
            var providers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var candidate in providers)
            {
                if (candidate is IInputProvider p && p.IsActive) return p;
            }
            return GetComponent<IInputProvider>();
        }

        private void Update()
        {
            // A player object from an older scene build has no CharacterController, and
            // the resulting exception fired every frame until the console was unusable.
            // Report the real fault once, then stop updating.
            if (_controller == null)
            {
                Debug.LogError($"[NullFrontier] {name} has no CharacterController, so movement is disabled. " +
                               "Rebuild the scene with Null Frontier > Build All Scenes.");
                enabled = false;
                return;
            }

            if (_health != null && !_health.IsAlive) return;

            _input.Reset();
            _provider?.Poll(_input);

            IsGrounded = _controller.isGrounded;
            if (IsGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;

            HandleCrouch();
            HandleDodge();
            HandleMovement();
            HandleJump();
            HandleInteract();
            UpdateAnimator();
        }

        private void HandleInteract()
        {
            if (_input.InteractPressed) GameEvents.RaisePlayerInteracted(transform.position);
        }

        private void HandleMovement()
        {
            if (IsDodging) return;

            Vector2 input = Vector2.ClampMagnitude(_input.Move, 1f);
            bool moving = input.sqrMagnitude > 0.01f;

            IsSprinting = _input.SprintHeld && moving && _stamina != null && _stamina.HasStamina;
            IsCrouching = _input.CrouchHeld;

            float targetSpeed = moving ? runSpeed : 0f;
            if (IsSprinting) targetSpeed *= sprintMultiplier;
            if (IsCrouching) targetSpeed *= crouchMultiplier;
            targetSpeed *= TempOverdrive.MoveSpeedScale;

            if (IsSprinting)
            {
                _stamina?.Drain(sprintStaminaPerSecond * Time.deltaTime);
            }

            Vector3 direction = Vector3.zero;
            if (moving)
            {
                float cameraYaw = cameraRig != null ? cameraRig.eulerAngles.y : transform.eulerAngles.y;
                _targetYaw = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + cameraYaw;
                direction = Quaternion.Euler(0f, _targetYaw, 0f) * Vector3.forward;
            }

            float smoothSpeed = Mathf.SmoothDamp(
                new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude,
                targetSpeed, ref _speedSmoothVelocity, rotationSmoothTime);

            _velocity = direction * smoothSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(_velocity * Time.deltaTime);

            if (moving)
            {
                float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetYaw,
                    ref _rotationSmoothVelocityStore, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private float _rotationSmoothVelocityStore;

        private void HandleJump()
        {
            if (!IsDodging && _input.JumpPressed && IsGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            _verticalVelocity += gravity * Time.deltaTime;
        }

        private void HandleCrouch()
        {
            float targetHeight = _input.CrouchHeld ? 1.1f : 1.8f;
            _controller.height = Mathf.Lerp(_controller.height, targetHeight, Time.deltaTime * 10f);
        }

        private void HandleDodge()
        {
            if (_iFrameTimer > 0f) _iFrameTimer -= Time.deltaTime;

            if (IsDodging)
            {
                _dodgeTimer -= Time.deltaTime;
                if (_dodgeTimer <= 0f) IsDodging = false;
                return;
            }

            if (!_input.DodgePressed) return;
            if (_stamina != null && !_stamina.TrySpend(dodgeStaminaCost)) return;

            IsDodging = true;
            _dodgeTimer = dodgeDuration;
            _iFrameTimer = dodgeIFrames;

            Vector3 direction = _input.Move.sqrMagnitude > 0.01f
                ? Quaternion.Euler(0f, _targetYaw, 0f) * Vector3.forward
                : transform.forward;
            _velocity = direction * dodgeSpeed;
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;
            Vector3 planarVelocity = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z);
            animator.SetFloat("Speed", planarVelocity.magnitude, 0.1f, Time.deltaTime);
            animator.SetFloat("MotionY", _controller.velocity.y);
            animator.SetBool("Grounded", IsGrounded);
            animator.SetBool("Crouching", IsCrouching);
        }

        public bool IsInvulnerable => _iFrameTimer > 0f;
    }
}
