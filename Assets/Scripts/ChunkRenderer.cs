using UnityEngine;
using System.Collections.Generic;

public class ChunkRenderer : MonoBehaviour
{
    public static ChunkRenderer Instance;

    private Dictionary<string, List<GameObject>> renderedChunks = new Dictionary<string, List<GameObject>>();
    private ChunkManager chunkManager;
    private ChunkGenerator chunkGenerator;
    private Sprite shadowSprite;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[ChunkRenderer] Instance created and set");
        }
        else
        {
            Debug.LogWarning("[ChunkRenderer] Duplicate instance destroyed");
            Destroy(gameObject);
        }
    }

    private Material unlitMaterial;

    private float cullingTimer = 0f;
    public float cullingInterval = 0.2f; // Check 5 times per second

    void Start()
    {
        chunkManager = ChunkManager.Instance;
        chunkGenerator = ChunkGenerator.Instance;
        
        // Tạo material Unlit mặc định để hiển thị luôn sáng
        Shader unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader != null)
        {
            unlitMaterial = new Material(unlitShader);
        }

        // Tạo bóng mờ dạng oval mềm mại cho cây
        shadowSprite = CreateShadowSprite();
    }

    void Update()
    {
        cullingTimer += Time.deltaTime;
        if (cullingTimer >= cullingInterval)
        {
            cullingTimer = 0f;
            MiraController localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                CullPlayers(localPlayer.transform.position);
            }
        }
    }

    private MiraController GetLocalPlayer()
    {
        MiraController[] players = FindObjectsByType<MiraController>();
        foreach (var p in players)
        {
            if (p.IsOwner) return p;
        }
        return null;
    }

    public void CullPlayers(Vector2 localPlayerPosition)
    {
        if (chunkManager == null) return;
        
        int playerChunkX = Mathf.FloorToInt(localPlayerPosition.x / chunkManager.chunkSize);
        int playerChunkY = Mathf.FloorToInt(localPlayerPosition.y / chunkManager.chunkSize);
        
        MiraController[] allPlayers = FindObjectsByType<MiraController>();
        
        foreach (var player in allPlayers)
        {
            if (player.IsOwner)
            {
                TogglePlayerVisuals(player, true);
                continue;
            }
            
            int otherChunkX = Mathf.FloorToInt(player.transform.position.x / chunkManager.chunkSize);
            int otherChunkY = Mathf.FloorToInt(player.transform.position.y / chunkManager.chunkSize);
            
            bool isInside3x3 = Mathf.Abs(otherChunkX - playerChunkX) <= 1 && Mathf.Abs(otherChunkY - playerChunkY) <= 1;
            TogglePlayerVisuals(player, isInside3x3);
        }
    }

    private void TogglePlayerVisuals(MiraController player, bool visible)
    {
        // Toggle SpriteRenderer
        var sr = player.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = visible;
        
        // Toggle Animator
        var anim = player.GetComponent<Animator>();
        if (anim != null) anim.enabled = visible;
        
        // Toggle SortingGroup
        var sg = player.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg != null) sg.enabled = visible;
        
        // Toggle Collider2D so player cannot collide with hidden players
        var col = player.GetComponent<Collider2D>();
        if (col != null) col.enabled = visible;
    }

    public void UpdateChunksAround(Vector2 playerPosition)
    {
        if (chunkManager == null) chunkManager = ChunkManager.Instance;
        if (chunkGenerator == null) chunkGenerator = ChunkGenerator.Instance;

        if (chunkManager == null || chunkGenerator == null)
        {
            Debug.LogError("[ChunkRenderer] Cannot update chunks - ChunkManager or ChunkGenerator is null!");
            return;
        }

        int playerChunkX = Mathf.FloorToInt(playerPosition.x / chunkManager.chunkSize);
        int playerChunkY = Mathf.FloorToInt(playerPosition.y / chunkManager.chunkSize);

        HashSet<string> chunksToKeep = new HashSet<string>();

        // Load and render chunks in 3x3 grid around player
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                int chunkX = playerChunkX + x;
                int chunkY = playerChunkY + y;
                string chunkKey = chunkManager.GetChunkKey(chunkX, chunkY);
                chunksToKeep.Add(chunkKey);

                if (!renderedChunks.ContainsKey(chunkKey))
                {
                    LoadAndRenderChunk(chunkX, chunkY, chunkKey);
                }
            }
        }

        // Identify and unload chunks that are out of range
        List<string> chunksToRemove = new List<string>();
        foreach (var key in renderedChunks.Keys)
        {
            if (!chunksToKeep.Contains(key))
            {
                chunksToRemove.Add(key);
            }
        }

        foreach (var key in chunksToRemove)
        {
            UnrenderChunk(key);
        }

        // Apply player culling when chunks update
        CullPlayers(playerPosition);
    }

    void LoadAndRenderChunk(int chunkX, int chunkY, string chunkKey)
    {
        Debug.Log($"[ChunkRenderer] LoadAndRenderChunk({chunkX}, {chunkY})");
        ChunkData chunkData = chunkManager.LoadChunk(chunkX, chunkY);
        
        if (chunkData == null)
        {
            Debug.Log($"[ChunkRenderer] Chunk ({chunkX}, {chunkY}) not found, generating new chunk");
            chunkData = chunkGenerator.GenerateChunk(chunkX, chunkY);
            if (chunkData == null)
            {
                Debug.LogError($"[ChunkRenderer] Failed to generate chunk ({chunkX}, {chunkY})!");
                return;
            }
            chunkManager.SaveChunk(chunkData);
        }
        else
        {
            Debug.Log($"[ChunkRenderer] Chunk ({chunkX}, {chunkY}) loaded from disk/cache");
        }

        RenderChunk(chunkData, chunkKey);
    }

    void RenderChunk(ChunkData chunkData, string chunkKey)
    {
        if (chunkData.objects == null)
        {
            chunkData.objects = new TileObject[0];
        }

        Debug.Log($"[ChunkRenderer] RenderChunk - Rendering {chunkData.tileSpriteNames.Length} tiles and {chunkData.objects.Length} objects for chunk {chunkKey}");

        List<GameObject> chunkObjects = new List<GameObject>();
        float tileScale = 100f / chunkGenerator.pixelsPerUnit; // pixelsPerUnit is 32f

        // Render ground tiles
        for (int i = 0; i < chunkData.tileSpriteNames.Length; i++)
        {
            string spriteName = chunkData.tileSpriteNames[i];
            int tileX = chunkData.tileX[i];
            int tileY = chunkData.tileY[i];
            
            Sprite sprite = chunkGenerator.GetGroundSprite(spriteName);
            if (sprite != null)
            {
                GameObject tile = new GameObject($"Tile_{tileX}_{tileY}");
                tile.transform.position = new Vector3(tileX, tileY, 0);
                tile.transform.localScale = new Vector3(tileScale, tileScale, 1f);
                tile.transform.SetParent(this.transform);
                
                SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                if (unlitMaterial != null)
                {
                    renderer.material = unlitMaterial;
                }
                renderer.sortingLayerName = "Ground"; // Đưa vào Sorting Layer Ground riêng biệt
                renderer.sortingOrder = 0;            // Mặc định ở layer Ground là 0
                
                chunkObjects.Add(tile);
            }
        }

        // Render objects (trees, grass, etc)
        Debug.Log($"[ChunkRenderer] RenderChunk - Starting rendering of {chunkData.objects.Length} objects");
        for (int i = 0; i < chunkData.objects.Length; i++)
        {
            TileObject obj = chunkData.objects[i];
            
            ChunkGenerator.ObjectSpawnConfig objConfig = null;
            foreach (var config in chunkGenerator.objectsToSpawn)
            {
                if (config.spriteName == obj.spriteName)
                {
                    objConfig = config;
                    break;
                }
            }

            Vector3 spawnPosition = new Vector3(obj.x, obj.y, 0);

            // Kiểm tra xem vị trí này có bị đè lên vật thể đặt trước (như House1) không
            // Quét một vòng tròn nhỏ bán kính 1.2 units tại vị trí spawn
            Collider2D hit = Physics2D.OverlapCircle(spawnPosition, 1.2f);
            if (hit != null && hit.transform.parent != this.transform && hit.GetComponentInParent<MiraController>() == null)
            {
                Debug.LogWarning($"[ChunkRenderer] Skipped rendering object '{obj.spriteName}' at ({obj.x:F1}, {obj.y:F1}) because it overlapped with collider: {hit.name} ({hit.gameObject.name})");
                continue;
            }

            if (objConfig == null)
            {
                Debug.LogWarning($"[ChunkRenderer] Skipped rendering object '{obj.spriteName}' at ({obj.x:F1}, {obj.y:F1}) because objConfig is null!");
            }

            // Trường hợp 1: Có Prefab
            if (objConfig != null && objConfig.prefab != null)
            {
                Debug.Log($"[ChunkRenderer] Instantiating Prefab: {objConfig.prefab.name} at {spawnPosition}");
                GameObject objGO = Instantiate(objConfig.prefab, spawnPosition, Quaternion.identity);
                objGO.name = $"{objConfig.prefab.name}_{obj.x}_{obj.y}";
                objGO.transform.SetParent(this.transform);

                // Nhân Scale của Prefab với scaleMultiplier cấu hình trong Inspector
                objGO.transform.localScale = objConfig.prefab.transform.localScale * objConfig.scaleMultiplier;

                // Thêm bóng cho Prefab nếu chưa có cấu hình bóng bên trong nó
                if (objConfig.addShadow && shadowSprite != null)
                {
                    if (objGO.transform.Find("Shadow") == null && objGO.transform.Find("shadow") == null)
                    {
                        GameObject shadowGO = new GameObject("Shadow");
                        shadowGO.transform.SetParent(objGO.transform);
                        shadowGO.transform.localPosition = new Vector3(0f, objConfig.shadowOffset, 0f);
                        shadowGO.transform.localScale = new Vector3(objConfig.shadowScale.x, objConfig.shadowScale.y, 1f);

                        SpriteRenderer shadowSR = shadowGO.AddComponent<SpriteRenderer>();
                        shadowSR.sprite = shadowSprite;
                        shadowSR.sortingOrder = -1; // Vẽ phía sau các bộ phận khác của cây
                        if (unlitMaterial != null)
                        {
                            shadowSR.material = unlitMaterial;
                        }
                    }
                }

                // Đảm bảo mỗi Prefab tự sinh có SortingGroup để gom nhóm tất cả SpriteRenderer con (Upper, Lower, Shadow...) lại và sort đồng bộ
                var sortingGroup = objGO.GetComponent<UnityEngine.Rendering.SortingGroup>();
                if (sortingGroup == null)
                {
                    sortingGroup = objGO.AddComponent<UnityEngine.Rendering.SortingGroup>();
                }
                sortingGroup.sortingOrder = CalculateSortingOrder(spawnPosition.y);

                chunkObjects.Add(objGO);
                continue;
            }

            // Trường hợp 2: Không có Prefab, tạo động
            Sprite sprite = chunkGenerator.GetTreeSprite(obj.spriteName);
            if (sprite != null)
            {
                GameObject objGO = new GameObject($"Tree_{obj.x}_{obj.y}");
                objGO.transform.position = spawnPosition;
                objGO.transform.SetParent(this.transform);

                float multiplier = objConfig != null ? objConfig.scaleMultiplier : 1f;
                float objectScale = tileScale * multiplier;
                if (obj.spriteName == "to_cho_ti_mt_s_assets_l (7)_0")
                {
                    objectScale = tileScale * 0.65f * multiplier;
                }
                objGO.transform.localScale = new Vector3(objectScale, objectScale, 1f);

                SpriteRenderer renderer = objGO.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                if (unlitMaterial != null)
                {
                    renderer.material = unlitMaterial;
                }
                int treeSortingOrder = CalculateSortingOrder(spawnPosition.y);
                renderer.sortingOrder = treeSortingOrder;

                // Thêm bóng cho Sprite tự tạo động
                if (objConfig != null && objConfig.addShadow && shadowSprite != null)
                {
                    GameObject shadowGO = new GameObject("Shadow");
                    shadowGO.transform.SetParent(objGO.transform);
                    shadowGO.transform.localPosition = new Vector3(0f, objConfig.shadowOffset, 0f);
                    shadowGO.transform.localScale = new Vector3(objConfig.shadowScale.x, objConfig.shadowScale.y, 1f);

                    SpriteRenderer shadowSR = shadowGO.AddComponent<SpriteRenderer>();
                    shadowSR.sprite = shadowSprite;
                    shadowSR.sortingOrder = treeSortingOrder - 1; // Vẽ ngay phía sau cây
                    if (unlitMaterial != null)
                    {
                        shadowSR.material = unlitMaterial;
                    }
                }

                if (objConfig != null && objConfig.addCollider)
                {
                    Rigidbody2D objRb = objGO.AddComponent<Rigidbody2D>();
                    objRb.bodyType = RigidbodyType2D.Static;
                    objRb.gravityScale = 0;
                    objRb.constraints = RigidbodyConstraints2D.FreezeAll;

                    BoxCollider2D collider = objGO.AddComponent<BoxCollider2D>();
                    collider.size = objConfig.colliderSize;
                    collider.offset = objConfig.colliderOffset;
                    collider.isTrigger = objConfig.isTrigger;
                }

                chunkObjects.Add(objGO);
            }
        }

        renderedChunks[chunkKey] = chunkObjects;
    }

    void UnrenderChunk(string chunkKey)
    {
        Debug.Log($"[ChunkRenderer] Unloading and destroying GameObjects for chunk: {chunkKey}");
        if (renderedChunks.ContainsKey(chunkKey))
        {
            foreach (var go in renderedChunks[chunkKey])
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }
            renderedChunks.Remove(chunkKey);
        }
    }

    private Sprite CreateShadowSprite()
    {
        int width = 64;
        int height = 32;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        
        Color transparent = new Color(0, 0, 0, 0);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                tex.SetPixel(x, y, transparent);
            }
        }
        
        float centerX = width / 2f;
        float centerY = height / 2f;
        float radiusX = width / 2f - 2f;
        float radiusY = height / 2f - 2f;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x - centerX) / radiusX;
                float dy = (y - centerY) / radiusY;
                float distSqr = dx * dx + dy * dy;
                
                if (distSqr <= 1f)
                {
                    // Tạo viền mềm mịn giảm dần từ tâm ra ngoài
                    float alpha = Mathf.Clamp01(1f - distSqr);
                    // Độ trong suốt tối đa là 40% (0.4f)
                    tex.SetPixel(x, y, new Color(0, 0, 0, alpha * 0.4f));
                }
            }
        }
        
        tex.Apply();
        // Pivot ở chính giữa bóng (0.5, 0.5)
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
    }

    int CalculateSortingOrder(float worldY)
    {
        if (!chunkGenerator || !chunkGenerator.useYSorting)
        {
            return 2;
        }
        int offset = 15000; // Thay đổi về 15000 để tránh tràn số 16-bit của Unity
        return offset - Mathf.FloorToInt(worldY * 100f); // Nhân 100 để lấy độ chính xác thập phân
    }
}
