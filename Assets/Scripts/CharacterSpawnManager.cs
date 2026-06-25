using UnityEngine;
using Unity.Netcode;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// Quản lý spawn đúng Character prefab dựa trên modelName của PlayerData.
///
/// CÁCH HOẠT ĐỘNG:
///   1. Client gửi modelName lên Server qua ConnectionApprovalResponse payload
///   2. Server nhận, tìm đúng prefab trong CharacterRegistry
///   3. Server spawn prefab đúng loại tại vị trí spawn
///   4. Mira vẫn là Default Player Prefab (fallback nếu không tìm thấy prefab)
///
/// SETUP:
///   - Thêm component này vào cùng GameObject với NetworkManager
///   - Bật "Connection Approval" trong NetworkManager Inspector
///   - Tắt "Force Same Prefabs" nếu muốn nhiều loại prefab khác nhau cùng lúc
/// </summary>
public class CharacterSpawnManager : MonoBehaviour
{
    [Header("Character Registry")]
    [Tooltip("Để trống - sẽ tự load từ Resources/CharacterRegistry.asset")]
    private CharacterRegistry _registry;

    [Header("Spawn Settings")]
    [SerializeField] private Vector3 defaultSpawnPosition = Vector3.zero;
    [SerializeField] private float spawnRadius = 2f; // Random radius quanh spawn point

    // Map: clientId -> modelName (Server lưu để spawn sau khi approve)
    private Dictionary<ulong, string> _pendingModelNames = new Dictionary<ulong, string>();

    private void Awake()
    {
        _registry = Resources.Load<CharacterRegistry>("CharacterRegistry");
        if (_registry == null)
            Debug.LogWarning("[CharacterSpawnManager] Khong tim thay Resources/CharacterRegistry.asset!");
        else
            Debug.Log($"[CharacterSpawnManager] Loaded registry: {_registry.characters.Count} characters.");
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton == null) return;

        // Đăng ký Connection Approval callback (Server)
        NetworkManager.Singleton.ConnectionApprovalCallback = OnConnectionApproval;

        // Đăng ký spawn callback khi client được chấp nhận (Server)
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.ConnectionApprovalCallback = null;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    // ─── CLIENT: Gửi modelName lên Server khi kết nối ────────────────────────────
    /// <summary>
    /// Gọi method này TRƯỚC khi StartClient() để gửi modelName.
    /// </summary>
    public static void SetClientModelName(string modelName)
    {
        if (NetworkManager.Singleton == null) return;

        // Encode modelName vào ConnectionData (payload gửi kèm khi connect)
        byte[] payload = Encoding.UTF8.GetBytes(modelName ?? "Mira");
        NetworkManager.Singleton.NetworkConfig.ConnectionData = payload;
        Debug.Log($"[CharacterSpawnManager] Client set modelName payload: '{modelName}'");
    }

    // ─── SERVER: Nhận và duyệt kết nối ──────────────────────────────────────────
    private void OnConnectionApproval(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        // Đọc modelName từ payload
        string modelName = "Mira";
        if (request.Payload != null && request.Payload.Length > 0)
        {
            modelName = Encoding.UTF8.GetString(request.Payload);
        }

        Debug.Log($"[CharacterSpawnManager] Server: Client {request.ClientNetworkId} xin ket noi voi model '{modelName}'");

        // Lưu modelName để dùng khi spawn
        _pendingModelNames[request.ClientNetworkId] = modelName;

        // Chấp nhận kết nối, KHÔNG tự spawn (createPlayerObject = false)
        response.Approved        = true;
        response.CreatePlayerObject = false; // Ta sẽ tự spawn đúng prefab
        response.Pending         = false;
    }

    // ─── SERVER: Spawn đúng prefab sau khi client kết nối ────────────────────────
    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        string modelName = "Mira";
        if (_pendingModelNames.TryGetValue(clientId, out string saved))
        {
            modelName = saved;
            _pendingModelNames.Remove(clientId);
        }

        Debug.Log($"[CharacterSpawnManager] Server: Spawning '{modelName}' cho client {clientId}");

        // Tìm prefab trong registry
        GameObject prefabToSpawn = null;
        if (_registry != null)
            prefabToSpawn = _registry.GetPrefab(modelName);

        // Fallback: dùng Default Player Prefab (Mira)
        if (prefabToSpawn == null)
        {
            prefabToSpawn = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            Debug.LogWarning($"[CharacterSpawnManager] Khong tim thay prefab '{modelName}', fallback Mira.");
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError("[CharacterSpawnManager] Khong co prefab nao de spawn!");
            return;
        }

        // Random vị trí quanh spawn point
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = defaultSpawnPosition + new Vector3(offset.x, offset.y, 0);

        GameObject playerObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            netObj.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[CharacterSpawnManager] Da spawn '{modelName}' cho client {clientId} tai {spawnPos}");
        }
        else
        {
            Debug.LogError($"[CharacterSpawnManager] Prefab '{modelName}' khong co NetworkObject component!");
            Destroy(playerObj);
        }
    }
}
