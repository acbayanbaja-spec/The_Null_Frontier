using UnityEngine;

namespace NullFrontier.UI
{
    /// <summary>
    /// Renders the menu street's reflection probe exactly once, a moment after the scene
    /// opens. A realtime probe refresh every frame would cost six cube faces per frame on
    /// a phone for a reflection that only has to sit in wet asphalt; nothing on the stage
    /// moves far enough to need a second capture.
    /// </summary>
    [RequireComponent(typeof(ReflectionProbe))]
    public class MenuReflectionProbe : MonoBehaviour
    {
        [SerializeField] private float settleSeconds = 0.5f;

        private ReflectionProbe _probe;
        private float _remaining;

        private void Awake()
        {
            _probe = GetComponent<ReflectionProbe>();
            _remaining = Mathf.Max(0f, settleSeconds);
        }

        private void Update()
        {
            if (_probe == null)
            {
                enabled = false;
                return;
            }

            // Held back a few frames so the stage is fully built and the first frames'
            // shader compilation is out of the way - a probe captured mid-warmup bakes
            // whichever materials had not finished compiling into the road.
            _remaining -= Time.unscaledDeltaTime;
            if (_remaining > 0f) return;

            _probe.RenderProbe();
            enabled = false;
        }
    }
}
