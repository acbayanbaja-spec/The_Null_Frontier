using NullFrontier.Core;
using NullFrontier.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace NullFrontier.UI
{
    /// <summary>
    /// Main menu flow. Every button performs a real action; features that need a
    /// backend connection are marked unavailable when BackendClient is not authenticated.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Screens (managed by ScreenManager)")]
        [SerializeField] private string titleScreen = "Title";
        [SerializeField] private string characterScreen = "CharacterSelect";
        [SerializeField] private string settingsScreen = "Settings";
        [SerializeField] private string multiplayerScreen = "Multiplayer";
        [SerializeField] private string accountScreen = "Account";
        [SerializeField] private string badgesScreen = "Badges";
        [SerializeField] private string rewardsScreen = "Rewards";
        [SerializeField] private string helpScreen = "Help";

        [Header("Buttons")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button multiplayerButton;
        [SerializeField] private Image rewardsDot;

        [Header("Scenes")]
        [SerializeField] private string gameSceneName = "VerticalCity";

        private void Start()
        {
            // ScreenManager disables every screen in Awake; the title must be raised.
            ScreenManager.Instance?.Show(titleScreen);

            var save = SaveSystem.Instance;
            if (save != null) save.Load();

            if (continueButton != null)
            {
                bool hasSave = save != null && save.HasLocalSave;
                continueButton.interactable = hasSave;
                continueButton.onClick.AddListener(ContinueGame);
            }
            if (multiplayerButton != null)
            {
                bool online = BackendClient.Instance != null && BackendClient.Instance.IsAuthenticated;
                multiplayerButton.interactable = online;
                multiplayerButton.onClick.AddListener(() => ScreenManager.Instance?.Show(multiplayerScreen));
            }

            // The dot means "a drop is still sitting in the outbox", so it reads exactly
            // the counter the rewards page lists - never a decoration.
            if (rewardsDot != null)
            {
                var badges = AchievementSystem.Instance;
                rewardsDot.gameObject.SetActive(badges != null && badges.ClaimableCount > 0);
            }
        }

        public void StartNewGame()
        {
            var save = SaveSystem.Instance;
            if (save != null)
            {
                // A fresh run must not inherit an old save's inventory/levels.
                save.StartNewSave();
            }

            if (ScreenManager.Instance != null && ScreenManager.Instance.HasScreen(characterScreen))
                ScreenManager.Instance.Show(characterScreen);
            else
                SceneLoader.Instance?.LoadSceneAsync(gameSceneName, additive: false);
        }

        public void ContinueGame()
        {
            var save = SaveSystem.Instance;
            if (save == null || !save.HasLocalSave) return;

            var game = GameManager.Instance;
            if (game != null)
            {
                game.SelectedCharacterId = save.Data.selectedCharacterId;
                game.CurrentRegionId = save.Data.currentRegionId;
            }
            SceneLoader.Instance?.LoadSceneAsync(gameSceneName, additive: false);
        }

        public void OpenSettings() => ScreenManager.Instance?.Show(settingsScreen);
        public void OpenCharacterSelect() => ScreenManager.Instance?.Show(characterScreen);
        public void OpenAccount() => ScreenManager.Instance?.Show(accountScreen);

        /// <summary>Badge wall, supply outbox and the control sheet. All three read the
        /// same save the game writes, so an empty page means nothing has happened yet.</summary>
        public void OpenBadges() => ScreenManager.Instance?.Show(badgesScreen);
        public void OpenRewards() => ScreenManager.Instance?.Show(rewardsScreen);
        public void OpenHelp() => ScreenManager.Instance?.Show(helpScreen);
        public void QuitGame() => Application.Quit();
    }
}
