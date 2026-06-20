using UnityEngine;
using UnityEngine.UI;

public class PlayerPositionUI : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform; // Drag player GameObject vào đây
    public Text positionText; // Text UI để hiển thị tọa độ
    
    [Header("Settings")]
    public bool showChunkInfo = true; // Hiển thị thêm chunk position
    public int chunkSize = 16; // Kích thước chunk
    
    void Update()
    {
        if (playerTransform != null && positionText != null)
        {
            Vector3 pos = playerTransform.position;
            
            string displayText = $"Position: ({pos.x:F2}, {pos.y:F2})";
            
            if (showChunkInfo)
            {
                int chunkX = Mathf.FloorToInt(pos.x / chunkSize);
                int chunkY = Mathf.FloorToInt(pos.y / chunkSize);
                displayText += $"\nChunk: ({chunkX}, {chunkY})";
            }
            
            positionText.text = displayText;
        }
    }
}
