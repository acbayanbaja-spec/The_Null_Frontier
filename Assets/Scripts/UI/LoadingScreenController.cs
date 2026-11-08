using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.UI
{
    /// <summary>
    /// Loading overlay bound to SceneLoader progress.
    /// </summary>
    public class LoadingScreenController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private UnityEngine.UI.Slider progressBar;

        private void Update()
        {
            var loader = SceneLoader.Instance;
            bool loading = loader != null && loader.IsLoading;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = loading ? 1f : 0f;
                canvasGroup.blocksRaycasts = loading;
                canvasGroup.interactable = false;
            }
            if (progressBar != null && loader != null)
            {
                progressBar.value = loader.Progress;
            }
        }
    }
}
