using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AILife.Auth;

namespace AILife.UI
{
    /// <summary>
    /// Quản lý giao diện Login và Register
    /// </summary>
    public class LoginRegisterUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private GameObject registerPanel;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private GameObject mainMenuPanel;
        
        [Header("Login Inputs")]
        [SerializeField] private TMP_InputField loginEmailInput;
        [SerializeField] private TMP_InputField loginPasswordInput;
        [SerializeField] private Toggle rememberMeToggle;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button showRegisterButton;
        [SerializeField] private TextMeshProUGUI loginErrorText;
        
        [Header("Register Inputs")]
        [SerializeField] private TMP_InputField registerUsernameInput;
        [SerializeField] private TMP_InputField registerEmailInput;
        [SerializeField] private TMP_InputField registerPasswordInput;
        [SerializeField] private TMP_InputField registerConfirmPasswordInput;
        [SerializeField] private Button registerButton;
        [SerializeField] private Button showLoginButton;
        [SerializeField] private TextMeshProUGUI registerErrorText;
        [SerializeField] private TextMeshProUGUI registerSuccessText;
        
        [Header("Loading")]
        [SerializeField] private TextMeshProUGUI loadingText;
        [SerializeField] private Slider loadingSlider;
        
        private void Start()
        {
            // Subscribe to AuthManager events
            AuthManager.Instance.OnLoginSuccess += OnLoginSuccess;
            AuthManager.Instance.OnLoginFailed += OnLoginFailed;
            AuthManager.Instance.OnRegisterSuccess += OnRegisterSuccess;
            AuthManager.Instance.OnRegisterFailed += OnRegisterFailed;
            
            // Setup button listeners
            loginButton.onClick.AddListener(OnLoginButtonClicked);
            registerButton.onClick.AddListener(OnRegisterButtonClicked);
            showRegisterButton.onClick.AddListener(ShowRegisterPanel);
            showLoginButton.onClick.AddListener(ShowLoginPanel);
            
            // Clear error messages
            ClearErrors();
            
            // Check if already logged in
            if (AuthManager.Instance.IsLoggedIn)
            {
                ShowMainMenu();
            }
            else
            {
                ShowLoginPanel();
            }
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginSuccess -= OnLoginSuccess;
                AuthManager.Instance.OnLoginFailed -= OnLoginFailed;
                AuthManager.Instance.OnRegisterSuccess -= OnRegisterSuccess;
                AuthManager.Instance.OnRegisterFailed -= OnRegisterFailed;
            }
        }
        
        #region UI Navigation
        
        private void ShowLoginPanel()
        {
            loginPanel.SetActive(true);
            registerPanel.SetActive(false);
            loadingPanel.SetActive(false);
            mainMenuPanel.SetActive(false);
            ClearErrors();
        }
        
        private void ShowRegisterPanel()
        {
            loginPanel.SetActive(false);
            registerPanel.SetActive(true);
            loadingPanel.SetActive(false);
            mainMenuPanel.SetActive(false);
            ClearErrors();
        }
        
        private void ShowLoading(string message = "Đang tải...")
        {
            loginPanel.SetActive(false);
            registerPanel.SetActive(false);
            loadingPanel.SetActive(true);
            mainMenuPanel.SetActive(false);
            loadingText.text = message;
        }
        
        private void ShowMainMenu()
        {
            loginPanel.SetActive(false);
            registerPanel.SetActive(false);
            loadingPanel.SetActive(false);
            mainMenuPanel.SetActive(true);
            
            // Cập nhật UI main menu với thông tin user
            UpdateMainMenuUI();
        }
        
        private void UpdateMainMenuUI()
        {
            // TODO: Cập nhật tên người chơi, level, v.v.
            // Có thể tìm các Text components và cập nhật
        }
        
        #endregion
        
        #region Button Handlers
        
        private void OnLoginButtonClicked()
        {
            string email = loginEmailInput.text.Trim();
            string password = loginPasswordInput.text;
            
            // Validation
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ShowLoginError("Vui lòng nhập email và mật khẩu");
                return;
            }
            
            if (!IsValidEmail(email))
            {
                ShowLoginError("Email không hợp lệ");
                return;
            }
            
            // Show loading
            ShowLoading("Đang đăng nhập...");
            
            // Call AuthManager
            AuthManager.Instance.Login(email, password);
        }
        
        private void OnRegisterButtonClicked()
        {
            string username = registerUsernameInput.text.Trim();
            string email = registerEmailInput.text.Trim();
            string password = registerPasswordInput.text;
            string confirmPassword = registerConfirmPasswordInput.text;
            
            // Validation
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || 
                string.IsNullOrEmpty(password))
            {
                ShowRegisterError("Vui lòng điền đầy đủ thông tin");
                return;
            }
            
            if (username.Length < 3)
            {
                ShowRegisterError("Tên người dùng phải có ít nhất 3 ký tự");
                return;
            }
            
            if (!IsValidEmail(email))
            {
                ShowRegisterError("Email không hợp lệ");
                return;
            }
            
            if (password.Length < 6)
            {
                ShowRegisterError("Mật khẩu phải có ít nhất 6 ký tự");
                return;
            }
            
            if (password != confirmPassword)
            {
                ShowRegisterError("Mật khẩu xác nhận không khớp");
                return;
            }
            
            // Show loading
            ShowLoading("Đang đăng ký...");
            
            // Call AuthManager
            AuthManager.Instance.Register(username, email, password, confirmPassword);
        }
        
        #endregion
        
        #region AuthManager Callbacks
        
        private void OnLoginSuccess()
        {
            Debug.Log("[LoginRegisterUI] Login successful!");
            ShowMainMenu();
        }
        
        private void OnLoginFailed(string errorMessage)
        {
            Debug.LogError($"[LoginRegisterUI] Login failed: {errorMessage}");
            ShowLoginPanel();
            ShowLoginError(errorMessage);
        }
        
        private void OnRegisterSuccess()
        {
            Debug.Log("[LoginRegisterUI] Register successful!");
            
            // Show success message
            ClearErrors();
            registerSuccessText.text = "Đăng ký thành công! Vui lòng đăng nhập.";
            registerSuccessText.gameObject.SetActive(true);
            
            // Switch to login after delay
            Invoke(nameof(ShowLoginPanel), 2f);
        }
        
        private void OnRegisterFailed(string errorMessage)
        {
            Debug.LogError($"[LoginRegisterUI] Register failed: {errorMessage}");
            ShowRegisterPanel();
            ShowRegisterError(errorMessage);
        }
        
        #endregion
        
        #region Helper Methods
        
        private void ShowLoginError(string message)
        {
            loginErrorText.text = message;
            loginErrorText.gameObject.SetActive(true);
        }
        
        private void ShowRegisterError(string message)
        {
            registerErrorText.text = message;
            registerErrorText.gameObject.SetActive(true);
            registerSuccessText.gameObject.SetActive(false);
        }
        
        private void ClearErrors()
        {
            loginErrorText.gameObject.SetActive(false);
            registerErrorText.gameObject.SetActive(false);
            registerSuccessText.gameObject.SetActive(false);
        }
        
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        
        #endregion
    }
}
