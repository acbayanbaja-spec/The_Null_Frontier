using NullFrontier.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace NullFrontier.Player
{
    /// <summary>
    /// Touch input provider. Reads the left VirtualJoystick for movement and the
    /// right-half screen drag for camera look; action buttons are bound in the
    /// inspector. Active only on touch-capable platforms.
    /// </summary>
    public class MobileInputProvider : MonoBehaviour, IInputProvider
    {
        [Header("Movement")]
        [SerializeField] private VirtualJoystick moveJoystick;

        [Header("Action buttons (optional)")]
        [SerializeField] private TouchButton jumpButton;
        [SerializeField] private TouchButton sprintButton;
        [SerializeField] private TouchButton crouchButton;
        [SerializeField] private TouchButton fireButton;
        [SerializeField] private TouchButton aimButton;
        [SerializeField] private TouchButton reloadButton;
        [SerializeField] private TouchButton interactButton;
        [SerializeField] private TouchButton abilityButton;
        [SerializeField] private TouchButton dodgeButton;
        [SerializeField] private TouchButton switchButton;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 0.4f;

        public bool IsActive => Application.isMobilePlatform || Touchscreen.current != null;

        private Vector2 _lookDelta;

        private void OnEnable() => EnhancedTouchSupport.Enable();
        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void Update()
        {
            _lookDelta = Vector2.zero;
            var touches = EnhancedTouch.activeTouches;
            if (touches.Count == 0) return;

            // Right-half drags that do not start on UI rotate the camera.
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.screenPosition.x < Screen.width * 0.4f) continue;
                if (IsOverUi(touch)) continue;
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    _lookDelta += touch.delta * lookSensitivity;
                }
            }
        }

        public void Poll(PlayerInputState state)
        {
            if (moveJoystick != null) state.Move = moveJoystick.Value;
            state.LookDelta = _lookDelta;

            state.JumpPressed |= ConsumePressed(jumpButton);
            state.DodgePressed |= ConsumePressed(dodgeButton);
            state.InteractPressed |= ConsumePressed(interactButton);
            state.ReloadPressed |= ConsumePressed(reloadButton);
            state.AbilityPressed |= ConsumePressed(abilityButton);
            state.WeaponSwitchPressed |= ConsumePressed(switchButton);

            state.SprintHeld |= Held(sprintButton);
            state.CrouchHeld |= Held(crouchButton);
            state.FireHeld |= Held(fireButton);
            state.AimHeld |= Held(aimButton);
        }

        private static bool Held(TouchButton button) => button != null && button.Held;
        private static bool ConsumePressed(TouchButton button) => button != null && button.PressedThisFrame;

        private static bool IsOverUi(EnhancedTouch touch)
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject(touch.touchId);
        }
    }
}
