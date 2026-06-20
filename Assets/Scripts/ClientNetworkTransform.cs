using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Thành phần đồng bộ hóa vị trí qua mạng do Client làm chủ (Client-Authoritative).
/// Cho phép máy khách (Client) tự di chuyển nhân vật của mình và đồng bộ tọa độ đó lên Server.
/// </summary>
[AddComponentMenu("Netcode/Client Network Transform")]
public class ClientNetworkTransform : NetworkTransform
{
    /// <summary>
    /// Ghi đè phương thức này để báo cho Netcode biết rằng Server không quản lý quyền thay đổi vị trí.
    /// Trả về false nghĩa là Client sở hữu (Owner) có quyền thay đổi và đồng bộ transform lên mạng.
    /// </summary>
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
