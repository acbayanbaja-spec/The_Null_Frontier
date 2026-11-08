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

        private int _pressFrame;
        private int _releaseFrame;

        private void LateUpdate()
        {
            // UGUI can deliver a tap after the gameplay Updates have already polled for
            // this frame. Clearing immediately would drop that tap, so an edge stays
            // readable until the end of the following frame.
            if (PressedThisFrame && Time.frameCount > _pressFrame) PressedThisFrame = false;
            if (ReleasedThisFrame && Time.frameCount > _releaseFrame) ReleasedThisFrame = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Held)
            {
                PressedThisFrame = true;
                _pressFrame = Time.frameCount;
            }
            Held = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Held = false;
            ReleasedThisFrame = true;
            _releaseFrame = Time.frameCount;
        }
    }
}
