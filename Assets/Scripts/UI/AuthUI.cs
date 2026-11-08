using NullFrontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NullFrontier.UI
{
    /// <summary>
    /// Sign in / sign up panel. On success the backend token enables cloud saves
    /// and leaderboards; offline play is unaffected when closed.
    /// </summary>
    public class AuthUI : MonoBehaviour
    {
        [SerializeField] private InputField emailInput;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private InputField usernameInput;
        [SerializeField] private Button signInButton;
        [SerializeField] private Button signUpButton;
        [SerializeField] private Text statusLabel;

        private void Start()
        {
            var client = BackendClient.Instance;
            if (client != null) client.OnAuthResult += HandleAuthResult;

            if (signInButton != null) signInButton.onClick.AddListener(() => Submit(false));
            if (signUpButton != null) signUpButton.onClick.AddListener(() => Submit(true));
        }

        private void OnDestroy()
        {
            var client = BackendClient.Instance;
            if (client != null) client.OnAuthResult -= HandleAuthResult;
        }

        private void Submit(bool isSignUp)
        {
            var client = BackendClient.Instance;
            if (client == null || emailInput == null || passwordInput == null) return;

            string email = emailInput.text.Trim();
            string password = passwordInput.text;
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                SetStatus("Enter email and password.");
                return;
            }

            if (!client.IsConfigured)
            {
                SetStatus("Online services are not configured in this build.");
                return;
            }

            SetStatus("Connecting...");
            if (isSignUp) client.SignUp(email, password, usernameInput != null ? usernameInput.text.Trim() : null);
            else client.SignIn(email, password);
        }

        private void HandleAuthResult(bool ok, string error)
        {
            SetStatus(ok ? "Connected. Cloud saves enabled." : $"Failed: {error}");
            // Popping returns to the screen that opened this one; hiding the page would
            // leave ScreenManager with nothing visible.
            if (ok) ScreenManager.Instance?.Pop();
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }
    }
}
