using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Quản lý sinh quái vật ngẫu nhiên trong thế giới game.
/// Hỗ trợ cả chế độ chơi mạng (Unity Netcode) và chơi đơn (Offline).
/// </summary>
public class ControllCreateMonster : MonoBehaviour
{
    public enum SpawnMode
    {
        AroundSpawner, // Sinh quái vật xung quanh vị trí của Spawner này
        AroundPlayers  // Sinh quái vật xung quanh vị trí của các người chơi ngẫu nhiên
    }

    [Header("Monster Prefabs")]
    [Tooltip("Danh sách các prefab quái vật để spawn ngẫu nhiên.")]
    [SerializeField] private List<GameObject> monsterPrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    [Tooltip("Số lượng quái vật tối đa tồn tại cùng lúc.")]
    [SerializeField] private int maxMonsterCount = 20;

    [Tooltip("Tự động spawn quái vật ngay khi game/server bắt đầu.")]
    [SerializeField] private bool spawnOnStart = true;

    [Tooltip("Số lượng quái vật sinh ra lúc bắt đầu.")]
    [SerializeField] private int spawnCountOnStart = 5;

    [Tooltip("Tự động spawn thêm quái vật theo chu kỳ.")]
    [SerializeField] private bool spawnPeriodically = true;

    [Tooltip("Khoảng thời gian (giây) giữa các đợt spawn.")]
    [SerializeField] private float spawnInterval = 5f;

    [Tooltip("Số lượng quái vật sinh ra ở mỗi chu kỳ.")]
    [SerializeField] private int spawnCountPerInterval = 1;

    [Header("Location Settings")]
    [Tooltip("Chế độ chọn vị trí sinh quái vật.")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.AroundSpawner;

    [Tooltip("Tâm khu vực sinh quái (dùng cho chế độ AroundSpawner). Nếu để trống sẽ tự lấy transform của script này.")]
    [SerializeField] private Transform spawnAreaCenter;

    [Tooltip("Bán kính tối đa để sinh quái vật.")]
    [SerializeField] private float spawnRadius = 10f;

    [Tooltip("Bán kính tối thiểu so với tâm (để tránh spawn đè lên người chơi hoặc tâm).")]
    [SerializeField] private float minSpawnRadius = 2f;

    private List<GameObject> activeMonsters = new List<GameObject>();
    private Coroutine spawnCoroutine;
    private bool isServerRunning = false;

    private void Start()
    {
        if (spawnAreaCenter == null)
        {
            spawnAreaCenter = transform;
        }

        // Đăng ký các sự kiện Netcode nếu NetworkManager tồn tại
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.OnServerStopped += OnServerStopped;

            // Nếu server đã chạy từ trước (khi object này được kích hoạt muộn)
            if (NetworkManager.Singleton.IsServer)
            {
                OnServerStarted();
            }
        }
        else
        {
            // Nếu không có NetworkManager (chạy Offline/Local testing)
            StartSpawningOffline();
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
        }
    }

    private void OnServerStarted()
    {
        if (isServerRunning) return;
        isServerRunning = true;
        Debug.Log("[ControllCreateMonster] Server started. Active monster spawning...");

        if (spawnOnStart)
        {
            SpawnMonsters(spawnCountOnStart);
        }

        if (spawnPeriodically)
        {
            if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
            spawnCoroutine = StartCoroutine(PeriodicSpawnCoroutine());
        }
    }

    private void OnServerStopped(bool isHost)
    {
        isServerRunning = false;
        Debug.Log("[ControllCreateMonster] Server stopped. Spawning stopped.");
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
        // Khi server tắt, các NetworkObject tự huỷ, ta dọn danh sách quản lý
        activeMonsters.Clear();
    }

    private void StartSpawningOffline()
    {
        Debug.Log("[ControllCreateMonster] Running in Offline mode (No NetworkManager).");
        
        if (spawnOnStart)
        {
            SpawnMonsters(spawnCountOnStart);
        }

        if (spawnPeriodically)
        {
            if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
            spawnCoroutine = StartCoroutine(PeriodicSpawnCoroutine());
        }
    }

    private IEnumerator PeriodicSpawnCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // Chỉ spawn quái vật nếu:
            // 1. Không dùng Network (Offline mode)
            // 2. Hoặc đang chạy online và là Server/Host
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
            {
                SpawnMonsters(spawnCountPerInterval);
            }
        }
    }

    private void CleanActiveMonstersList()
    {
        // Loại bỏ các tham chiếu null (những con quái đã bị tiêu diệt)
        activeMonsters.RemoveAll(item => item == null);
    }

    private void SpawnMonsters(int count)
    {
        if (monsterPrefabs == null || monsterPrefabs.Count == 0)
        {
            Debug.LogWarning("[ControllCreateMonster] Cannot spawn: Monster Prefabs list is empty!");
            return;
        }

        CleanActiveMonstersList();

        int spawnedThisTime = 0;
        for (int i = 0; i < count; i++)
        {
            if (activeMonsters.Count >= maxMonsterCount)
            {
                Debug.Log($"[ControllCreateMonster] Reached maximum monster count limit ({maxMonsterCount}). Spawning paused.");
                break;
            }

            // Chọn ngẫu nhiên một prefab trong danh sách
            GameObject prefab = monsterPrefabs[Random.Range(0, monsterPrefabs.Count)];
            if (prefab == null) continue;

            // Lấy vị trí ngẫu nhiên
            Vector3 spawnPos = GetRandomSpawnPosition();

            // Tạo đối tượng quái vật
            GameObject monsterInstance = Instantiate(prefab, spawnPos, Quaternion.identity);
            activeMonsters.Add(monsterInstance);
            spawnedThisTime++;

            // Thực hiện đồng bộ qua Netcode nếu có NetworkManager đang chạy
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkObject netObj = monsterInstance.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                }
                else
                {
                    Debug.LogWarning($"[ControllCreateMonster] Prefab '{prefab.name}' does not have a NetworkObject component. It will only spawn on the server.");
                }
            }
        }

        if (spawnedThisTime > 0)
        {
            Debug.Log($"[ControllCreateMonster] Spawned {spawnedThisTime} monster(s). Active count: {activeMonsters.Count}/{maxMonsterCount}");
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Vector3 center = spawnAreaCenter.position;

        if (spawnMode == SpawnMode.AroundPlayers)
        {
            // Lấy tâm dựa trên vị trí người chơi ngẫu nhiên
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                var clients = NetworkManager.Singleton.ConnectedClientsList;
                if (clients != null && clients.Count > 0)
                {
                    var randomClient = clients[Random.Range(0, clients.Count)];
                    if (randomClient != null && randomClient.PlayerObject != null)
                    {
                        center = randomClient.PlayerObject.transform.position;
                    }
                }
            }
            else
            {
                // Tìm bằng Tag trong chế độ chơi đơn Offline
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                if (players.Length > 0)
                {
                    center = players[Random.Range(0, players.Length)].transform.position;
                }
            }
        }

        // Tạo offset ngẫu nhiên
        Vector2 randomPoint = Random.insideUnitCircle;
        float distance = Mathf.Lerp(minSpawnRadius, spawnRadius, randomPoint.magnitude);
        Vector2 direction = randomPoint.normalized;
        if (direction == Vector2.zero) direction = Vector2.up;

        Vector3 offset = new Vector3(direction.x * distance, direction.y * distance, 0);
        return center + offset;
    }
}
