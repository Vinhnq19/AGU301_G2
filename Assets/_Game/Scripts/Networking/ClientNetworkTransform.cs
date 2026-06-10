using Unity.Netcode.Components;
using UnityEngine;

namespace DungeonBuilder.Networking
{
    /// <summary>
    /// Cho phep Client (Owner) duoc quyen cap nhat Transform thay vi chi Server.
    /// Thay the component NetworkTransform hien tai tren Player prefab bang component nay.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        /// <summary>
        /// Cho phep Owner ghi de vi tri/goc quay len server.
        /// </summary>
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
