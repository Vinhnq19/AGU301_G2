using Unity.Netcode;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // NetworkVariable tự động sync từ Server → tất cả Client
    private NetworkVariable<int> _score = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> _gameStarted = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int Score => _score.Value;
    public bool GameStarted => _gameStarted.Value;

    public override void OnNetworkSpawn()
    {
        _score.OnValueChanged += OnScoreChanged;
        _gameStarted.OnValueChanged += OnGameStartedChanged;
    }

    public override void OnNetworkDespawn()
    {
        _score.OnValueChanged -= OnScoreChanged;
        _gameStarted.OnValueChanged -= OnGameStartedChanged;
    }

    // ── Server RPC: Client yêu cầu tăng score ──────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void AddScoreServerRpc(int amount)
    {
        if (!IsServer) return;
        _score.Value += amount;
    }

    // ── Server-only: Bắt đầu game ──────────────────────────────────────
    public void StartGame()
    {
        if (!IsServer) return;
        _gameStarted.Value = true;
        StartGameClientRpc();
    }

    // ── Client RPC: Thông báo game bắt đầu ────────────────────────────
    [ClientRpc]
    private void StartGameClientRpc()
    {
        Debug.Log("[NetworkGameManager] Game đã bắt đầu trên tất cả clients!");
    }

    private void OnScoreChanged(int prev, int next)
    {
        Debug.Log($"[Score] {prev} → {next}");
        // Cập nhật UI tại đây
    }

    private void OnGameStartedChanged(bool prev, bool next)
    {
        if (next) Debug.Log("[GameManager] Game Started!");
    }
}