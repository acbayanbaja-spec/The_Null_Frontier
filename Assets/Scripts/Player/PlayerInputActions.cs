using UnityEngine;
using UnityEngine.InputSystem;

namespace NullFrontier.Player
{
    /// <summary>
    /// Input actions built in code (instead of a .inputactions asset) so the project
    /// works with zero imported assets. Keyboard/mouse + gamepad are bound; touch
    /// controls feed PlayerInputState directly through MobileInputProvider.
    /// </summary>
    public class PlayerInputActions
    {
        public InputActionMap Map { get; }

        public InputAction Move { get; }
        public InputAction Look { get; }
        public InputAction Jump { get; }
        public InputAction Sprint { get; }
        public InputAction Crouch { get; }
        public InputAction Dodge { get; }
        public InputAction Fire { get; }
        public InputAction Aim { get; }
        public InputAction Reload { get; }
        public InputAction Interact { get; }
        public InputAction Ability { get; }
        public InputAction WeaponSwitch { get; }
        public InputAction Pause { get; }

        public PlayerInputActions()
        {
            Map = new InputActionMap("Player");

            Move = Map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");

            Look = Map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            Look.AddBinding("<Pointer>/delta");
            Look.AddBinding("<Gamepad>/rightStick");

            Jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Sprint = Button("Sprint", "<Keyboard>/shift", "<Gamepad>/leftStickPress");
            Crouch = Button("Crouch", "<Keyboard>/ctrl", "<Gamepad>/buttonEast");
            Dodge = Button("Dodge", "<Keyboard>/alt", "<Gamepad>/rightStickPress");
            Fire = Button("Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            Aim = Button("Aim", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            Reload = Button("Reload", "<Keyboard>/r", "<Gamepad>/buttonWest");
            Interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonNorth");
            Ability = Button("Ability", "<Keyboard>/q", "<Gamepad>/rightShoulder");
            WeaponSwitch = Button("WeaponSwitch", "<Keyboard>/tab", "<Gamepad>/leftShoulder");
            Pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
        }

        private InputAction Button(string name, string keyboardPath, string gamepadPath)
        {
            var action = Map.AddAction(name, InputActionType.Button);
            action.AddBinding(keyboardPath);
            action.AddBinding(gamepadPath);
            return action;
        }

        public void Enable() => Map.Enable();
        public void Disable() => Map.Disable();
    }
}
