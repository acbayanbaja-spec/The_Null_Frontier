using NullFrontier.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NullFrontier.UI
{
    /// <summary>
    /// Pause overlay. Time scale driven so gameplay freezes; quitting saves first.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private string pauseScreen = "Pause";
        [SerializeField] private string mainMenuScene = "MainMenu";

        private bool _paused;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) TogglePause();
        }

        public void TogglePause()
        {
            _paused = !_paused;
            Time.timeScale = _paused ? 0f : 1f;

            // PopAll, not Pop: opening Settings from Pause leaves two screens on the
            // stack, and popping would reveal Settings instead of gameplay.
            if (_paused) ScreenManager.Instance?.Show(pauseScreen);
            else ScreenManager.Instance?.PopAll();
        }

        public void Resume() => TogglePause();

        public void SaveAndQuitToMenu()
        {
            SaveSystem.Instance?.SaveNow();
            Time.timeScale = 1f;
            _paused = false;
            SceneLoader.Instance?.LoadSceneAsync(mainMenuScene, additive: false);
        }
    }
}
