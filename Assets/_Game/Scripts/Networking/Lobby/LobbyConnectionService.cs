using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DungeonBuilder.Core.Debugging;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DungeonBuilder.Networking.Lobby
{
    /// <summary>
    /// Boc logic Host/Join cho lobby. Truyen ten nguoi choi qua connection payload,
    /// server doc trong ConnectionApprovalCallback va luu vao LobbyController truoc khi client connected.
    /// </summary>
    public sealed class LobbyConnectionService : MonoBehaviour
    {
        [SerializeField] private LobbyController _lobbyController;
        [SerializeField] private ushort _port = 7777;

        [Tooltip("Số port dự phòng sẽ thử khi port chính bị chiếm (7777 → 7778 → 7779...). " +
                 "Unity Editor thường không nhả UDP socket sau khi thoát Play mode, nên port cũ " +
                 "vẫn bị giữ cho tới khi đóng Editor.")]
        [SerializeField, Range(0, 20)] private int _portFallbackAttempts = 10;

        private NetworkManager _net;

        /// <summary>Port đang thực sự dùng (có thể khác _port nếu phải fallback). Client cần đúng port này.</summary>
        public ushort ActivePort { get; private set; }

        public ushort Port => ActivePort != 0 ? ActivePort : _port;

        /// <summary>Trang thai ket noi thay doi (started ok / failed / disconnected).</summary>
        public event Action<string> StatusChanged;

        private void Awake()
        {
            _net = NetworkManager.Singleton;
            if (_net != null)
            {
                // NetworkManager song xuyen scene tu Lobby -> game scene.
                DontDestroyOnLoad(_net.gameObject);
            }
        }

        /// <summary>Bat dau lam host (vua server vua choi).</summary>
        public bool StartHost(string playerName)
        {
            if (!EnsureReady())
            {
                return false;
            }

            _net.NetworkConfig.ConnectionApproval = true;
            _net.ConnectionApprovalCallback = ApprovalCheck;

            // Payload ten cho chinh host.
            _net.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(playerName ?? string.Empty);

            var transport = _net.GetComponent<UnityTransport>();

            // Dò trước port còn trống rồi mới StartHost MỘT lần.
            // Không thử-rồi-fail nhiều lần được: NGO tự Shutdown khi bind lỗi và không cho
            // StartHost lại ngay trong cùng frame (ném "There is no NetworkManager assigned").
            //
            // Port bị chiếm là chuyện thường khi dev: Unity Editor giữ lại UDP socket của phiên
            // Play trước cho tới khi đóng Editor, nên 7777 có thể "bận" dù không game nào đang chạy.
            int attempts = Mathf.Max(0, _portFallbackAttempts) + 1;
            ushort chosen = 0;
            for (int i = 0; i < attempts; i++)
            {
                ushort candidate = (ushort)(_port + i);
                if (IsUdpPortFree(candidate))
                {
                    chosen = candidate;
                    break;
                }
            }

            if (chosen == 0)
            {
                ActivePort = 0;
                StatusChanged?.Invoke($"Failed to start host — port {_port}..{_port + attempts - 1} đều bận");
                DBLog.Warning("lobby.no-free-port",
                    $"Không còn port trống trong dải {_port}..{_port + attempts - 1}. " +
                    "Thường do Unity Editor giữ socket của phiên Play cũ — khởi động lại Editor để giải phóng.", 0f, this);
                return false;
            }

            if (transport != null)
            {
                // Host listen tren tat ca interface; address chi de hien thi.
                transport.SetConnectionData("0.0.0.0", chosen, "0.0.0.0");
            }

            if (!_net.StartHost())
            {
                ActivePort = 0;
                StatusChanged?.Invoke("Failed to start host");
                return false;
            }

            ActivePort = chosen;

            if (chosen == _port)
            {
                StatusChanged?.Invoke("Hosting");
            }
            else
            {
                DBLog.Warning("lobby.port-fallback",
                    $"Port {_port} bị chiếm — đã host trên port {chosen}. " +
                    "Người chơi khác phải join bằng đúng IP:port này.", 0f, this);
                StatusChanged?.Invoke($"Hosting (port {chosen})");
            }

            return true;
        }

        /// <summary>Thử bind tạm một UDP socket để biết port còn trống hay không.</summary>
        private static bool IsUdpPortFree(ushort port)
        {
            try
            {
                using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                probe.Bind(new IPEndPoint(IPAddress.Any, port));
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        /// <summary>Join vao host theo IP. ID phong = IP cua host.</summary>
        public bool StartClient(string hostIp, string playerName)
        {
            if (!EnsureReady())
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(hostIp))
            {
                StatusChanged?.Invoke("Enter Room IP (ID) before joining");
                return false;
            }

            _net.NetworkConfig.ConnectionApproval = true;

            // Chấp nhận cả "192.168.1.5" lẫn "192.168.1.5:7778" — khi host phải fallback sang
            // port khác thì Room ID có kèm port, dán thẳng vào là join được.
            ParseAddress(hostIp, out string ip, out ushort port);

            var transport = _net.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ip, port);
            }

            _net.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(playerName ?? string.Empty);

            bool ok = _net.StartClient();
            StatusChanged?.Invoke(ok ? $"Connecting to {ip}:{port}..." : "Failed to start client");
            return ok;
        }

        /// <summary>Tách "ip" hoặc "ip:port" thành 2 phần; thiếu port thì dùng port mặc định.</summary>
        private void ParseAddress(string raw, out string ip, out ushort port)
        {
            ip = raw.Trim();
            port = _port;

            int colon = ip.LastIndexOf(':');
            if (colon <= 0 || colon >= ip.Length - 1)
            {
                return;
            }

            string portText = ip.Substring(colon + 1);
            if (ushort.TryParse(portText, out ushort parsed) && parsed > 0)
            {
                port = parsed;
                ip = ip.Substring(0, colon).Trim();
            }
        }

        public void Disconnect()
        {
            if (_net != null && _net.IsListening)
            {
                _net.Shutdown();
            }

            StatusChanged?.Invoke("Disconnected");
        }

        /// <summary>Lay IPv4 LAN cua may nay de hien thi lam Room ID.</summary>
        public string GetLocalIPv4()
        {
            try
            {
                // Mo socket UDP toi mot dia chi ngoai de OS chon interface LAN dung.
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint)
                {
                    return endPoint.Address.ToString();
                }
            }
            catch (Exception e)
            {
                DBLog.Warning("lobby.ip-socket", $"Khong lay duoc IP qua socket: {e.Message}", 0f, this);
            }

            // Fallback: duyet host entry.
            try
            {
                foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                DBLog.Warning("lobby.ip-dns", $"Khong lay duoc IP qua DNS: {e.Message}", 0f, this);
            }

            return "127.0.0.1";
        }

        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            string playerName = request.Payload != null && request.Payload.Length > 0
                ? Encoding.UTF8.GetString(request.Payload)
                : null;

            if (_lobbyController != null)
            {
                _lobbyController.RegisterPendingName(request.ClientNetworkId, playerName);
            }

            // Player object spawn o game scene, khong phai lobby.
            response.CreatePlayerObject = false;
            response.Approved = true;
            response.Pending = false;
        }

        private bool EnsureReady()
        {
            _net ??= NetworkManager.Singleton;
            if (_net == null)
            {
                StatusChanged?.Invoke("NetworkManager not found");
                DBLog.Warning("lobby.no-nm", "NetworkManager.Singleton null khi start ket noi.", 0f, this);
                return false;
            }

            if (_net.IsListening)
            {
                StatusChanged?.Invoke("Already in a session");
                return false;
            }

            return true;
        }
    }
}
