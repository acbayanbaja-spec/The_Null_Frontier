using UnityEngine;
using UnityEngine.InputSystem;

namespace NullFrontier.Player
{
    /// <summary>
    /// Fills PlayerInputState from the Input System (keyboard/mouse/gamepad).
    /// Active on desktop and editor; mobile uses MobileInputProvider instead.
    /// </summary>
    public class DesktopInputProvider : MonoBehaviour, IInputProvider
    {
        [SerializeField] private float lookSensitivity = 0.08f;

        public PlayerInputActions Actions { get; private set; }
        public bool IsActive => !Application.isMobilePlatform;

        private void Awake()
        {
            Actions = new PlayerInputActions();
        }

        private void OnEnable() => Actions.Enable();
        // Destroy order at play-mode exit can run OnDisable before Awake has built the
        // action map; without the guard every teardown logged a null-reference.
        private void OnDisable() => Actions?.Disable();

        public void Poll(PlayerInputState state)
        {
            state.Move = Actions.Move.ReadValue<Vector2>();
            state.LookDelta = Actions.Look.ReadValue<Vector2>() * lookSensitivity;
            state.SprintHeld = Actions.Sprint.IsPressed();
            state.CrouchHeld = Actions.Crouch.IsPressed();
            state.FireHeld = Actions.Fire.IsPressed();
            state.AimHeld = Actions.Aim.IsPressed();
            state.JumpPressed |= Actions.Jump.WasPressedThisFrame();
            state.DodgePressed |= Actions.Dodge.WasPressedThisFrame();
            state.InteractPressed |= Actions.Interact.WasPressedThisFrame();
            state.ReloadPressed |= Actions.Reload.WasPressedThisFrame();
            state.AbilityPressed |= Actions.Ability.WasPressedThisFrame();
            state.WeaponSwitchPressed |= Actions.WeaponSwitch.WasPressedThisFrame();
            state.PausePressed |= Actions.Pause.WasPressedThisFrame();
        }
    }

    public interface IInputProvider
    {
        bool IsActive { get; }
        void Poll(PlayerInputState state);
    }
}
