using UnityEngine;

namespace NullFrontier.UI
{
    /// <summary>
    /// A button target with no arguments, so scene-generated buttons can use
    /// persistent listeners. A lambda cannot be persisted into a scene file, which is
    /// why navigation is expressed as a component with a field instead of a closure.
    /// </summary>
    public class ScreenLink : MonoBehaviour
    {
        [SerializeField] private string screenName;

        public void Show() => ScreenManager.Instance?.Show(screenName);
    }
}
