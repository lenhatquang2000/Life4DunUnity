using UnityEngine;
using Unity.Netcode;

public class NetworkManagerUI : MonoBehaviour
{
    private void OnGUI()
    {
        // Chỉ hiện thị Hosting Bar nếu người dùng đã đăng nhập và có quyền "Hosting"
        if (AILife.Auth.AuthManager.Instance == null || 
            !AILife.Auth.AuthManager.Instance.IsLoggedIn || 
            !AILife.Auth.AuthManager.Instance.UserPermissions.Contains("Hosting"))
        {
            return;
        }

        // Tạo một vùng nhỏ ở góc trên bên trái màn hình
        GUILayout.BeginArea(new Rect(15, 15, 250, 200));

        if (NetworkManager.Singleton == null)
        {
            GUILayout.Label("NetworkManager not found");
            GUILayout.EndArea();
            return;
        }
        
        // Nếu mạng chưa được khởi động (chưa chạy Host/Server/Client)
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            StartButtons();
        }
        else
        {
            StatusLabels();
        }

        GUILayout.EndArea();
    }

    private void StartButtons()
    {
        // Nút bấm chạy Host (Vừa làm Server vừa làm Player 1)
        if (GUILayout.Button("Start Host (Server + Player 1)", GUILayout.Height(40)))
        {
            if (AILife.Auth.PlayerManager.Instance != null && AILife.Auth.PlayerManager.Instance.CurrentPlayer != null)
            {
                string modelName = AILife.Auth.PlayerManager.Instance.CurrentPlayer.model;
                Debug.Log($"[NetworkManagerUI] Host is setting modelName: {modelName}");
                CharacterSpawnManager.SetClientModelName(modelName);
            }
            NetworkManager.Singleton.StartHost();
            Debug.Log("[NetworkManagerUI] Host started.");
        }
        
        GUILayout.Space(10);

        // Nút bấm chạy Client (Kết nối vào IP localhost 127.0.0.1 mặc định)
        if (GUILayout.Button("Start Client (Player 2)", GUILayout.Height(40)))
        {
            if (AILife.Auth.PlayerManager.Instance != null && AILife.Auth.PlayerManager.Instance.CurrentPlayer != null)
            {
                string modelName = AILife.Auth.PlayerManager.Instance.CurrentPlayer.model;
                Debug.Log($"[NetworkManagerUI] Client is setting modelName: {modelName}");
                CharacterSpawnManager.SetClientModelName(modelName);
            }
            NetworkManager.Singleton.StartClient();
            Debug.Log("[NetworkManagerUI] Client started.");
        }
        
        GUILayout.Space(10);

        // Nút bấm chạy Server Only (Nếu muốn chạy máy chủ không có người chơi)
        if (GUILayout.Button("Start Server Only", GUILayout.Height(30)))
        {
            NetworkManager.Singleton.StartServer();
            Debug.Log("[NetworkManagerUI] Server started.");
        }
    }

    private void StatusLabels()
    {
        string mode = "Unknown";
        if (NetworkManager.Singleton.IsHost) mode = "Host (Server + Local Player)";
        else if (NetworkManager.Singleton.IsServer) mode = "Dedicated Server";
        else if (NetworkManager.Singleton.IsClient) mode = "Client (Remote Player)";

        GUILayout.Box($"Mode: {mode}", GUILayout.ExpandWidth(true));
        
        GUILayout.Label($"Transport: {NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetType().Name}");
        
        // Hiển thị số lượng người chơi đang kết nối (chỉ Server/Host thấy chính xác)
        if (NetworkManager.Singleton.IsServer)
        {
            GUILayout.Label($"Connected Clients: {NetworkManager.Singleton.ConnectedClientsList.Count}");
        }

        GUILayout.Space(10);

        // Nút ngắt kết nối
        if (GUILayout.Button("Disconnect", GUILayout.Height(30)))
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[NetworkManagerUI] Network shutdown.");
        }
    }
}
