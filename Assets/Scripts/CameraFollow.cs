using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothing = 5f;
    public Vector3 offset = new Vector3(0, 0, -10f);

    void Start()
    {
        // If target is not assigned, try to find the player
        if (target == null)
        {
            GameObject player = GameObject.Find("Mira");
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Target position with offset
        Vector3 targetPosition = target.position + offset;
        
        // Smoothly move camera towards target position
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothing * Time.deltaTime);
    }
}
