using UnityEngine;

namespace NullFrontier.Core
{
    /// <summary>
    /// Persistent game state singleton. Survives scene loads via DontDestroyOnLoad.
    /// Owns the current game mode and selected character id.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameMode CurrentMode { get; private set; } = GameMode.None;
        public string SelectedCharacterId { get; set; } = "iidreuspade";
        public string CurrentRegionId { get; set; } = "vertical_city";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
        }

        public void SetGameMode(GameMode mode)
        {
            CurrentMode = mode;
            GameEvents.RaiseGameModeChanged(mode);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) GameEvents.RaiseSaveRequested();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) GameEvents.RaiseSaveRequested();
        }
    }
}
