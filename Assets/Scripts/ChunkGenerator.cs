using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class ChunkGenerator : NetworkBehaviour
{
    public static ChunkGenerator Instance;
    
    [Header("Ground Settings")]
    public string groundSpritesPath = "Assets/Map/Ground";
    public int tilesPerChunk = 16;
    public float pixelsPerUnit = 32f; // Tile size in pixels
    
    [System.Serializable]
    public class ObjectSpawnConfig
    {
        public string spriteName;        // Tên sprite (vd: "forest_obj_11_0")
        public GameObject prefab;        // Prefab dùng để instantiate thay vì tạo động
        public float scaleMultiplier = 1f; // Nhân tỉ lệ (Scale) khi spawn (mặc định là 1)
        [Range(0f, 1f)]
        public float spawnChance;        // Xác suất spawn (0-1)
        public bool useNoiseGeneration;  // Dùng Perlin Noise (cho cây) hay random thuần
        [Range(0f, 1f)]
        public float noiseThreshold;     // Ngưỡng noise (nếu dùng noise)
        [Range(0.01f, 0.5f)]
        public float noiseScale;         // Kích thước vùng (nếu dùng noise)
        
        [Header("Collider Settings")]
        public bool addCollider;         // Có thêm collider không
        public Vector2 colliderSize = Vector2.one;    // Kích thước collider
        public Vector2 colliderOffset = Vector2.zero; // Offset collider
        public bool isTrigger;           // Collider có phải trigger không

        [Header("Shadow Settings")]
        public bool addShadow = true;                       // Có tự động thêm bóng không
        public Vector2 shadowScale = new Vector2(1.2f, 0.6f); // Kích thước bóng (rộng, cao)
        public float shadowOffset = -0.1f;                  // Vị trí lệch Y của bóng so với gốc cây
    }
    
    [Header("Object Settings")]
    public ObjectSpawnConfig[] objectsToSpawn = new ObjectSpawnConfig[]
    {
        new ObjectSpawnConfig 
        { 
            spriteName = "forest_obj_11_0", 
            spawnChance = 0.1f,
            useNoiseGeneration = true,
            noiseThreshold = 0.6f,
            noiseScale = 0.1f,
            addCollider = true,
            colliderSize = new Vector2(0.8f, 0.8f),
            colliderOffset = Vector2.zero,
            isTrigger = false
        },
        new ObjectSpawnConfig 
        { 
            spriteName = "to_cho_ti_mt_s_assets_l (4)_0", 
            spawnChance = 0.05f,
            useNoiseGeneration = false,
            noiseThreshold = 0.5f,
            noiseScale = 0.15f,
            addCollider = true,
            colliderSize = new Vector2(0.9f, 0.9f),
            colliderOffset = Vector2.zero,
            isTrigger = false
        },
        new ObjectSpawnConfig 
        { 
            spriteName = "to_cho_ti_mt_s_assets_l (7)_0", 
            spawnChance = 0.05f,
            useNoiseGeneration = false,
            noiseThreshold = 0.5f,
            noiseScale = 0.15f,
            addCollider = true,
            colliderSize = new Vector2(0.6f, 0.6f),
            colliderOffset = Vector2.zero,
            isTrigger = false
        }
    };
    
    public int forestNoiseSeed = 12345; // Seed để tạo map luôn giống nhau
    public NetworkVariable<int> forestNoiseSeedNetwork = new NetworkVariable<int>(12345);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            forestNoiseSeedNetwork.Value = forestNoiseSeed;
        }
    }

    public int GetSeed()
    {
        if (IsSpawned)
        {
            return forestNoiseSeedNetwork.Value;
        }
        return forestNoiseSeed;
    }
    
    [Header("Y-Sorting Settings")]
    public bool useYSorting = true; // Bật Y-sorting (vật ở dưới che vật ở trên)
    public int ySortingMultiplier = 1; // Nhân với Y (phải match với MiraController)
    
    [Header("Ground Sprites List")]
    public Sprite[] groundSprites; // Cho phép kéo thả trực tiếp trong Inspector

    [Header("Debug - Available Sprites")]
    [TextArea(3, 10)]
    public string availableSprites = "Run game to see list..."; // Hiển thị danh sách sprites
    
    private Dictionary<string, Sprite> objectSprites = new Dictionary<string, Sprite>(); // Cache sprites by name
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[ChunkGenerator] Instance created and set");
        }
        else
        {
            Debug.LogWarning("[ChunkGenerator] Duplicate instance destroyed");
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        Debug.Log("[ChunkGenerator] Start() called");
        LoadGroundSprites();
        LoadObjectSprites();
    }
    
    void LoadObjectSprites()
    {
        Debug.Log($"[ChunkGenerator] LoadObjectSprites() - Looking for {objectsToSpawn.Length} object types");
        
        List<string> allSpriteNames = new List<string>();
        
        #if UNITY_EDITOR
        // Tìm sprites trong cả Ground và TopDown folder
        string[] groundGuids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { groundSpritesPath });
        string[] topDownPath = new[] { "Assets/Map/TopDown" };
        string[] topDownGuids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", topDownPath);
        
        List<string> allGuids = new List<string>(groundGuids);
        allGuids.AddRange(topDownGuids);
        
        Debug.Log($"[ChunkGenerator] ===== LOADING OBJECT SPRITES =====");
        Debug.Log($"[ChunkGenerator] Found {allGuids.Count} sprites (Ground: {groundGuids.Length}, TopDown: {topDownGuids.Length})");
        Debug.Log($"[ChunkGenerator] Looking for object types:");
        foreach (var config in objectsToSpawn)
        {
            Debug.Log($"[ChunkGenerator]   - Need: '{config.spriteName}'");
        }
        
        int foundCount = 0;
        foreach (string guid in allGuids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                allSpriteNames.Add(sprite.name);
                
                // Check if this sprite is in our objectsToSpawn list
                foreach (var config in objectsToSpawn)
                {
                    if (sprite.name == config.spriteName)
                    {
                        objectSprites[sprite.name] = sprite;
                        foundCount++;
                        Debug.Log($"[ChunkGenerator] ✓ MATCHED: '{sprite.name}' from {assetPath}");
                    }
                }
            }
        }
        
        Debug.Log($"[ChunkGenerator] ===== LOADED {objectSprites.Count}/{objectsToSpawn.Length} OBJECT SPRITES (Matched: {foundCount}) =====");
        Debug.Log($"[ChunkGenerator] Loaded sprites in dictionary:");
        foreach (var entry in objectSprites)
        {
            Debug.Log($"[ChunkGenerator]   - '{entry.Key}' = {entry.Value}");
        }
        #endif
        
        // Update Inspector field for easy viewing
        availableSprites = string.Join("\n", allSpriteNames);
        
        // Check for missing sprites
        foreach (var config in objectsToSpawn)
        {
            if (config.prefab == null && !objectSprites.ContainsKey(config.spriteName))
            {
                Debug.LogError($"[ChunkGenerator] ✗ Object sprite '{config.spriteName}' NOT FOUND!");
                Debug.LogError($"[ChunkGenerator] Available sprites: {allSpriteNames.Count}");
                foreach (var name in allSpriteNames)
                {
                    Debug.LogError($"[ChunkGenerator]   - '{name}'");
                }
            }
        }
    }
    
    void LoadGroundSprites()
    {
        Debug.Log($"[ChunkGenerator] LoadGroundSprites() - Inspector has {groundSprites?.Length ?? 0} ground sprites configured.");
        if (groundSprites == null || groundSprites.Length == 0)
        {
            Debug.LogError("[ChunkGenerator] No ground sprites assigned in the Inspector! Chunk generation will fail!");
        }
        else
        {
            Debug.Log($"[ChunkGenerator] Ground sprite names: {string.Join(", ", System.Array.ConvertAll(groundSprites, s => s.name))}");
        }
    }
    
    public ChunkData GenerateChunk(int chunkX, int chunkY)
    {
        Debug.Log($"[ChunkGenerator] GenerateChunk({chunkX}, {chunkY}) called");
        
        if (groundSprites == null || groundSprites.Length == 0)
        {
            Debug.LogError("[ChunkGenerator] Cannot generate chunk - no ground sprites loaded!");
            return null;
        }
        
        ChunkData chunkData = new ChunkData(chunkX, chunkY);
        int totalObjectsSpawned = 0;
        
        // Thiết lập seed cố định dựa trên vị trí chunk và seed chung
        int seed = (chunkX * 1812433253) ^ (chunkY * 713429621) ^ GetSeed();
        Random.InitState(seed);
        
        Debug.Log($"[ChunkGenerator] Y-Sorting enabled: {useYSorting}");
        Debug.Log($"[ChunkGenerator] Object types to spawn: {objectsToSpawn.Length}");
        
        // 1. Sinh các ô đất nền như cũ
        for (int x = 0; x < tilesPerChunk; x++)
        {
            for (int y = 0; y < tilesPerChunk; y++)
            {
                int worldX = chunkX * tilesPerChunk + x;
                int worldY = chunkY * tilesPerChunk + y;
                
                int randomIndex = Random.Range(0, groundSprites.Length);
                string spriteName = groundSprites[randomIndex].name;
                chunkData.AddTile(spriteName, worldX, worldY);
            }
        }

        // 2. Sử dụng thuật toán Poisson Disc Sampling để tìm các tọa độ sinh cây/vật thể tối ưu
        // Bán kính tối thiểu giữa các vật thể là 2.2 units để tránh chồng lấn
        float minDistance = 2.2f;
        List<Vector2> samplePoints = GeneratePoissonPoints(minDistance, tilesPerChunk, tilesPerChunk);
        Debug.Log($"[ChunkGenerator] GeneratePoissonPoints returned {samplePoints.Count} points for chunk ({chunkX}, {chunkY})");

        // 3. Với mỗi tọa độ mẫu, chạy qua bộ lọc Perlin Noise / Random để quyết định loại vật thể spawn
        foreach (Vector2 point in samplePoints)
        {
            float worldX = chunkX * tilesPerChunk + point.x;
            float worldY = chunkY * tilesPerChunk + point.y;

            foreach (var config in objectsToSpawn)
            {
                if (config.prefab == null && !objectSprites.ContainsKey(config.spriteName))
                {
                    Debug.LogWarning($"[ChunkGenerator] Skipping config '{config.spriteName}' because sprite is not found and prefab is null");
                    continue;
                }

                bool shouldSpawn = false;

                if (config.useNoiseGeneration)
                {
                    // Dùng Perlin Noise
                    float noiseValue = GetNoise(worldX, worldY, config.noiseScale);
                    shouldSpawn = noiseValue > config.noiseThreshold;
                    // Log ngẫu nhiên một vài lần để tránh ngập Console
                    if (Random.value < 0.05f)
                    {
                        Debug.Log($"[ChunkGenerator] Noise check for {config.spriteName} at ({worldX:F1}, {worldY:F1}): value={noiseValue:F3}, threshold={config.noiseThreshold}, result={shouldSpawn}");
                    }
                }
                else
                {
                    // Random thuần
                    shouldSpawn = Random.value < config.spawnChance;
                }

                if (shouldSpawn)
                {
                    chunkData.AddObject(config.spriteName, config.spriteName, worldX, worldY);
                    totalObjectsSpawned++;
                    Debug.Log($"[ChunkGenerator] ✓ Added Object: {config.spriteName} at world position ({worldX:F1}, {worldY:F1})");
                    break; // Chỉ spawn 1 object tại một điểm mẫu
                }
            }
        }
        
        Debug.Log($"[ChunkGenerator] Generated chunk at ({chunkX}, {chunkY}) with {tilesPerChunk * tilesPerChunk} tiles and {totalObjectsSpawned} objects using Poisson Disc Sampling");
        return chunkData;
    }

    /// <summary>
    /// Thuật toán Poisson Disc Sampling để tạo ra phân bố phân tán tự nhiên
    /// </summary>
    private List<Vector2> GeneratePoissonPoints(float r, int width, int height, int k = 30)
    {
        List<Vector2> points = new List<Vector2>();
        List<Vector2> activeList = new List<Vector2>();

        float cellSize = r / Mathf.Sqrt(2);
        int gridWidth = Mathf.CeilToInt(width / cellSize);
        int gridHeight = Mathf.CeilToInt(height / cellSize);
        int[,] grid = new int[gridWidth, gridHeight];

        // Khởi tạo grid trống (-1)
        for (int i = 0; i < gridWidth; i++)
            for (int j = 0; j < gridHeight; j++)
                grid[i, j] = -1;

        // Chọn điểm bắt đầu ngẫu nhiên
        Vector2 firstPoint = new Vector2(Random.Range(0f, width), Random.Range(0f, height));
        points.Add(firstPoint);
        activeList.Add(firstPoint);
        int cX = Mathf.FloorToInt(firstPoint.x / cellSize);
        int cY = Mathf.FloorToInt(firstPoint.y / cellSize);
        grid[cX, cY] = points.Count - 1;

        while (activeList.Count > 0)
        {
            int activeIndex = Random.Range(0, activeList.Count);
            Vector2 point = activeList[activeIndex];
            bool found = false;

            for (int i = 0; i < k; i++)
            {
                float angle = Random.value * Mathf.PI * 2;
                float radius = Random.Range(r, 2 * r);
                Vector2 candidate = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                if (candidate.x >= 0 && candidate.x < width && candidate.y >= 0 && candidate.y < height)
                {
                    int cellX = Mathf.FloorToInt(candidate.x / cellSize);
                    int cellY = Mathf.FloorToInt(candidate.y / cellSize);

                    // Kiểm tra các ô xung quanh xem có bị quá gần điểm nào khác không
                    bool ok = true;
                    int minX = Mathf.Max(0, cellX - 2);
                    int maxX = Mathf.Min(gridWidth - 1, cellX + 2);
                    int minY = Mathf.Max(0, cellY - 2);
                    int maxY = Mathf.Min(gridHeight - 1, cellY + 2);

                    for (int gx = minX; gx <= maxX && ok; gx++)
                    {
                        for (int gy = minY; gy <= maxY && ok; gy++)
                        {
                            int index = grid[gx, gy];
                            if (index != -1)
                                if (Vector2.Distance(candidate, points[index]) < r)
                                    ok = false;
                        }
                    }

                    if (ok)
                    {
                        points.Add(candidate);
                        activeList.Add(candidate);
                        grid[cellX, cellY] = points.Count - 1;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                activeList.RemoveAt(activeIndex);
            }
        }

        return points;
    }
    
    /// <summary>
    /// Lấy giá trị Perlin Noise cho vị trí (tạo vùng clustered)
    /// </summary>
    float GetNoise(float worldX, float worldY, float scale)
    {
        int seed = GetSeed();
        float offsetX = seed;
        float offsetY = seed * 2;
        
        float noiseX = (worldX + offsetX) * scale;
        float noiseY = (worldY + offsetY) * scale;
        
        return Mathf.PerlinNoise(noiseX, noiseY);
    }
    
    public Sprite GetGroundSprite(string spriteName)
    {
        if (groundSprites == null || groundSprites.Length == 0)
        {
            Debug.LogError("[ChunkGenerator] GetGroundSprite - No sprites loaded!");
            return null;
        }
        
        foreach (Sprite sprite in groundSprites)
        {
            if (sprite.name == spriteName)
            {
                return sprite;
            }
        }
        
        Debug.LogWarning($"[ChunkGenerator] GetGroundSprite - Sprite '{spriteName}' not found!");
        return null;
    }
    
    public Sprite GetTreeSprite(string spriteName)
    {
        if (objectSprites.ContainsKey(spriteName))
        {
            return objectSprites[spriteName];
        }
        
        Debug.LogWarning($"[ChunkGenerator] GetTreeSprite - Object sprite '{spriteName}' not found!");
        return null;
    }
}
