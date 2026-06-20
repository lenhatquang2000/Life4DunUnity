using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class MiraController : NetworkBehaviour
{
    public float moveSpeed = 3f;

    [Header("Player Attributes")]
    public int level = 1;
    public int attack = 10;
    public int defense = 5;
    public int health = 100;
    public int maxHealth = 100;
    
    [Header("Y-Sorting Settings")]
    public bool useYSorting = true; // Bật Y-sorting cho player
    public int ySortingMultiplier = 1; // Nhân với Y (phải match với ChunkGenerator)
    
    private Animator animator;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private int lastSortingOrder = -999; // Cache sorting order
    private float lastCachedY = -9999f; // Cache Y position to avoid frequent updates
    
    private string currentAction = "Breathing_Idle";
    private string currentDirection = "south";
    private string lastAnimation = "";
    
    private ChunkManager chunkManager;
    private Vector2 lastChunkPosition;
    private Vector3 lastPosition;
    private System.Collections.Generic.List<Key> inputHistory = new System.Collections.Generic.List<Key>();

    void Start()
    {
        Debug.Log("[MiraController] Start() called");
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic; // Kinematic cho top-down (không bị gravity)
            rb.gravityScale = 0;
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints2D.FreezeAll; // Khóa tất cả hướng di chuyển
            Debug.Log("[MiraController] Rigidbody2D created - Type: Kinematic, Gravity: 0, Constraints: FreezeAll");
        }
        else
        {
            Debug.Log($"[MiraController] Rigidbody2D found - Gravity: {rb.gravityScale}, Body Type: {rb.bodyType}");
        }
        
        // Debug player collider
        var playerCol = GetComponent<Collider2D>();
        Debug.Log($"[MiraController] Player has collider: {playerCol != null}, Type: {(playerCol != null ? playerCol.GetType().Name : "None")}");
        
        // Setup Sorting Group để cố định sorting order cho cả animation
        var sortingGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sortingGroup == null)
        {
            sortingGroup = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            Debug.Log("[MiraController] Added SortingGroup component");
        }
        
        // Initialize Y position cache và set initial sorting
        lastCachedY = transform.position.y;
        lastPosition = transform.position;
        
        // Set player initial sorting order thông qua Sorting Group
        if (useYSorting)
        {
            int offset = 15000;
            // Công thức: sorting = offset - Y * 100 (độ chính xác thập phân)
            int initialSorting = offset - Mathf.FloorToInt(transform.position.y * 100f);
            sortingGroup.sortingOrder = initialSorting;
            lastSortingOrder = initialSorting;
            Debug.Log($"[MiraController] Y-Sorting enabled - Initial Y: {transform.position.y:F2}, Initial sorting: {initialSorting}");
        }
        else
        {
            sortingGroup.sortingOrder = 1; // Fixed sorting
            Debug.Log("[MiraController] Player sorting order set to 1 (fixed)");
        }
        
        // Auto-create ChunkManager if not exists
        chunkManager = ChunkManager.Instance;
        if (chunkManager == null)
        {
            Debug.LogWarning("[MiraController] ChunkManager.Instance is NULL! Creating new ChunkManager...");
            GameObject chunkManagerObj = new GameObject("ChunkManager");
            chunkManager = chunkManagerObj.AddComponent<ChunkManager>();
            Debug.Log("[MiraController] ChunkManager created successfully");
        }
        else
        {
            Debug.Log("[MiraController] ChunkManager.Instance found");
        }
        
        Debug.Log($"[MiraController] Starting position: {transform.position}");
        
        // Wait a frame to initialize chunk rendering
        if (IsOwner)
        {
            StartCoroutine(InitializeChunksAfterDelay());
            
            // Thiết lập camera để theo dõi đúng Local Player của máy này
            CameraFollower follower = Object.FindAnyObjectByType<CameraFollower>();
            if (follower != null)
            {
                follower.playerTransform = transform;
                Debug.Log("[MiraController] CameraFollower target set to Local Player");
            }
            
            CameraFollow follow = Object.FindAnyObjectByType<CameraFollow>();
            if (follow != null)
            {
                follow.target = transform;
                Debug.Log("[MiraController] CameraFollow target set to Local Player");
            }

            // Load player attributes and model
            if (AILife.Auth.PlayerManager.Instance != null)
            {
                AILife.Auth.PlayerManager.Instance.OnPlayerSelected += OnPlayerSelected;
                if (AILife.Auth.PlayerManager.Instance.CurrentPlayer != null)
                {
                    ApplyAttributes(AILife.Auth.PlayerManager.Instance.CurrentPlayer);
                }
                else
                {
                    string savedPlayerId = PlayerPrefs.GetString("SelectedPlayerId", "");
                    if (!string.IsNullOrEmpty(savedPlayerId))
                    {
                        AILife.Auth.PlayerManager.Instance.GetPlayerDetails(savedPlayerId);
                    }
                }
            }
        }
    }

    public override void OnDestroy()
    {
        if (IsOwner && AILife.Auth.PlayerManager.Instance != null)
        {
            AILife.Auth.PlayerManager.Instance.OnPlayerSelected -= OnPlayerSelected;
        }
        base.OnDestroy();
    }
    
    System.Collections.IEnumerator InitializeChunksAfterDelay()
    {
        Debug.Log("[MiraController] Waiting for ChunkRenderer to initialize...");
        yield return new WaitForEndOfFrame();
        
        // Auto-create ChunkRenderer if not exists
        if (ChunkRenderer.Instance == null)
        {
            Debug.LogWarning("[MiraController] ChunkRenderer.Instance is NULL! Creating new ChunkRenderer...");
            GameObject chunkRendererObj = new GameObject("ChunkRenderer");
            chunkRendererObj.AddComponent<ChunkRenderer>();
            Debug.Log("[MiraController] ChunkRenderer created successfully");
        }
        
        if (ChunkRenderer.Instance != null)
        {
            ChunkRenderer.Instance.UpdateChunksAround(transform.position);
        }
        lastChunkPosition = GetCurrentChunkPosition();
        Debug.Log($"[MiraController] Initial chunk position: {lastChunkPosition}");
    }

    void Update()
    {
        if (IsOwner)
        {
            float moveX = 0f;
            float moveY = 0f;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // Thêm phím vào lịch sử nếu vừa được nhấn trong frame này
                if (keyboard.wKey.wasPressedThisFrame && !inputHistory.Contains(Key.W)) inputHistory.Add(Key.W);
                if (keyboard.sKey.wasPressedThisFrame && !inputHistory.Contains(Key.S)) inputHistory.Add(Key.S);
                if (keyboard.aKey.wasPressedThisFrame && !inputHistory.Contains(Key.A)) inputHistory.Add(Key.A);
                if (keyboard.dKey.wasPressedThisFrame && !inputHistory.Contains(Key.D)) inputHistory.Add(Key.D);

                // Loại bỏ phím khỏi lịch sử nếu phím đó đã được thả ra
                if (!keyboard.wKey.isPressed) inputHistory.Remove(Key.W);
                if (!keyboard.sKey.isPressed) inputHistory.Remove(Key.S);
                if (!keyboard.aKey.isPressed) inputHistory.Remove(Key.A);
                if (!keyboard.dKey.isPressed) inputHistory.Remove(Key.D);

                // Ưu tiên phím được nhấn sau cùng trong danh sách phím đang được giữ
                if (inputHistory.Count > 0)
                {
                    Key activeKey = inputHistory[inputHistory.Count - 1];
                    switch (activeKey)
                    {
                        case Key.W: moveY = 1f; break;
                        case Key.S: moveY = -1f; break;
                        case Key.A: moveX = -1f; break;
                        case Key.D: moveX = 1f; break;
                    }
                }

                // Phát hiện nhấn phím F để chặt cây
                if (keyboard.fKey.wasPressedThisFrame)
                {
                    TryChopTree();
                }
            }

            Vector2 moveInput = new Vector2(moveX, moveY);
            bool isMoving = moveInput.magnitude > 0.1f;

            if (isMoving)
            {
                moveInput.Normalize();
                currentAction = "Walking";
                currentDirection = GetDirectionString(moveInput);
                
                transform.Translate(moveInput * moveSpeed * Time.deltaTime);
            }
            else
            {
                currentAction = "Breathing_Idle";
            }
            
            CheckChunkChange();
        }
        else
        {
            // For remote players, calculate action/direction based on position changes
            Vector3 currentPos = transform.position;
            Vector3 delta = currentPos - lastPosition;
            bool isMoving = delta.magnitude > (moveSpeed * Time.deltaTime * 0.05f) || delta.magnitude > 0.005f;

            if (isMoving)
            {
                currentAction = "Walking";
                currentDirection = GetDirectionString(new Vector2(delta.x, delta.y));
            }
            else
            {
                currentAction = "Breathing_Idle";
            }
            
            lastPosition = currentPos;
        }

        // Run visual updates for all players locally
        UpdatePlayerSorting();
        UpdateAnimation();
    }

    void UpdatePlayerSorting()
    {
        var sortingGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sortingGroup != null && useYSorting)
        {
            float playerY = transform.position.y;
            
            // Kiểm tra Y thay đổi, nhưng với threshold nhỏ (0.01) để update thường xuyên
            if (Mathf.Abs(playerY - lastCachedY) > 0.01f)
            {
                int offset = 15000;
                // Công thức: sorting = offset - Y * 100 (độ chính xác thập phân)
                int newSorting = offset - Mathf.FloorToInt(playerY * 100f);
                
                if (newSorting != lastSortingOrder)
                {
                    sortingGroup.sortingOrder = newSorting;
                    lastSortingOrder = newSorting;
                    Debug.Log($"[MiraController] UPDATED - Player Y: {playerY:F2}, FloorY: {Mathf.FloorToInt(playerY)}, Sorting: {newSorting}");
                }
                
                lastCachedY = playerY;
            }
        }
    }

    string GetDirectionString(Vector2 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        if (angle > -45f && angle <= 45f) return "east";
        if (angle > 45f && angle <= 135f) return "north";
        if (angle > 135f || angle <= -135f) return "west";
        if (angle > -135f && angle <= -45f) return "south";

        return "south";
    }

    void UpdateAnimation()
    {
        string animName = $"{currentAction}_{currentDirection}";
        
        if (animName != lastAnimation)
        {
            animator.Play(animName);
            lastAnimation = animName;
        }
    }
    
    Vector2 GetCurrentChunkPosition()
    {
        if (chunkManager == null) return Vector2.zero;
        
        int chunkX = Mathf.FloorToInt(transform.position.x / chunkManager.chunkSize);
        int chunkY = Mathf.FloorToInt(transform.position.y / chunkManager.chunkSize);
        
        return new Vector2(chunkX, chunkY);
    }
    
    void CheckChunkChange()
    {
        if (chunkManager == null) return;
        
        Vector2 currentChunkPos = GetCurrentChunkPosition();
        
        if (currentChunkPos != lastChunkPosition)
        {
            if (ChunkRenderer.Instance != null)
            {
                ChunkRenderer.Instance.UpdateChunksAround(transform.position);
            }
            lastChunkPosition = currentChunkPos;
        }
    }

    private void OnPlayerSelected(AILife.Auth.PlayerData player)
    {
        if (player != null)
        {
            ApplyAttributes(player);
        }
    }

    private void ApplyAttributes(AILife.Auth.PlayerData player)
    {
        if (player.attributes != null)
        {
            this.level = player.attributes.level;
            this.attack = player.attributes.attack;
            this.defense = player.attributes.defense;
            this.health = player.attributes.health;
            this.maxHealth = player.attributes.maxHealth;
            
            // Tốc độ chạy: Speed 35 -> 3.5f (Speed * 0.1f)
            this.moveSpeed = player.attributes.speed * 0.1f;
            if (this.moveSpeed <= 0f) this.moveSpeed = 3f;

            Debug.Log($"[MiraController] Applied character attributes: Speed={this.moveSpeed}, Level={this.level}, Attack={this.attack}, Defense={this.defense}, HP={this.health}/{this.maxHealth}");
        }

        // Load đúng model (kích hoạt Child GameObject tương ứng với tên model)
        string modelName = string.IsNullOrEmpty(player.model) ? "Mira" : player.model;
        Transform modelTransform = transform.Find(modelName);
        if (modelTransform != null)
        {
            // Deactivate all child models first
            foreach (Transform child in transform)
            {
                if (child.name == "Mira" || child.name == "Archer" || child.name == "Warrior") // example names
                {
                    child.gameObject.SetActive(false);
                }
            }
            // Activate the selected model
            modelTransform.gameObject.SetActive(true);
            
            // Re-assign components from the active model child if necessary
            var childAnimator = modelTransform.GetComponent<Animator>();
            if (childAnimator != null) animator = childAnimator;
            
            var childSpriteRenderer = modelTransform.GetComponent<SpriteRenderer>();
            if (childSpriteRenderer != null) spriteRenderer = childSpriteRenderer;
        }
    }

    private void OnGUI()
    {
        if (!IsOwner) return;

        AILife.Auth.PlayerData player = AILife.Auth.PlayerManager.Instance != null ? AILife.Auth.PlayerManager.Instance.CurrentPlayer : null;
        if (player == null) return;

        // Vẽ HUD ở góc dưới bên trái màn hình
        GUILayout.BeginArea(new Rect(15, Screen.height - 110, 250, 95));
        GUILayout.BeginVertical("box");
        GUILayout.Label($"<b>Nhân Vật:</b> {player.username} (Lvl {player.attributes?.level ?? 1})");
        
        int currentHp = player.attributes?.health ?? 100;
        int maxHp = player.attributes?.maxHealth ?? 100;
        GUILayout.Label($"<b>HP:</b> {currentHp} / {maxHp}");
        
        // Progress bar cho HP
        float fillPercent = maxHp > 0 ? (float)currentHp / maxHp : 0f;
        Rect progressRect = GUILayoutUtility.GetRect(180, 16);
        GUI.Box(progressRect, "");
        GUI.color = Color.red;
        GUI.Box(new Rect(progressRect.x, progressRect.y, progressRect.width * fillPercent, progressRect.height), "");
        GUI.color = Color.white;
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void TryChopTree()
    {
        float chopRadius = 1.8f; // Tăng nhẹ bán kính quét lên 1.8 units cho dễ trúng
        Debug.Log($"[TryChopTree] Đã nhấn phím F! Vị trí người chơi: {transform.position}");
        
        // Quét tất cả các Collider 2D xung quanh người chơi
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, chopRadius);
        Debug.Log($"[TryChopTree] Quét thấy {hitColliders.Length} vật thể có Collider trong bán kính {chopRadius}");
        
        foreach (var hitCollider in hitColliders)
        {
            Debug.Log($"[TryChopTree] -> Đang kiểm tra vật thể: {hitCollider.gameObject.name} (Collider: {hitCollider.name})");

            // Bỏ qua nếu collider thuộc về chính người chơi hoặc người chơi khác
            if (hitCollider.GetComponentInParent<MiraController>() != null)
            {
                Debug.Log($"[TryChopTree]   -> Bỏ qua vì đây là Người chơi (Player)");
                continue;
            }
            
            // Tìm component Animator của cây ở đối tượng va chạm hoặc cha/con của nó
            Animator treeAnim = hitCollider.GetComponent<Animator>();
            if (treeAnim == null) treeAnim = hitCollider.GetComponentInParent<Animator>();
            if (treeAnim == null) treeAnim = hitCollider.GetComponentInChildren<Animator>();

            if (treeAnim != null)
            {
                Debug.Log($"[TryChopTree] Đang kiểm tra Animator của: {treeAnim.gameObject.name}");
                
                // 1. In ra danh sách toàn bộ Parameter của Animator để kiểm tra có chữ "Chop" chưa
                string paramList = "";
                bool hasChopTrigger = false;
                foreach (var param in treeAnim.parameters)
                {
                    paramList += $"{param.name} ({param.type}), ";
                    if (param.name == "Chop") hasChopTrigger = true;
                }
                Debug.Log($"[TryChopTree] Danh sách Parameters của cây: [ {paramList} ]");
                Debug.Log($"[TryChopTree] Animator có chứa Trigger 'Chop' viết hoa không? {hasChopTrigger}");

                // 2. In ra trạng thái (State) hiện tại của Animator
                AnimatorStateInfo stateInfo = treeAnim.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"[TryChopTree] Trạng thái Animator hiện tại: (ShortNameHash: {stateInfo.shortNameHash})");

                // 3. Kích hoạt trigger "Chop"
                treeAnim.SetTrigger("Chop");
                Debug.Log($"[TryChopTree] SUCCESS! Đã gửi lệnh Trigger 'Chop' tới Animator của: {treeAnim.gameObject.name}");
                
                // Chỉ chặt 1 cây gần nhất mỗi lần nhấn phím F
                break;
            }
            else
            {
                Debug.Log($"[TryChopTree]   -> Thất bại: Không tìm thấy component Animator trên {hitCollider.gameObject.name}");
            }
        }
    }
}
