using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Networking
{
    /// <summary>
    /// Debug UI don gian cho phep Start Host / Client ngay trong Editor.
    /// Dat script nay len bat ky GameObject nao trong scene khi test.
    /// XOA TRUOC KHI BUILD RELEASE.
    /// </summary>
    public sealed class NetworkDebugUI : MonoBehaviour
    {
        [SerializeField] private string _hostAddress = "127.0.0.1";

        private void OnGUI()
        {
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.IsListening)
            {
                GUILayout.Label($"[Network] Listening — IsHost={NetworkManager.Singleton.IsHost} | Clients={NetworkManager.Singleton.ConnectedClientsIds.Count}");
                if (GUILayout.Button("Disconnect")) NetworkManager.Singleton.Shutdown();
                return;
            }

            GUILayout.Label("=== Network Debug ===");
            if (GUILayout.Button("Start HOST"))
            {
                NetworkManager.Singleton.StartHost();
            }

            if (GUILayout.Button("Start CLIENT"))
            {
                // Neu dung Unity Transport, set address truoc khi StartClient
                var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                if (transport != null) transport.SetConnectionData(_hostAddress, 7777);
                NetworkManager.Singleton.StartClient();
            }

            if (GUILayout.Button("Start SERVER (headless)"))
            {
                NetworkManager.Singleton.StartServer();
            }
        }
    }
}
