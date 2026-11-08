using NullFrontier.Characters;
using NullFrontier.Core;
using NullFrontier.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace NullFrontier.UI
{
    /// <summary>
    /// The account chip that sits in the corner of the menu screens: the operative the
    /// player actually picked, their real rank, and the real XP progress toward the next
    /// one. Refreshes whenever it is enabled, because that is when a selection or a save
    /// load can have changed any of it.
    /// </summary>
    public class PlayerCardController : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text levelLabel;
        [SerializeField] private Slider xpBar;
        [SerializeField] private string portraitFolder = "Portraits";

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            var definition = CharacterRegistry.Get(GameManager.Instance?.SelectedCharacterId);

            if (nameLabel != null) nameLabel.text = definition.DisplayName.ToUpperInvariant();

            if (portrait != null)
            {
                var sprite = string.IsNullOrEmpty(definition.ModelPrefab)
                    ? null
                    : Resources.Load<Sprite>($"{portraitFolder}/{definition.ModelPrefab}");
                portrait.sprite = sprite;
                // No render yet means the portrait pass has not run; a tinted plate still
                // reads as a card instead of a blank hole in the layout.
                portrait.color = sprite != null
                    ? Color.white
                    : Color.Lerp(new Color(0.12f, 0.16f, 0.22f), definition.AccentColor, 0.35f);
            }

            var progression = ProgressionSystem.Instance;
            if (progression == null) return;

            if (levelLabel != null) levelLabel.text = $"Lv. {progression.Level}";
            if (xpBar == null) return;

            long needed = ProgressionSystem.XpNeededForLevel(progression.Level);
            xpBar.value = needed > 0 ? Mathf.Clamp01(progression.Xp / (float)needed) : 1f;
        }
    }
}
