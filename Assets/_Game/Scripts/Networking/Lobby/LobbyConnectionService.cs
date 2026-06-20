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

        private NetworkManager _net;

        public ushort Port => _port;

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

            // Host listen tren tat ca interface; address chi de hien thi.
            var transport = _net.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", _port, "0.0.0.0");
            }

            // Payload ten cho chinh host.
            _net.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(playerName ?? string.Empty);

            bool ok = _net.StartHost();
            StatusChanged?.Invoke(ok ? "Hosting" : "Khong the start host");
            return ok;
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
                StatusChanged?.Invoke("Nhap IP phong (ID) truoc khi join");
                return false;
            }

            _net.NetworkConfig.ConnectionApproval = true;

            var transport = _net.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(hostIp.Trim(), _port);
            }

            _net.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(playerName ?? string.Empty);

            bool ok = _net.StartClient();
            StatusChanged?.Invoke(ok ? $"Dang ket noi toi {hostIp}..." : "Khong the start client");
            return ok;
        }

        public void Disconnect()
        {
            if (_net != null && _net.IsListening)
            {
                _net.Shutdown();
            }

            StatusChanged?.Invoke("Da ngat ket noi");
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
                StatusChanged?.Invoke("Khong tim thay NetworkManager");
                DBLog.Warning("lobby.no-nm", "NetworkManager.Singleton null khi start ket noi.", 0f, this);
                return false;
            }

            if (_net.IsListening)
            {
                StatusChanged?.Invoke("Da o trong mot phien ket noi");
                return false;
            }

            return true;
        }
    }
}
