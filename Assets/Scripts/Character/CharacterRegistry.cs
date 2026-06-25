using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject lưu danh sách tất cả Character có thể dùng trong game.
/// Tạo asset: Assets > Create > AILife > Character Registry
/// Được dùng trong Runtime (AuthUI_IMGUI) để hiển thị danh sách chọn nhân vật.
/// Được cập nhật tự động bởi NetworkPrefabManagerTool khi scan prefabs.
/// </summary>
[CreateAssetMenu(fileName = "CharacterRegistry", menuName = "AILife/Character Registry")]
public class CharacterRegistry : ScriptableObject
{
    [System.Serializable]
    public class CharacterEntry
    {
        [Tooltip("Tên model - phải khớp với Prefab name và modelName trên server")]
        public string modelName;

        [Tooltip("Prefab của character (phải có NetworkObject)")]
        public GameObject prefab;

        [Tooltip("Tên hiển thị thân thiện trong UI")]
        public string displayName;
    }

    [Header("Danh sach Character")]
    public List<CharacterEntry> characters = new List<CharacterEntry>();

    /// <summary>Trả về mảng tên dùng cho GUILayout.Toolbar</summary>
    public string[] GetDisplayNames()
    {
        string[] names = new string[characters.Count];
        for (int i = 0; i < characters.Count; i++)
            names[i] = string.IsNullOrEmpty(characters[i].displayName)
                ? characters[i].modelName
                : characters[i].displayName;
        return names;
    }

    /// <summary>Trả về modelName theo index</summary>
    public string GetModelName(int index)
    {
        if (index < 0 || index >= characters.Count) return "";
        return characters[index].modelName;
    }

    /// <summary>Tìm prefab theo modelName</summary>
    public GameObject GetPrefab(string modelName)
    {
        foreach (var c in characters)
            if (c.modelName == modelName) return c.prefab;
        return null;
    }

    /// <summary>Kiểm tra modelName đã có trong danh sách chưa</summary>
    public bool Contains(string modelName)
    {
        foreach (var c in characters)
            if (c.modelName == modelName) return true;
        return false;
    }
}
