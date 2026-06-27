using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Network Prefab Manager Tool
/// Mở từ menu: Tools > Network Prefab Manager
///
/// Cho phép thêm / xóa Prefab vào NetworkManager (NetworkPrefabsList)
/// mà không bị giới hạn chỉ 1 Prefab.
/// </summary>
public class NetworkPrefabManagerTool : EditorWindow
{
    // ─── Tham chiếu ────────────────────────────────────────────────────────────
    private NetworkManager     _networkManager;
    private NetworkPrefabsList _prefabsList;
    private CharacterRegistry  _characterRegistry;

    // ─── UI ────────────────────────────────────────────────────────────────────
    private GameObject   _newPrefab;
    private Vector2      _scrollPos;
    private List<string> _log = new List<string>();

    // ─── Mở cửa sổ ─────────────────────────────────────────────────────────────
    [MenuItem("Tools/Network Prefab Manager")]
    public static void ShowWindow()
    {
        var w = GetWindow<NetworkPrefabManagerTool>("Network Prefab Manager");
        w.minSize = new Vector2(500, 460);
        w.AutoDetect();
    }

    // ─── Tự động tìm NetworkManager trong scene ─────────────────────────────────
    private void AutoDetect()
    {
        _networkManager = FindObjectOfType<NetworkManager>();
        if (_networkManager != null)
            TryLoadPrefabsList();

        // Tự động tìm CharacterRegistry
        TryFindCharacterRegistry();
    }

