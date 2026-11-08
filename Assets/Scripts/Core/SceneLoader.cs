using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NullFrontier.Core
{
    /// <summary>
    /// Async additive scene loading with progress callbacks. Region scenes are
    /// loaded additively around a persistent bootstrap scene so the open world
    /// streams instead of loading everything at once.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        public float Progress { get; private set; }
        public bool IsLoading { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadSceneAsync(string sceneName, bool additive, Action onComplete = null)
        {
            if (IsLoading) return;
            StartCoroutine(LoadRoutine(sceneName, additive, onComplete));
        }

        private IEnumerator LoadRoutine(string sceneName, bool additive, Action onComplete)
        {
            IsLoading = true;
            Progress = 0f;

            var mode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            var operation = SceneManager.LoadSceneAsync(sceneName, mode);
            operation.allowSceneActivation = true;

            while (!operation.isDone)
            {
                Progress = operation.progress;
                yield return null;
            }

            IsLoading = false;
            Progress = 1f;
            onComplete?.Invoke();
        }

        public void UnloadSceneAsync(string sceneName, Action onComplete = null)
        {
            StartCoroutine(UnloadRoutine(sceneName, onComplete));
        }

        private IEnumerator UnloadRoutine(string sceneName, Action onComplete)
        {
            var operation = SceneManager.UnloadSceneAsync(sceneName);
            if (operation != null)
            {
                while (!operation.isDone) yield return null;
            }
            onComplete?.Invoke();
        }
    }
}
