using NullFrontier.Combat;
using NullFrontier.Player;
using UnityEngine;
using UnityEngine.UI;

namespace NullFrontier.UI
{
    /// <summary>
    /// Drives the state ring on a touch key: a dark veil that unwinds while an ability
    /// cools, a sprint key that stays veiled while the stamina bar is spent, and a fire
    /// key that counts the magazine out.
    ///
    /// Every number here is read straight from the component that owns it. Nothing is
    /// simulated - if the source is missing the key simply stays unveiled.
    /// </summary>
    public class KeyOverlay : MonoBehaviour
    {
        public enum Source { Ability, Weapon, Stamina }

        [SerializeField] private Source source;
        [SerializeField] private Image veil;
        [SerializeField] private Text readout;
        [SerializeField] private Color veilColor = new Color(0.01f, 0.02f, 0.045f, 0.72f);

        private AbilityController _abilities;
        private PlayerCombat _combat;
        private Stamina _stamina;
        private float _shown = -1f;
        private string _shownText;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            _abilities = player.GetComponentInChildren<AbilityController>(true);
            _combat = player.GetComponentInChildren<PlayerCombat>(true);
            _stamina = player.GetComponent<Stamina>();
        }

        private void Update()
        {
            float veilAmount = 0f;
            string text = null;

            switch (source)
            {
                case Source.Ability:
                    if (_abilities != null) veilAmount = Mathf.Clamp01(_abilities.CooldownNormalized);
                    break;

                case Source.Stamina:
                    if (_stamina != null)
                    {
                        float max = Mathf.Max(1f, _stamina.Max);
                        veilAmount = Mathf.Clamp01(1f - _stamina.Current / max);
                    }
                    break;

                case Source.Weapon:
                    var weapon = _combat != null ? _combat.CurrentWeapon : null;
                    var definition = weapon != null ? weapon.Definition : null;
                    if (weapon != null && definition != null)
                    {
                        text = weapon.IsReloading ? "RELOADING"
                            : $"{weapon.CurrentAmmo} / {definition.magazineSize}";
                        veilAmount = weapon.IsReloading ? 1f
                            : weapon.CurrentAmmo <= 0 ? 1f : 0f;
                    }
                    break;
            }

            if (veil != null && Mathf.Abs(veilAmount - _shown) > 0.01f)
            {
                _shown = veilAmount;
                veil.color = veilColor;
                veil.fillAmount = veilAmount;
                veil.enabled = veilAmount > 0.01f;
            }

            if (readout != null && text != _shownText)
            {
                _shownText = text;
                readout.text = text ?? string.Empty;
                readout.enabled = !string.IsNullOrEmpty(text);
            }
        }
    }
}
