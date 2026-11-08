using UnityEngine;
using UnityEngine.EventSystems;

namespace NullFrontier.UI
{
    /// <summary>
    /// Touch/click button that tracks Held/Pressed/Released edges for a frame.
    /// Bind in the inspector via UnityEvents or read from MobileInputProvider.
    /// </summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool Held { get; private set; }
        public bool PressedThisFrame { get; private set; }
        public bool ReleasedThisFrame { get; private set; }

        private void LateUpdate()
        {
            PressedThisFrame = false;
            ReleasedThisFrame = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Held) PressedThisFrame = true;
            Held = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Held = false;
            ReleasedThisFrame = true;
        }
    }
}
