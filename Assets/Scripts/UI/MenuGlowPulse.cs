using UnityEngine;
using UnityEngine.UI;

namespace NullFrontier.UI
{
    /// <summary>
    /// Breathing light for a menu plate. The title screen is drawn once and then sits
    /// still, and a still frame reads as a screenshot rather than a game - this is the
    /// cheapest way to make the primary action look like it is switched on.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class MenuGlowPulse : MonoBehaviour
    {
        [SerializeField] private float amplitude = 0.4f;
        [SerializeField] private float seconds = 3.2f;
        [SerializeField] private float scaleAmplitude = 0.045f;
        [SerializeField] private Color warmShift = new Color(1.05f, 1.02f, 1.12f);

        private Image _image;
        private Color _base;
        private Vector3 _baseScale;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _base = _image.color;
            _baseScale = transform.localScale;
        }

        private void LateUpdate()
        {
            float wave = Mathf.Sin(2f * Mathf.PI * (Time.unscaledTime / seconds));

            // Written from the cached colour, never from image.color: reading the live
            // value back would compound the pulse onto itself and blow the alpha out.
            Color c = _base;
            c.r = Mathf.Min(1f, c.r * (1f + 0.06f * wave * warmShift.r));
            c.g = Mathf.Min(1f, c.g * (1f + 0.06f * wave * warmShift.g));
            c.b = Mathf.Min(1f, c.b * (1f + 0.06f * wave * warmShift.b));
            c.a = Mathf.Clamp01(c.a * (1f + amplitude * wave));
            _image.color = c;

            float scale = 1f + scaleAmplitude * wave;
            transform.localScale = new Vector3(_baseScale.x * scale, _baseScale.y * scale,
                _baseScale.z);
        }
    }
}
