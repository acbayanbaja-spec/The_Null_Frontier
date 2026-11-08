using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NullFrontier.Core
{
    /// <summary>
    /// REST client for the NULL FRONTIER backend (Node/Express on Render).
    /// Auth token is held in memory only. All endpoints fail silently to offline
    /// behavior — the game remains playable without a connection.
    /// </summary>
    public class BackendClient : MonoBehaviour
    {
        public static BackendClient Instance { get; private set; }

        // No endpoint is baked in. A phone must never point at localhost, and shipping a
        // placeholder host would be worse than shipping no online services at all.
        [SerializeField] private string apiBaseUrl = "";
        [SerializeField] private float requestTimeoutSeconds = 15f;

        public string AuthToken { get; private set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(AuthToken) && IsConfigured;

        /// <summary>
        /// True when a usable endpoint is set. Devices require HTTPS; the editor may also
        /// use a loopback HTTP server for local development.
        /// </summary>
        public bool IsConfigured
        {
            get
            {
                if (string.IsNullOrWhiteSpace(apiBaseUrl) ||
                    !Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri)) return false;
                if (uri.Scheme == Uri.UriSchemeHttps) return true;
#if UNITY_EDITOR
                return uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
#else
                return false;
#endif
            }
        }

        public event Action<bool, string> OnAuthResult;

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

        public void SetApiBaseUrl(string url) => apiBaseUrl = (url ?? string.Empty).TrimEnd('/');

        public void SetAuthToken(string token) => AuthToken = token;

        public void SignUp(string email, string password, string username)
        {
            if (!RequireEndpoint()) return;
            StartCoroutine(AuthRoutine("/auth/signup", email, password, username));
        }

        public void SignIn(string email, string password)
        {
            if (!RequireEndpoint()) return;
            StartCoroutine(AuthRoutine("/auth/signin", email, password, null));
        }

        private bool RequireEndpoint()
        {
            if (IsConfigured) return true;
            OnAuthResult?.Invoke(false, "Online services are not configured in this build.");
            return false;
        }

        private IEnumerator AuthRoutine(string endpoint, string email, string password, string username)
        {
            if (!IsConfigured) yield break;
            var payload = new SignupRequest { email = email, password = password, username = username };
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

            using var request = new UnityWebRequest(apiBaseUrl + endpoint, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = (int)requestTimeoutSeconds;

            yield return request.SendWebRequest();

            bool ok = request.result == UnityWebRequest.Result.Success && request.responseCode == 200;
            string token = null;
            if (ok)
            {
                var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                token = response?.token;
                AuthToken = token;
            }
            OnAuthResult?.Invoke(ok, ok ? null : request.error);
        }

        public void UploadCloudSave(SaveSystem.SaveData data)
        {
            if (!IsAuthenticated) return;
            string json = JsonUtility.ToJson(data);
            StartCoroutine(AuthorizedRoutine("/saves/cloud", UnityWebRequest.kHttpVerbPUT, json, null));
        }

        public void FetchCloudSave(Action<bool, string> onComplete)
        {
            if (!IsAuthenticated)
            {
                onComplete?.Invoke(false, "Not authenticated");
                return;
            }
            StartCoroutine(AuthorizedRoutine("/saves/cloud", UnityWebRequest.kHttpVerbGET, null, onComplete));
        }

        public void SubmitLeaderboardScore(string leaderboardId, long score, Action<bool> onComplete)
        {
            if (!IsAuthenticated) return;
            var payload = JsonUtility.ToJson(new ScoreRequest { leaderboardId = leaderboardId, score = score });
            StartCoroutine(AuthorizedRoutine("/leaderboards/score", UnityWebRequest.kHttpVerbPOST, payload,
                (ok, _) => onComplete?.Invoke(ok)));
        }

        private IEnumerator AuthorizedRoutine(string endpoint, string verb, string json, Action<bool, string> onComplete)
        {
            if (!IsConfigured)
            {
                onComplete?.Invoke(false, "Online services are not configured in this build.");
                yield break;
            }
            using var request = new UnityWebRequest(apiBaseUrl + endpoint, verb);
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + AuthToken);
            request.timeout = (int)requestTimeoutSeconds;

            yield return request.SendWebRequest();

            bool ok = request.result == UnityWebRequest.Result.Success &&
                      (request.responseCode == 200 || request.responseCode == 201);
            onComplete?.Invoke(ok, ok ? request.downloadHandler.text : request.error);
        }

        [Serializable] private class SignupRequest { public string email; public string password; public string username; }
        [Serializable] private class AuthResponse { public string token; }
        [Serializable] private class ScoreRequest { public string leaderboardId; public long score; }
    }
}
