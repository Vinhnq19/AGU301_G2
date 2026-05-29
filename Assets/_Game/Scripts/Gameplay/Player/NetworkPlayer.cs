// Assets/_Game/Scripts/Gameplay/Player/NetworkPlayer.cs
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Điều khiển Player qua mạng. Chỉ owner mới điều khiển được.
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    // NetworkVariable: HP tự động sync
    private NetworkVariable<int> _hp = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int HP => _hp.Value;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Chỉ owner cần camera
            Camera.main.transform.SetParent(transform);
            Debug.Log($"[Player] Spawned - IsOwner: {IsOwner}, ClientId: {OwnerClientId}");
        }
    }

    private void Update()
    {
        // Chỉ xử lý input của chính mình
        if (!IsOwner) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = new Vector3(h, 0, v).normalized;
        transform.Translate(dir * moveSpeed * Time.deltaTime, Space.World);
    }

    // ── Client gửi yêu cầu tấn công lên Server ────────────────────────
    [ServerRpc]
    public void AttackServerRpc(Vector3 targetPosition)
    {
        // Validate và xử lý tấn công trên Server
        Debug.Log($"[Server] Player {OwnerClientId} tấn công tại {targetPosition}");

        // Notify tất cả clients
        AttackEffectClientRpc(targetPosition);
    }

    // ── Server thông báo effect tấn công cho tất cả ───────────────────
    [ClientRpc]
    private void AttackEffectClientRpc(Vector3 position)
    {
        // Spawn particle, âm thanh tại vị trí tấn công
        Debug.Log($"[Client] Hiện effect tấn công tại {position}");
    }

    // ── Server giảm HP Player ─────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damage)
    {
        _hp.Value = Mathf.Max(0, _hp.Value - damage);
        if (_hp.Value <= 0)
        {
            DieClientRpc();
        }
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        Debug.Log($"[Player {OwnerClientId}] Đã chết!");
        // Animation die, respawn logic...
    }
}
