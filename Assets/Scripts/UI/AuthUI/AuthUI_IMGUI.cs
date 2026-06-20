using UnityEngine;
using AILife.Auth;
using System.Collections.Generic;

namespace AILife.UI
{
    public class AuthUI_IMGUI : MonoBehaviour
    {
        private string email = "";
        private string password = "";
        private string username = "";
        private string confirmPassword = "";

        private bool isRegisterMode = false;
        private string statusMessage = "";
        private bool isLoading = false;

        private string newPlayerName = "";
        private bool playersLoaded = false;
        private string[] availableModels = new string[] { "Mira" };
        private int selectedModelIndex = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<AuthUI_IMGUI>() == null)
            {
                GameObject go = new GameObject("AuthUI_IMGUI_AutoSpawn");
                go.AddComponent<AuthUI_IMGUI>();
                DontDestroyOnLoad(go);
                Debug.Log("[AuthUI_IMGUI] Auto-spawned AuthUI_IMGUI_AutoSpawn GameObject.");
            }
        }

        private void Awake()
        {
            // Auto-create AuthManager if not exists
            if (AuthManager.Instance == null)
            {
                GameObject authManagerObj = new GameObject("AuthManager");
                authManagerObj.AddComponent<AuthManager>();
                DontDestroyOnLoad(authManagerObj);
                Debug.Log("[AuthUI_IMGUI] AuthManager was automatically created.");
            }

            // Auto-create PlayerManager if not exists
            if (PlayerManager.Instance == null)
            {
                GameObject playerManagerObj = new GameObject("PlayerManager");
                playerManagerObj.AddComponent<PlayerManager>();
                DontDestroyOnLoad(playerManagerObj);
                Debug.Log("[AuthUI_IMGUI] PlayerManager was automatically created.");
            }
        }

        private bool needToCreateCharacter = false;

        private void Start()
        {
            // Subscribe to events
            AuthManager.Instance.OnLoginSuccess += OnLoginSuccess;
            AuthManager.Instance.OnLoginFailed += OnLoginFailed;
            AuthManager.Instance.OnRegisterSuccess += OnRegisterSuccess;
            AuthManager.Instance.OnRegisterFailed += OnRegisterFailed;

            PlayerManager.Instance.OnPlayersLoaded += OnPlayersLoaded;
            PlayerManager.Instance.OnPlayerSelected += OnPlayerSelected;
            PlayerManager.Instance.OnPlayerCreated += OnPlayerCreated;
            PlayerManager.Instance.OnError += OnPlayerError;

            // Load saved session if any
            if (AuthManager.Instance.IsLoggedIn)
            {
                statusMessage = "Đã tự động đăng nhập với token lưu sẵn.";
                FetchPlayers();
            }
        }

        private void OnDestroy()
        {
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginSuccess -= OnLoginSuccess;
                AuthManager.Instance.OnLoginFailed -= OnLoginFailed;
                AuthManager.Instance.OnRegisterSuccess -= OnRegisterSuccess;
                AuthManager.Instance.OnRegisterFailed -= OnRegisterFailed;
            }

            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnPlayersLoaded -= OnPlayersLoaded;
                PlayerManager.Instance.OnPlayerSelected -= OnPlayerSelected;
                PlayerManager.Instance.OnPlayerCreated -= OnPlayerCreated;
                PlayerManager.Instance.OnError -= OnPlayerError;
            }
        }

        private void OnGUI()
        {
            if (AuthManager.Instance == null || PlayerManager.Instance == null)
            {
                return;
            }

            // Set GUI skin font size slightly larger for legibility if needed
            GUI.skin.label.fontSize = 12;
            GUI.skin.button.fontSize = 12;

            // Draw UI in the top right / middle area depending on status
            Rect windowRect = new Rect(Screen.width - 320, 15, 300, 450);
            
            // If already completed character selection, show selected status or hide
            if (AuthManager.Instance.IsLoggedIn && PlayerManager.Instance.CurrentPlayer != null && !needToCreateCharacter)
            {
                GUILayout.BeginArea(new Rect(Screen.width - 320, 15, 300, 150));
                GUILayout.BeginVertical("box");
                GUILayout.Label($"<b>User:</b> {AuthManager.Instance.CurrentUser?.username ?? "Đã đăng nhập"}", GUILayout.ExpandWidth(true));
                GUILayout.Label($"<b>Nhân vật:</b> {PlayerManager.Instance.CurrentPlayer.username} (Gold: {PlayerManager.Instance.CurrentPlayer.gold})", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Đổi nhân vật / Đăng xuất", GUILayout.Height(30)))
                {
                    PlayerManager.Instance.SelectPlayer(null);
                    AuthManager.Instance.Logout();
                    playersLoaded = false;
                    needToCreateCharacter = false;
                }
                GUILayout.EndVertical();
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(windowRect);
            GUILayout.BeginVertical("box");

            if (!AuthManager.Instance.IsLoggedIn)
            {
                if (!isRegisterMode)
                {
                    DrawLoginForm();
                }
                else
                {
                    DrawRegisterForm();
                }
            }
            else
            {
                DrawCharacterSelectionForm();
            }

            // Display status message if any
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(10);
                GUILayout.Label($"<b>Trạng thái:</b> {statusMessage}", GUILayout.ExpandWidth(true));
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawLoginForm()
        {
            GUILayout.Label("<b>ĐĂNG NHẬP HỆ THỐNG</b>", GUILayout.Height(25));
            GUILayout.Space(5);

            GUILayout.Label("Email hoặc Tên tài khoản:");
            email = GUILayout.TextField(email, GUILayout.Height(25));

            GUILayout.Label("Mật khẩu:");
            password = GUILayout.PasswordField(password, '*', GUILayout.Height(25));

            GUILayout.Space(15);

            if (isLoading)
            {
                GUILayout.Box("Đang xử lý đăng nhập...", GUILayout.ExpandWidth(true));
            }
            else
            {
                if (GUILayout.Button("Đăng Nhập", GUILayout.Height(35)))
                {
                    isLoading = true;
                    statusMessage = "Đang gửi yêu cầu đăng nhập...";
                    AuthManager.Instance.Login(email, password);
                }

                GUILayout.Space(10);
                if (GUILayout.Button("Chưa có tài khoản? Đăng ký ngay", GUILayout.Height(25)))
                {
                    isRegisterMode = true;
                    statusMessage = "";
                }
            }
        }

        private void DrawRegisterForm()
        {
            GUILayout.Label("<b>ĐĂNG KÝ TÀI KHOẢN MỚI</b>", GUILayout.Height(25));
            GUILayout.Space(5);

            GUILayout.Label("Tên hiển thị (Username):");
            username = GUILayout.TextField(username, GUILayout.Height(25));

            GUILayout.Label("Email:");
            email = GUILayout.TextField(email, GUILayout.Height(25));

            GUILayout.Label("Mật khẩu (>= 6 ký tự):");
            password = GUILayout.PasswordField(password, '*', GUILayout.Height(25));

            GUILayout.Label("Xác nhận mật khẩu:");
            confirmPassword = GUILayout.PasswordField(confirmPassword, '*', GUILayout.Height(25));

            GUILayout.Space(15);

            if (isLoading)
            {
                GUILayout.Box("Đang xử lý đăng ký...", GUILayout.ExpandWidth(true));
            }
            else
            {
                if (GUILayout.Button("Đăng Ký", GUILayout.Height(35)))
                {
                    isLoading = true;
                    statusMessage = "Đang gửi yêu cầu đăng ký...";
                    AuthManager.Instance.Register(username, email, password, confirmPassword);
                }

                GUILayout.Space(10);
                if (GUILayout.Button("Đã có tài khoản? Đăng nhập", GUILayout.Height(25)))
                {
                    isRegisterMode = false;
                    statusMessage = "";
                }
            }
        }

        private void DrawCharacterSelectionForm()
        {
            GUILayout.Label($"<b>CHỌN NHÂN VẬT</b>", GUILayout.Height(25));
            GUILayout.Label($"Tài khoản: {AuthManager.Instance.CurrentUser?.username ?? "Đăng nhập thành công"}");
            GUILayout.Space(10);

            if (!playersLoaded)
            {
                if (GUILayout.Button("Tải danh sách nhân vật", GUILayout.Height(30)))
                {
                    FetchPlayers();
                }
            }
            else
            {
                List<PlayerData> players = PlayerManager.Instance.UserPlayers;
                if (players == null || players.Count == 0)
                {
                    GUILayout.Label("<i>Chưa có nhân vật nào. Hãy tạo nhân vật mới ở dưới!</i>");
                    needToCreateCharacter = true;
                }
                else
                {
                    GUILayout.Label("Danh sách nhân vật:");
                    foreach (var p in players)
                    {
                        if (GUILayout.Button($"{p.username} (Level: {(p.attributes != null ? p.attributes.level : 1)}, Gold: {p.gold})", GUILayout.Height(30)))
                        {
                            statusMessage = $"Đã chọn nhân vật: {p.username}";
                            PlayerManager.Instance.SelectPlayer(p);
                        }
                    }
                }

                GUILayout.Space(15);
                GUILayout.Label("<b>Chọn Model nhân vật:</b>");
                selectedModelIndex = GUILayout.Toolbar(selectedModelIndex, availableModels, GUILayout.Height(25));
                
                GUILayout.Space(5);
                GUILayout.Label("<b>Tên nhân vật mới:</b>");
                newPlayerName = GUILayout.TextField(newPlayerName, GUILayout.Height(25));
                if (GUILayout.Button("Tạo Nhân Vật", GUILayout.Height(30)))
                {
                    if (string.IsNullOrEmpty(newPlayerName))
                    {
                        statusMessage = "Tên nhân vật không được để trống!";
                    }
                    else
                    {
                        string modelName = availableModels[selectedModelIndex];
                        statusMessage = $"Đang tạo nhân vật {newPlayerName} với model {modelName}...";
                        PlayerManager.Instance.CreatePlayer(newPlayerName, modelName);
                    }
                }
            }

            GUILayout.Space(15);
            if (GUILayout.Button("Đăng Xuất", GUILayout.Height(25)))
            {
                AuthManager.Instance.Logout();
                playersLoaded = false;
                needToCreateCharacter = false;
                PlayerManager.Instance.SelectPlayer(null);
                statusMessage = "Đã đăng xuất.";
            }
        }

        private void FetchPlayers()
        {
            statusMessage = "Đang tải danh sách nhân vật...";
            PlayerManager.Instance.GetUserPlayers();
        }

        #region Event Callbacks
        private void OnLoginSuccess()
        {
            isLoading = false;
            statusMessage = "Đăng nhập thành công!";
            FetchPlayers();
        }

        private void OnLoginFailed(string err)
        {
            isLoading = false;
            statusMessage = $"Đăng nhập thất bại: {err}";
        }

        private void OnRegisterSuccess()
        {
            isLoading = false;
            isRegisterMode = false;
            statusMessage = "Đăng ký thành công! Hãy đăng nhập.";
        }

        private void OnRegisterFailed(string err)
        {
            isLoading = false;
            statusMessage = $"Đăng ký thất bại: {err}";
        }

        private void OnPlayersLoaded(List<PlayerData> players)
        {
            playersLoaded = true;
            statusMessage = $"Đã tải {players.Count} nhân vật của tài khoản.";
            if (players == null || players.Count == 0)
            {
                needToCreateCharacter = true;
            }
            else
            {
                needToCreateCharacter = false;
            }
        }

        private void OnPlayerSelected(PlayerData player)
        {
            if (player != null)
            {
                statusMessage = $"Đã kích hoạt nhân vật: {player.username}";
                
                // Tự động kết nối Client nếu không có quyền Hosting
                if (!AuthManager.Instance.UserPermissions.Contains("Hosting"))
                {
                    if (Unity.Netcode.NetworkManager.Singleton != null)
                    {
                        statusMessage = "Đang kết nối vào máy chủ (Client)...";
                        Unity.Netcode.NetworkManager.Singleton.StartClient();
                    }
                }
            }
        }

        private void OnPlayerCreated(PlayerData player)
        {
            statusMessage = $"Tạo nhân vật {player.username} thành công!";
            newPlayerName = "";
            FetchPlayers();
        }

        private void OnPlayerError(string err)
        {
            statusMessage = $"Lỗi: {err}";
        }
        #endregion
    }
}
