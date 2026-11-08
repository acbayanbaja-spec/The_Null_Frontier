using UnityEngine;

namespace NullFrontier.Player
{
    /// <summary>
    /// Frame input snapshot consumed by PlayerController. Filled by an input provider:
    /// DesktopInputProvider (keyboard/mouse/gamepad via Input System) or
    /// MobileInputProvider (on-screen joystick + touch buttons).
    /// </summary>
    public class PlayerInputState
    {
        public Vector2 Move;
        public Vector2 LookDelta;
        public bool JumpPressed;
        public bool SprintHeld;
        public bool CrouchHeld;
        public bool FireHeld;
        public bool AimHeld;
        public bool InteractPressed;
        public bool ReloadPressed;
        public bool AbilityPressed;
        public bool WeaponSwitchPressed;
        public bool PausePressed;
        public bool DodgePressed;

        // Set by providers each frame before gameplay reads the snapshot.
        public void Reset()
        {
            Move = Vector2.zero;
            LookDelta = Vector2.zero;
            JumpPressed = false;
            SprintHeld = false;
            CrouchHeld = false;
            FireHeld = false;
            AimHeld = false;
            InteractPressed = false;
            ReloadPressed = false;
            AbilityPressed = false;
            WeaponSwitchPressed = false;
            PausePressed = false;
            DodgePressed = false;
        }
    }
}
