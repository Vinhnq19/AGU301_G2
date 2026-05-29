using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giao diện khởi động: Host, Join, hoặc Server.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button serverButton;
    [SerializeField] private TMP_Text statusText;

    private void Start()
    {
        hostButton.onClick.AddListener(StartHost);
        clientButton.onClick.AddListener(StartClient);
        serverButton.onClick.AddListener(StartServer);

        // Lắng nghe sự kiện kết nối
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // ── HOST: vừa là Server vừa là Client ─────────────────────────────
    private void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        statusText.text = "🟢 Đang chạy với tư cách HOST (Server + Client)";
        Debug.Log("[Lobby] Started as HOST");
    }

    // ── CLIENT: kết nối tới Server ────────────────────────────────────
    private void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        statusText.text = "🔵 Đang kết nối tới Server...";
        Debug.Log("[Lobby] Started as CLIENT");
    }

    // ── SERVER: chỉ là Server (không render) ──────────────────────────
    private void StartServer()
    {
        NetworkManager.Singleton.StartServer();
        statusText.text = "🟡 Đang chạy với tư cách SERVER";
        Debug.Log("[Lobby] Started as SERVER");
    }

    private void OnClientConnected(ulong clientId)
    {
        statusText.text = $"✅ Client {clientId} đã kết nối! Tổng: {NetworkManager.Singleton.ConnectedClients.Count}";
    }

    private void OnClientDisconnected(ulong clientId)
    {
        statusText.text = $"❌ Client {clientId} đã ngắt kết nối.";
    }
}
