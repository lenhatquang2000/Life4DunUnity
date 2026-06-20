using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class ChunkManager : MonoBehaviour
{
    public static ChunkManager Instance;
    
    [Header("Chunk Settings")]
    public int chunkSize = 16;
    public string chunkDataPath = "ChunkData";
    
    private Dictionary<string, ChunkData> loadedChunks = new Dictionary<string, ChunkData>();
    private HashSet<string> chunksToGenerate = new HashSet<string>();
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[ChunkManager] Instance created and set");
        }
        else
        {
            Debug.LogWarning("[ChunkManager] Duplicate instance destroyed");
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        Debug.Log("[ChunkManager] Start() called");
        EnsureChunkDataDirectory();
    }
    
    /// <summary>
    /// Xóa tất cả chunks đã lưu - Gọi từ code hoặc Inspector
    /// </summary>
    [ContextMenu("Delete All Saved Chunks")]
    public void DeleteAllSavedChunks()
    {
        string fullPath = Path.Combine(Application.persistentDataPath, chunkDataPath);
        
        if (Directory.Exists(fullPath))
        {
            string[] files = Directory.GetFiles(fullPath, "*.json");
            int count = files.Length;
            
            foreach (string file in files)
            {
                File.Delete(file);
                Debug.Log($"[ChunkManager] Deleted: {Path.GetFileName(file)}");
            }
            
            loadedChunks.Clear();
            Debug.Log($"[ChunkManager] ✓✓✓ DELETED {count} CHUNK FILES ✓✓✓");
            Debug.Log($"[ChunkManager] Path: {fullPath}");
            Debug.Log($"[ChunkManager] Please restart the game to generate new chunks!");
        }
        else
        {
            Debug.LogWarning($"[ChunkManager] Chunk directory doesn't exist: {fullPath}");
        }
    }
    
    /// <summary>
    /// Mở folder chứa chunks trong File Explorer
    /// </summary>
    [ContextMenu("Open Chunks Folder")]
    public void OpenChunksFolder()
    {
        string fullPath = Path.Combine(Application.persistentDataPath, chunkDataPath);
        
        if (Directory.Exists(fullPath))
        {
            Application.OpenURL("file://" + fullPath);
            Debug.Log($"[ChunkManager] Opened folder: {fullPath}");
        }
        else
        {
            Debug.LogWarning($"[ChunkManager] Folder doesn't exist: {fullPath}");
        }
    }
    
    void EnsureChunkDataDirectory()
    {
        string fullPath = Path.Combine(Application.persistentDataPath, chunkDataPath);
        Debug.Log($"[ChunkManager] ===== CHUNK DATA LOCATION =====");
        Debug.Log($"[ChunkManager] Chunk data path: {fullPath}");
        Debug.Log($"[ChunkManager] ==================================");
        
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
            Debug.Log($"[ChunkManager] Created chunk data directory");
        }
        else
        {
            Debug.Log($"[ChunkManager] Chunk data directory already exists");
            
            // List existing chunk files
            string[] chunkFiles = Directory.GetFiles(fullPath, "*.json");
            Debug.Log($"[ChunkManager] Found {chunkFiles.Length} existing chunk files");
            foreach (string file in chunkFiles)
            {
                Debug.Log($"[ChunkManager]   - {Path.GetFileName(file)}");
            }
        }
    }
    
    public string GetChunkKey(int chunkX, int chunkY)
    {
        return $"chunk_{chunkX}_{chunkY}";
    }
    
    public bool ChunkExists(int chunkX, int chunkY)
    {
        string chunkKey = GetChunkKey(chunkX, chunkY);
        string filePath = GetChunkFilePath(chunkX, chunkY);
        bool exists = File.Exists(filePath);
        Debug.Log($"[ChunkManager] ChunkExists({chunkX}, {chunkY}): {exists} at {filePath}");
        return exists;
    }
    
    public ChunkData LoadChunk(int chunkX, int chunkY)
    {
        string chunkKey = GetChunkKey(chunkX, chunkY);
        Debug.Log($"[ChunkManager] LoadChunk({chunkX}, {chunkY}) - Key: {chunkKey}");
        
        if (loadedChunks.ContainsKey(chunkKey))
        {
            Debug.Log($"[ChunkManager] Chunk {chunkKey} already loaded from memory");
            return loadedChunks[chunkKey];
        }
        
        string filePath = GetChunkFilePath(chunkX, chunkY);
        
        if (File.Exists(filePath))
        {
            Debug.Log($"[ChunkManager] Loading chunk {chunkKey} from file: {filePath}");
            string json = File.ReadAllText(filePath);
            ChunkData data = JsonUtility.FromJson<ChunkData>(json);
            loadedChunks[chunkKey] = data;
            Debug.Log($"[ChunkManager] Successfully loaded chunk {chunkKey} with {data.tileSpriteNames.Length} tiles");
            return data;
        }
        
        Debug.Log($"[ChunkManager] Chunk {chunkKey} not found on disk at {filePath}");
        return null;
    }
    
    public void SaveChunk(ChunkData chunkData)
    {
        string chunkKey = GetChunkKey(chunkData.chunkX, chunkData.chunkY);
        string filePath = GetChunkFilePath(chunkData.chunkX, chunkData.chunkY);
        
        Debug.Log($"[ChunkManager] Saving chunk {chunkKey} to {filePath}");
        string json = JsonUtility.ToJson(chunkData, true);
        File.WriteAllText(filePath, json);
        
        loadedChunks[chunkKey] = chunkData;
        Debug.Log($"[ChunkManager] Saved chunk {chunkKey} with {chunkData.tileSpriteNames.Length} tiles");
    }
    
    private string GetChunkFilePath(int chunkX, int chunkY)
    {
        string fileName = $"chunk_{chunkX}_{chunkY}.json";
        return Path.Combine(Application.persistentDataPath, chunkDataPath, fileName);
    }
    
    public void UnloadChunk(int chunkX, int chunkY)
    {
        string chunkKey = GetChunkKey(chunkX, chunkY);
        if (loadedChunks.ContainsKey(chunkKey))
        {
            loadedChunks.Remove(chunkKey);
        }
    }
    
    public void ClearLoadedChunks()
    {
        loadedChunks.Clear();
    }
}
