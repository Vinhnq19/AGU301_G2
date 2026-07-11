using DungeonBuilder.Player;
using DungeonBuilder.UI;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Gameplay.Camera
{
    /// <summary>
    /// Gắn kết <see cref="RespawnPanelView"/> (screen-space, sống trên HUD Canvas) với
    /// <see cref="PlayerStats"/> của LOCAL player. Chỉ chạy cho player mà client hiện tại sở hữu,
    /// giống pattern của <see cref="LocalPlayerCameraBinder"/>.
    /// </summary>
    public sealed class LocalPlayerRespawnPanelBinder : NetworkBehaviour
    {
        private RespawnPanelView _panel;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }

            _panel = FindFirstObjectByType<RespawnPanelView>();
            if (_panel == null)
            {
                Debug.LogWarning(
                    "[LocalPlayerRespawnPanelBinder] No RespawnPanelView was found in the scene.",
                    this);
                return;
            }

            var stats = GetComponent<PlayerStats>();
            if (stats == null)
            {
                Debug.LogWarning(
                    "[LocalPlayerRespawnPanelBinder] Missing PlayerStats on the local player.",
                    this);
                return;
            }

            _panel.Bind(stats);
        }

        public override void OnNetworkDespawn()
        {
            if (_panel != null)
            {
                _panel.Unbind();
                _panel = null;
            }
        }
    }
}
