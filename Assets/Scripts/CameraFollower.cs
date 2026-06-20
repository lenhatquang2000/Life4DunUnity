using UnityEngine;

public class CameraFollower : MonoBehaviour
{
    public Transform playerTransform;
    [Header("Camera Settings")]
    public float cameraSize = 4f; // Điều chỉnh để zoom in/out (nhỏ hơn = gần hơn)
    public Vector3 cameraOffset = new Vector3(0, 0, -10f); // Offset từ player
    public float followSpeed = 0.1f; // Độ mượt của camera (0 = ngay lập tức, 1 = rất chậm)
    
    private Camera mainCamera;
    
    void Start()
    {
        mainCamera = GetComponent<Camera>();
        
        if (mainCamera == null)
        {
            Debug.LogError("[CameraFollower] Main Camera component not found!");
            return;
        }
        
        // Tự động tìm player nếu chưa assign
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                Debug.Log("[CameraFollower] Found player automatically");
            }
            else
            {
                Debug.LogWarning("[CameraFollower] Player not found! Please assign player in Inspector");
            }
        }
        
        // Set camera size
        mainCamera.orthographicSize = cameraSize;
        Debug.Log($"[CameraFollower] Camera size set to {cameraSize}");
    }
    
    void LateUpdate()
    {
        if (playerTransform == null) return;
        
        // Tính vị trí mục tiêu cho camera
        Vector3 targetPosition = playerTransform.position + cameraOffset;
        
        // Smooth follow hoặc ngay lập tức
        if (followSpeed <= 0)
        {
            transform.position = targetPosition;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed);
        }
    }
}
