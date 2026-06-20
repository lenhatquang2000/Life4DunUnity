using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AILife.Auth
{
    /// <summary>
    /// Quản lý xác thực người dùng - Đăng nhập/Đăng ký
    /// </summary>
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }
        
        [Header("API Configuration")]
        [SerializeField] private string apiBaseUrl = "https://life4dunbackend.onrender.com/api";
        
        [Header("Session")]
        [SerializeField] private string currentAccessToken;
        [SerializeField] private string currentRefreshToken;
        [SerializeField] private UserData currentUser;
        [SerializeField] private System.Collections.Generic.List<string> userPermissions = new System.Collections.Generic.List<string>();
        
        // Events
        public event Action OnLoginSuccess;
        public event Action OnPermissionsLoaded;
        
        // Properties
        public System.Collections.Generic.List<string> UserPermissions => userPermissions;
        public event Action<string> OnLoginFailed;
        public event Action OnRegisterSuccess;
        public event Action<string> OnRegisterFailed;
        public event Action OnLogout;
        
        // Properties
        public bool IsLoggedIn => !string.IsNullOrEmpty(currentAccessToken);
        public UserData CurrentUser => currentUser;
        public string AccessToken => currentAccessToken;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Load saved tokens
            LoadSession();
        }
        
        #region Public Methods
        
        /// <summary>
        /// Đăng nhập với email và password
        /// </summary>
        public void Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                OnLoginFailed?.Invoke("Vui lòng nhập email và mật khẩu");
                return;
            }
            
            StartCoroutine(LoginCoroutine(email, password));
        }
        
        /// <summary>
        /// Đăng ký tài khoản mới
        /// </summary>
        public void Register(string username, string email, string password, string confirmPassword)
        {
            // Validation
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || 
                string.IsNullOrEmpty(password))
            {
                OnRegisterFailed?.Invoke("Vui lòng điền đầy đủ thông tin");
                return;
            }
            
            if (password != confirmPassword)
            {
                OnRegisterFailed?.Invoke("Mật khẩu xác nhận không khớp");
                return;
            }
            
            if (password.Length < 6)
            {
                OnRegisterFailed?.Invoke("Mật khẩu phải có ít nhất 6 ký tự");
                return;
            }
            
            StartCoroutine(RegisterCoroutine(username, email, password));
        }
        
        /// <summary>
        /// Đăng xuất
        /// </summary>
        public void Logout()
        {
            currentAccessToken = null;
            currentRefreshToken = null;
            currentUser = null;
            
            ClearSession();
            OnLogout?.Invoke();
        }
        
        /// <summary>
        /// Refresh token khi access token hết hạn
        /// </summary>
        public void RefreshAccessToken()
        {
            if (string.IsNullOrEmpty(currentRefreshToken))
            {
                Logout();
                return;
            }
            
            StartCoroutine(RefreshTokenCoroutine());
        }
        
        #endregion
        
        #region Private Coroutines
        
        private IEnumerator LoginCoroutine(string usernameOrEmail, string password)
        {
            // API: POST /api/Auth/login
            // Hỗ trợ cả Email hoặc Username của Player
            LoginRequest requestData = new LoginRequest
            {
                usernameOrEmail = usernameOrEmail,
                password = password
            };
            
            string json = JsonUtility.ToJson(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            
            // API endpoint với chữ hoa A trong Auth
            using (UnityWebRequest request = new UnityWebRequest(
                $"{apiBaseUrl}/Auth/login", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseJson = request.downloadHandler.text;
                    LoginResponse response = JsonUtility.FromJson<LoginResponse>(responseJson);
                    
                    // API trả về token (không phải accessToken)
                    currentAccessToken = response.token;
                    // Không có refreshToken trong response này
                    currentUser = new UserData
                    {
                        id = response.userId,
                        email = response.email,
                        username = response.fullName,
                        role = response.role
                    };
                    
                    yield return FetchRolePermissionsCoroutine(response.role);
                    
                    SaveSession();
                    OnLoginSuccess?.Invoke();
                }
                else
                {
                    string errorMessage = "Đăng nhập thất bại";
                    
                    // Parse error từ response
                    try
                    {
                        ErrorResponse error = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                        if (!string.IsNullOrEmpty(error.message))
                            errorMessage = error.message;
                        else if (!string.IsNullOrEmpty(error.detail))
                            errorMessage = error.detail;
                    }
                    catch { }
                    
                    OnLoginFailed?.Invoke(errorMessage);
                }
            }
        }
        
        private IEnumerator RegisterCoroutine(string username, string email, string password)
        {
            // Theo Swagger API: POST /api/Auth/register
            // Yêu cầu: email, password, fullName
            RegisterRequest requestData = new RegisterRequest
            {
                fullName = username,
                email = email,
                password = password
            };
            
            string json = JsonUtility.ToJson(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            
            // API endpoint với chữ hoa A trong Auth
            using (UnityWebRequest request = new UnityWebRequest(
                $"{apiBaseUrl}/Auth/register", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    OnRegisterSuccess?.Invoke();
                }
                else
                {
                    string errorMessage = "Đăng ký thất bại";
                    
                    try
                    {
                        ErrorResponse error = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                        if (!string.IsNullOrEmpty(error.message))
                            errorMessage = error.message;
                    }
                    catch { }
                    
                    OnRegisterFailed?.Invoke(errorMessage);
                }
            }
        }
        
        private IEnumerator RefreshTokenCoroutine()
        {
            RefreshTokenRequest requestData = new RefreshTokenRequest
            {
                refreshToken = currentRefreshToken
            };
            
            string json = JsonUtility.ToJson(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            
            using (UnityWebRequest request = new UnityWebRequest(
                $"{apiBaseUrl}/auth/refresh", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseJson = request.downloadHandler.text;
                    LoginResponse response = JsonUtility.FromJson<LoginResponse>(responseJson);
                    
                    currentAccessToken = response.accessToken;
                    currentRefreshToken = response.refreshToken;
                    
                    SaveSession();
                }
                else
                {
                    // Refresh failed, logout
                    Logout();
                }
            }
        }

        public void FetchRolePermissions(string roleName)
        {
            if (string.IsNullOrEmpty(roleName))
            {
                userPermissions.Clear();
                OnPermissionsLoaded?.Invoke();
                return;
            }
            StartCoroutine(FetchRolePermissionsCoroutine(roleName));
        }

        private IEnumerator FetchRolePermissionsCoroutine(string roleName)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{apiBaseUrl}/RolePermissions/{roleName}"))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseJson = request.downloadHandler.text;
                    // Parse array of permissions
                    string wrappedJson = $"{{\"permissions\":{responseJson}}}";
                    PermissionListWrapper wrapper = JsonUtility.FromJson<PermissionListWrapper>(wrappedJson);
                    
                    userPermissions.Clear();
                    if (wrapper.permissions != null)
                    {
                        userPermissions.AddRange(wrapper.permissions);
                    }
                    Debug.Log($"[AuthManager] Loaded permissions for role '{roleName}': {string.Join(", ", userPermissions)}");
                }
                else
                {
                    Debug.LogError($"[AuthManager] Failed to load permissions for role '{roleName}': {request.error}");
                    userPermissions.Clear();
                }
                OnPermissionsLoaded?.Invoke();
            }
        }

        [System.Serializable]
        public class PermissionListWrapper
        {
            public string[] permissions;
        }
        
        #endregion
        
        #region Session Management
        
        private void SaveSession()
        {
            PlayerPrefs.SetString("AccessToken", currentAccessToken ?? "");
            PlayerPrefs.SetString("RefreshToken", currentRefreshToken ?? "");
            
            if (currentUser != null)
            {
                PlayerPrefs.SetString("UserId", currentUser.id);
                PlayerPrefs.SetString("Username", currentUser.username);
                PlayerPrefs.SetString("UserRole", currentUser.role ?? "");
                PlayerPrefs.SetString("UserPermissions", string.Join(",", userPermissions));
            }
            
            PlayerPrefs.Save();
        }
        
        private void LoadSession()
        {
            currentAccessToken = PlayerPrefs.GetString("AccessToken", "");
            currentRefreshToken = PlayerPrefs.GetString("RefreshToken", "");
            
            string userId = PlayerPrefs.GetString("UserId", "");
            string username = PlayerPrefs.GetString("Username", "");
            string role = PlayerPrefs.GetString("UserRole", "");
            string permsStr = PlayerPrefs.GetString("UserPermissions", "");
            
            userPermissions = string.IsNullOrEmpty(permsStr) ? new System.Collections.Generic.List<string>() : new System.Collections.Generic.List<string>(permsStr.Split(','));
            
            if (!string.IsNullOrEmpty(userId))
            {
                currentUser = new UserData
                {
                    id = userId,
                    username = username,
                    role = role
                };
            }
        }
        
        private void ClearSession()
        {
            PlayerPrefs.DeleteKey("AccessToken");
            PlayerPrefs.DeleteKey("RefreshToken");
            PlayerPrefs.DeleteKey("UserId");
            PlayerPrefs.DeleteKey("Username");
            PlayerPrefs.DeleteKey("UserRole");
            PlayerPrefs.DeleteKey("UserPermissions");
            PlayerPrefs.Save();
        }
        
        #endregion
    }
    
    #region Data Models
    
    [System.Serializable]
    public class UserData
    {
        public string id;
        public string username;
        public string email;
        public string role;
    }
    
    [System.Serializable]
    public class LoginRequest
    {
        public string usernameOrEmail;  // Có thể là Email hoặc Username của Player
        public string password;
    }
    
    [System.Serializable]
    public class LoginResponse
    {
        public string userId;
        public string email;
        public string fullName;
        public string token;
        public string expiresAt;
        public string accessToken;
        public string refreshToken;
        public string role;
    }
    
    [System.Serializable]
    public class RegisterRequest
    {
        public string email;
        public string password;
        public string fullName;
    }
    
    [System.Serializable]
    public class RegisterResponse
    {
        public string id;
        public string email;
        public string fullName;
        public string createdAt;
        public string lastLoginAt;
        public bool isActive;
        public PlayerData[] players;
    }
    
    [System.Serializable]
    public class PlayerData
    {
        public string id;
        public string userId;
        public string username;
        public int experience;
        public int gold;
        public int gems;
        public string createdAt;
        public string lastLoginAt;
        public string avatarUrl;
        public string model;
        public PlayerAttributes attributes;
    }
    
    [System.Serializable]
    public class PlayerAttributes
    {
        public int level;
        public int attack;
        public int defense;
        public int speed;
        public int health;
        public int maxHealth;
    }
    
    [System.Serializable]
    public class RefreshTokenRequest
    {
        public string refreshToken;
    }
    
    [System.Serializable]
    public class ErrorResponse
    {
        public string message;
        public string code;
        public string detail;
    }
    
    #endregion
}