    // ─── Tìm CharacterRegistry asset ────────────────────────────────────────────
    private void TryFindCharacterRegistry()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterRegistry");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _characterRegistry = AssetDatabase.LoadAssetAtPath<CharacterRegistry>(path);
        }
    }

    // ─── Nạp NetworkPrefabsList từ NetworkManager ───────────────────────────────
    private void TryLoadPrefabsList()
    {
        if (_networkManager == null) return;

        var config = _networkManager.NetworkConfig;
        if (config?.Prefabs?.NetworkPrefabsLists != null &&
            config.Prefabs.NetworkPrefabsLists.Count > 0)
        {
            _prefabsList = config.Prefabs.NetworkPrefabsLists[0];
        }
        else
        {
            _prefabsList = null;
        }
    }

    // ─── OnGUI ──────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        DrawHeader();
        EditorGUILayout.Space(8);
        DrawManagerSection();
        DrawRegistrySection();
        if (_prefabsList != null)
        {
            EditorGUILayout.Space(8);
            DrawPrefabListSection();
            EditorGUILayout.Space(8);
            DrawAddSection();
        }
        EditorGUILayout.Space(8);
        DrawLog();
    }

    // ─── Header ─────────────────────────────────────────────────────────────────
    void DrawHeader()
    {
        var title = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Network Prefab Manager", title, GUILayout.Height(30));
        EditorGUILayout.LabelField(
            "Them / Xoa Prefab vao NetworkManager (khong gioi han so luong)",
            EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(4);
        DrawLine();
    }

    // ─── Section chọn NetworkManager ────────────────────────────────────────────
    void DrawManagerSection()
    {
        EditorGUILayout.LabelField("NetworkManager trong Scene", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _networkManager = (NetworkManager)EditorGUILayout.ObjectField(
            "NetworkManager", _networkManager, typeof(NetworkManager), true);
        if (EditorGUI.EndChangeCheck())
            TryLoadPrefabsList();

        if (_networkManager == null)
        {
            EditorGUILayout.HelpBox(
                "Khong tim thay NetworkManager. Mo scene co NetworkManager roi thu lai.",
                MessageType.Warning);
            if (GUILayout.Button("Tu dong tim trong Scene", GUILayout.Height(28)))
                AutoDetect();
            return;
        }

        EditorGUI.BeginChangeCheck();
        _prefabsList = (NetworkPrefabsList)EditorGUILayout.ObjectField(
            "Network Prefabs List", _prefabsList, typeof(NetworkPrefabsList), false);
        if (EditorGUI.EndChangeCheck() && _prefabsList == null)
            TryLoadPrefabsList();

        if (_prefabsList == null)
        {
            EditorGUILayout.HelpBox(
                "NetworkManager chua co NetworkPrefabsList. Tim hoac tao asset moi.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tim DefaultNetworkPrefabs.asset", GUILayout.Height(28)))
                TryFindDefaultAsset();
            if (GUILayout.Button("Tao NetworkPrefabsList moi", GUILayout.Height(28)))
                CreateNewPrefabsList();
            EditorGUILayout.EndHorizontal();
        }
    }

    // ─── Tìm DefaultNetworkPrefabs.asset ─────────────────────────────────────────
    private void TryFindDefaultAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:NetworkPrefabsList");
        if (guids.Length == 0)
        {
            Log("[ERROR] Khong tim thay NetworkPrefabsList asset nao trong project.");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        _prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
        Log("[OK] Tim thay: " + path);
        AssignPrefabsListToManager();
    }

    // ─── Tạo NetworkPrefabsList mới ──────────────────────────────────────────────
    private void CreateNewPrefabsList()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Luu NetworkPrefabsList", "NetworkPrefabs", "asset",
            "Chon noi luu NetworkPrefabsList asset");
        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<NetworkPrefabsList>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        _prefabsList = asset;
        Log("[OK] Da tao: " + path);
        AssignPrefabsListToManager();
    }

    // ─── Gán prefabs list vào NetworkManager ─────────────────────────────────────
    private void AssignPrefabsListToManager()
    {
        if (_networkManager == null || _prefabsList == null) return;

        var lists = _networkManager.NetworkConfig?.Prefabs?.NetworkPrefabsLists;
        if (lists != null && !lists.Contains(_prefabsList))
        {
            lists.Add(_prefabsList);
            EditorUtility.SetDirty(_networkManager);
            Log("[OK] Da gan NetworkPrefabsList vao NetworkManager.");
        }
    }

    // ─── Section CharacterRegistry ────────────────────────────────────────────
    void DrawRegistrySection()
    {
        DrawLine();
        EditorGUILayout.LabelField("Character Registry (UI Chon Nhan Vat)", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _characterRegistry = (CharacterRegistry)EditorGUILayout.ObjectField(
            "Character Registry", _characterRegistry, typeof(CharacterRegistry), false);
        if (EditorGUI.EndChangeCheck() && _characterRegistry == null)
            TryFindCharacterRegistry();

        if (_characterRegistry == null)
        {
            EditorGUILayout.HelpBox(
                "Chua tim thay CharacterRegistry asset.\n" +
                "Tao moi: Assets > Create > AILife > Character Registry\n" +
                "Sau do gan vao AuthUI_IMGUI component.",
                MessageType.Info);

            if (GUILayout.Button("Tao CharacterRegistry.asset", GUILayout.Height(26)))
                CreateCharacterRegistry();
        }
        else
        {
            EditorGUILayout.LabelField($"  {_characterRegistry.characters.Count} characters trong registry",
                EditorStyles.miniLabel);

            GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);
            if (GUILayout.Button("Sync Prefabs List → CharacterRegistry", GUILayout.Height(26)))
                SyncToRegistry();
            GUI.backgroundColor = Color.white;
        }
    }

    // ─── Tạo CharacterRegistry asset ─────────────────────────────────────────────
    private void CreateCharacterRegistry()
    {
        // BẮT BUỘC lưu vào Resources/ để Runtime dùng Resources.Load được
        const string resourcesPath = "Assets/Resources";
        if (!System.IO.Directory.Exists(resourcesPath))
        {
            System.IO.Directory.CreateDirectory(resourcesPath);
            AssetDatabase.ImportAsset(resourcesPath);
        }

        const string assetPath = "Assets/Resources/CharacterRegistry.asset";
        var existing = AssetDatabase.LoadAssetAtPath<CharacterRegistry>(assetPath);
        if (existing != null)
        {
            _characterRegistry = existing;
            Log("[INFO] CharacterRegistry da ton tai tai: " + assetPath);
            return;
        }

        var asset = ScriptableObject.CreateInstance<CharacterRegistry>();
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        _characterRegistry = asset;
        Log("[OK] Da tao CharacterRegistry tai: " + assetPath);
    }

    // ─── Sync PrefabList → CharacterRegistry ─────────────────────────────────────
    private void SyncToRegistry()
    {
        if (_prefabsList == null || _characterRegistry == null) return;

        int added = 0, skipped = 0;
        foreach (var entry in _prefabsList.PrefabList)
        {
            var prefab = entry.Prefab;
            if (prefab == null) continue;

            if (_characterRegistry.Contains(prefab.name))
            {
                skipped++;
                continue;
            }

            _characterRegistry.characters.Add(new CharacterRegistry.CharacterEntry
            {
                modelName   = prefab.name,
                prefab      = prefab,
                displayName = prefab.name
            });
            added++;
            Log($"[OK] Registry: them {prefab.name}");
        }

        if (added > 0)
        {
            EditorUtility.SetDirty(_characterRegistry);
            AssetDatabase.SaveAssets();
            Log($"[OK] Sync xong: +{added} moi, {skipped} da co.");
        }
        else
        {
            Log($"[INFO] Tat ca {skipped} prefab da co trong registry.");
        }
    }

    // ─── Danh sách prefabs hiện tại ──────────────────────────────────────────────
    void DrawPrefabListSection()
    {
        DrawLine();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            $"Prefabs hien tai  ({_prefabsList.PrefabList.Count} items)",
            EditorStyles.boldLabel);
        if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            Repaint();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        if (_prefabsList.PrefabList.Count == 0)
        {
            EditorGUILayout.HelpBox("Danh sach trong. Them Prefab o phia duoi.", MessageType.Info);
            return;
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(160));

        int removeIndex = -1;

        for (int i = 0; i < _prefabsList.PrefabList.Count; i++)
        {
            var entry  = _prefabsList.PrefabList[i];
            var prefab = entry.Prefab;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Icon nhỏ
            Texture2D icon = prefab != null
                ? AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniTypeThumbnail(typeof(GameObject))
                : null;
            if (icon != null) GUILayout.Label(icon, GUILayout.Width(28), GUILayout.Height(28));
            else               GUILayout.Space(32);

            // Tên
            string label = prefab != null ? prefab.name : "(null)";
            EditorGUILayout.LabelField($"[{i}]  {label}", GUILayout.MinWidth(160));

            // Field readonly
            using (new EditorGUI.DisabledGroupScope(true))
                EditorGUILayout.ObjectField(prefab, typeof(GameObject), false, GUILayout.Width(160));

            // Nút Xóa
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("Xoa", GUILayout.Width(46))) removeIndex = i;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        if (removeIndex >= 0)
        {
            var removed = _prefabsList.PrefabList[removeIndex];
            string prefabName = removed.Prefab != null ? removed.Prefab.name : "null";
            _prefabsList.Remove(removed);
            EditorUtility.SetDirty(_prefabsList);
            AssetDatabase.SaveAssets();
            Log($"[OK] Da xoa prefab [{removeIndex}]: {prefabName}");
        }
    }

    // ─── Thêm prefab mới ──────────────────────────────────────────────────────────
    void DrawAddSection()
    {
        DrawLine();
        EditorGUILayout.LabelField("Them Prefab moi", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        _newPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Prefab", _newPrefab, typeof(GameObject), false);

        bool canAdd = _newPrefab != null;
        GUI.backgroundColor = canAdd ? new Color(0.4f, 0.9f, 0.5f) : Color.white;
        using (new EditorGUI.DisabledGroupScope(!canAdd))
        {
            if (GUILayout.Button("  Add  ", GUILayout.Width(64), GUILayout.Height(22)))
                AddPrefab(_newPrefab);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (_newPrefab != null)
        {
            bool hasNetObj    = _newPrefab.GetComponent<NetworkObject>() != null;
            bool alreadyExist = _prefabsList.PrefabList.Any(e => e.Prefab == _newPrefab);

            if (!hasNetObj)
                EditorGUILayout.HelpBox(
                    "Canh bao: Prefab nay KHONG co NetworkObject component!\n" +
                    "Them NetworkObject de NetworkManager co the spawn qua mang.",
                    MessageType.Warning);

            if (alreadyExist)
                EditorGUILayout.HelpBox("Prefab nay da co trong danh sach.", MessageType.Info);
        }

        EditorGUILayout.Space(6);

        // Scan tự động
        GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
        if (GUILayout.Button("Scan & Add tat ca Prefab trong  Assets/Character/Prefabs",
                             GUILayout.Height(30)))
            ScanAndAddAllCharacterPrefabs();
        GUI.backgroundColor = Color.white;
    }

    // ─── Thêm một prefab ──────────────────────────────────────────────────────────
    private void AddPrefab(GameObject go)
    {
        if (go == null || _prefabsList == null) return;

        if (_prefabsList.PrefabList.Any(e => e.Prefab == go))
        {
            Log($"[INFO] {go.name} da ton tai trong danh sach.");
            return;
        }

        _prefabsList.Add(new NetworkPrefab { Prefab = go });
        EditorUtility.SetDirty(_prefabsList);
        AssetDatabase.SaveAssets();
        Log($"[OK] Da them: {go.name}");
        _newPrefab = null;
    }

    // ─── Scan & Add tất cả prefab trong Assets/Character/Prefabs ─────────────────
    private void ScanAndAddAllCharacterPrefabs()
    {
        const string folder = "Assets/Character/Prefabs";
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });

        if (guids.Length == 0)
        {
            Log("[ERROR] Khong tim thay Prefab nao trong: " + folder);
            return;
        }

        int added = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var    go   = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            if (_prefabsList.PrefabList.Any(e => e.Prefab == go))
            {
                Log($"[INFO] Da co: {go.name}");
                continue;
            }

            if (go.GetComponent<NetworkObject>() == null)
            {
                Log($"[WARN] Bo qua {go.name}: thieu NetworkObject component.");
                continue;
            }

            _prefabsList.Add(new NetworkPrefab { Prefab = go });
            added++;
            Log($"[OK] Da them: {go.name}");
        }

        if (added > 0)
        {
            EditorUtility.SetDirty(_prefabsList);
            AssetDatabase.SaveAssets();
            Log($"[OK] Them thanh cong {added} prefab(s).");
        }
        else
        {
            Log("[INFO] Khong co prefab moi nao duoc them.");
        }
    }

    // ─── Log ─────────────────────────────────────────────────────────────────────
    void DrawLog()
    {
        if (_log.Count == 0) return;

        DrawLine();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
        if (GUILayout.Button("Xoa", GUILayout.Width(50)))
            _log.Clear();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Height(90));
        foreach (var msg in _log)
        {
            Color prev = GUI.color;
            if      (msg.StartsWith("[ERROR]")) GUI.color = new Color(1f, 0.4f, 0.4f);
            else if (msg.StartsWith("[OK]"))    GUI.color = new Color(0.4f, 1f, 0.5f);
            else if (msg.StartsWith("[WARN]"))  GUI.color = new Color(1f, 0.85f, 0.3f);
            EditorGUILayout.LabelField(msg, EditorStyles.miniLabel);
            GUI.color = prev;
        }
        EditorGUILayout.EndScrollView();
    }

    void Log(string msg)
    {
        _log.Add(msg);
        Repaint();
    }

    void DrawLine()
    {
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f));
        EditorGUILayout.Space(2);
    }
}
