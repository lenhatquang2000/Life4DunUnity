using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AILife.Auth
{
    /// <summary>
    /// Quản lý Players - Lấy danh sách, tạo mới, chọn player
    /// </summary>
    public class PlayerManager : MonoBehaviour
    {
        public static PlayerManager Instance { get; private set; }
        
        [Header("API Configuration")]
        [SerializeField] private string apiBaseUrl = "http://localhost:5205/api";
        
        [Header("Current Player")]
        [SerializeField] private PlayerData currentPlayer;
        [SerializeField] private List<PlayerData> userPlayers = new List<PlayerData>();
        
        // Events
        public event Action<List<PlayerData>> OnPlayersLoaded;
        public event Action<PlayerData> OnPlayerCreated;
        public event Action<PlayerData> OnPlayerSelected;
        public event Action<string> OnError;
        
        // Properties
        public PlayerData CurrentPlayer => currentPlayer;
        public List<PlayerData> UserPlayers => userPlayers;
        public bool HasPlayers => userPlayers.Count > 0;
        
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
        
        #region Public Methods
        
        /// <summary>
        /// Lấy danh sách tất cả players của user hiện tại
        /// GET /api/Players
        /// </summary>
        public void GetUserPlayers()
        {
            if (!AuthManager.Instance.IsLoggedIn)
            {
                OnError?.Invoke("Vui lòng đăng nhập trước");
                return;
            }
            
            StartCoroutine(GetPlayersCoroutine());
        }
        
        /// <summary>
        /// Tạo player mới
        /// POST /api/Players
        /// </summary>
        public void CreatePlayer(string username, string modelName)
        {
            if (!AuthManager.Instance.IsLoggedIn)
            {
                OnError?.Invoke("Vui lòng đăng nhập trước");
                return;
            }
            
            if (string.IsNullOrEmpty(username) || username.Length < 3)
            {
                OnError?.Invoke("Tên nhân vật phải có ít nhất 3 ký tự");
                return;
            }
            
            StartCoroutine(CreatePlayerCoroutine(username, modelName));
        }
        
        /// <summary>
        /// Lấy thông tin chi tiết một player
        /// GET /api/Players/{id}
        /// </summary>
        public void GetPlayerDetails(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                OnError?.Invoke("Player ID không hợp lệ");
                return;
            }
            
            StartCoroutine(GetPlayerDetailsCoroutine(playerId));
        }
        
        /// <summary>
        /// Chọn player để chơi
        /// </summary>
        public void SelectPlayer(PlayerData player)
        {
            currentPlayer = player;
            OnPlayerSelected?.Invoke(player);
            
            // Lưu player ID đã chọn
            if (player != null)
            {
                PlayerPrefs.SetString("SelectedPlayerId", player.id);
            }
            else
            {
                PlayerPrefs.DeleteKey("SelectedPlayerId");
            }
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Load player đã chọn trước đó
        /// </summary>
        public void LoadSelectedPlayer()
        {
            string savedPlayerId = PlayerPrefs.GetString("SelectedPlayerId", "");
            if (!string.IsNullOrEmpty(savedPlayerId))
            {
                GetPlayerDetails(savedPlayerId);
            }
        }
        
        #endregion
        
        #region Private Coroutines
        
        private IEnumerator GetPlayersCoroutine()
        {
            string userId = AuthManager.Instance.CurrentUser.id;
            using (UnityWebRequest request = UnityWebRequest.Get($"{apiBaseUrl}/Players/user/{userId}"))
            {
                // Thêm Authorization header
                request.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.AccessToken}");
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseJson = request.downloadHandler.text;
                    
                    // Parse array of players
                    // Vì JsonUtility không parse array trực tiếp, cần wrapper
                    string wrappedJson = $"{{\"players\":{responseJson}}}";
                    PlayerListWrapper wrapper = JsonUtility.FromJson<PlayerListWrapper>(wrappedJson);
                    
                    userPlayers = new List<PlayerData>(wrapper.players);
                    OnPlayersLoaded?.Invoke(userPlayers);
                }
                else
                {
                    string errorMessage = "Không thể tải danh sách nhân vật";
                    try
                    {
                        ErrorResponse error = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                        if (!string.IsNullOrEmpty(error.detail))
                            errorMessage = error.detail;
                    }
                    catch { }
                    
                    OnError?.Invoke(errorMessage);
                }
            }
        }
        
        private IEnumerator CreatePlayerCoroutine(string username, string modelName)
        {
            CreatePlayerRequest requestData = new CreatePlayerRequest
            {
                userId = AuthManager.Instance.CurrentUser.id,
                username = username,
                modelName = modelName
            };
            
            string json = JsonUtility.ToJson(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            
            string targetUrl = $"{apiBaseUrl}/Players";
            Debug.Log($"[PlayerManager] Sending POST Request to URL: {targetUrl}\nPayload JSON: {json}");
            
            using (UnityWebRequest request = new UnityWebRequest(
                targetUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.AccessToken}");
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseJson = request.downloadHandler.text;
                    Debug.Log($"[PlayerManager] CreatePlayer Success! Response Code: {request.responseCode}\nResponse JSON: {responseJson}");
                    PlayerData newPlayer = JsonUtility.FromJson<PlayerData>(responseJson);
                    
                    // Thêm vào danh sách
                    userPlayers.Add(newPlayer);
                    
                    OnPlayerCreated?.Invoke(newPlayer);
                    
                    // Tự động chọn player mới tạo
                    SelectPlayer(newPlayer);
                }
                else
                {
                    Debug.LogError($"[PlayerManager] CreatePlayer Failed! Response Code: {request.responseCode}\nError/Response content: {request.downloadHandler.text}");
                    string errorMessage = "Không thể tạo nhân vật";
                    
                    // Parse validation error
                    if (request.responseCode == 400)
                    {
                        errorMessage = "Tên nhân vật đã tồn tại";
                    }
                    else
                    {
                        try
                        {
                            ErrorResponse error = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                            if (!string.IsNullOrEmpty(error.detail))
                                errorMessage = error.detail;
                        }
                        catch { }
                    }
                    
                    OnError?.Invoke(errorMessage);
                }
            }
        }
        
        private IEnumerator GetPlayerDetailsCoroutine(string playerId)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{apiBaseUrl}/Players/{playerId}"))
            {
                request.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.AccessToken}");
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseJson = request.downloadHandler.text;
                    PlayerData player = JsonUtility.FromJson<PlayerData>(responseJson);
                    
                    currentPlayer = player;
                    OnPlayerSelected?.Invoke(player);
                }
                else
                {
                    OnError?.Invoke("Không thể tải thông tin nhân vật");
                }
            }
        }
        
        #endregion
        
        #region Data Models
        
        [System.Serializable]
        public class PlayerListWrapper
        {
            public PlayerData[] players;
        }
        
        [System.Serializable]
        public class CreatePlayerRequest
        {
            public string userId;
            public string username;
            public string modelName;
        }
        
        #endregion
    }
}
