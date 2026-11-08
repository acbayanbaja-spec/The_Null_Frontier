using UnityEngine;
using UnityEngine.EventSystems;

namespace NullFrontier.UI
{
    /// <summary>
    /// Floating joystick that lives on a full screen zone: the thumb ring appears under
    /// whatever the player touches inside it, exactly like the movement pad in a shipped
    /// mobile shooter, so there is no dead corner to hunt for at the start of a fight.
    ///
    /// All distances are measured in screen pixels and divided by the ring's own pixel
    /// radius. Mixing canvas units with pixel deltas (the old behaviour) made the throw
    /// depend on the device's canvas scale factor, which is why the pad felt stiff on a
    /// phone and fine in the editor.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform ring;
        [SerializeField] private RectTransform handle;
        [Tooltip("How far the knob travels inside the ring at full tilt, as a fraction of "
            + "the ring radius. Input stays full range either way.")]
        [SerializeField] private float handleTravel = 0.55f;
        [SerializeField] private float deadZone = 0.12f;
        [Tooltip("Off keeps the pad visible where the player learned to find it, the "
            + "MOBA convention. On lets it appear under the thumb anywhere in the zone.")]
        [SerializeField] private bool floating;

        public Vector2 Value { get; private set; }
        public bool IsHeld { get; private set; }

        private Canvas _canvas;
        private int _pointerId = -1;
        private Vector2 _originPixel;
        private float _radiusPixel = 120f;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (ring == null) ring = transform as RectTransform;
            // A floating pad hides until a thumb claims it; a fixed pad is the control
            // the player aims at, so it stays on screen.
            SetIdle(!floating);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // One finger owns the pad; a second tap inside the zone must not steal it.
            if (_pointerId >= 0 && _pointerId != eventData.pointerId) return;

            _pointerId = eventData.pointerId;
            IsHeld = true;
            // Measuring from the touch itself (not the pad centre) keeps a thumb that
            // lands on the rim from firing a full-tilt command before it moves.
            _originPixel = eventData.position;
            _radiusPixel = Mathf.Max(40f, RingPixelRadius());
            if (floating)
            {
                PlaceRing(_originPixel);
                SetIdle(true);
            }
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            Vector2 delta = eventData.position - _originPixel;
            Vector2 raw = delta / _radiusPixel;

            float length = raw.magnitude;
            if (length < deadZone)
            {
                Value = Vector2.zero;
            }
            else
            {
                // Rescale past the dead zone so a small flick still means "walk", and
                // clamp at full throw so diagonal input is never faster than straight.
                Value = Vector2.ClampMagnitude(
                    raw * ((length - deadZone) / (1f - deadZone)), 1f);
            }

            if (handle != null)
            {
                // The knob rides inside its own ring, so its offset is in the ring's
                // canvas units - not screen pixels.
                float travel = ring != null ? ring.rect.width * 0.5f * handleTravel : 90f;
                handle.anchoredPosition = Value * travel;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            _pointerId = -1;
            IsHeld = false;
            Value = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            SetIdle(!floating);
        }

        private float CanvasScale() => _canvas != null ? Mathf.Max(0.01f, _canvas.scaleFactor) : 1f;

        private float RingPixelRadius() => ring != null ? ring.rect.width * 0.5f * CanvasScale() : 120f;

        /// <summary>Move the ring so its centre sits under the finger, in canvas units.</summary>
        private void PlaceRing(Vector2 pixel)
        {
            if (ring == null) return;
            var rect = (transform as RectTransform) ?? ring;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, pixel,
                    _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                    out Vector3 world))
            {
                ring.position = world;
            }
        }

        private void SetIdle(bool active)
        {
            if (ring != null) ring.gameObject.SetActive(active);
            if (handle != null) handle.gameObject.SetActive(active);
        }
    }
}
