using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Player
{
    /// <summary>
    /// Gắn trên GameObject chứa Icon của Minimap (nằm trong Player Prefab).
    /// Layer của GameObject này nên được set là 'Minimap'.
    /// </summary>
    public sealed class MinimapIcon : NetworkBehaviour
    {
        [SerializeField] private SpriteRenderer _iconRenderer;
        
        [Header("Colors")]
        [SerializeField] private Color _selfColor = Color.green;
        [SerializeField] private Color _teammateColor = Color.cyan;

        public override void OnNetworkSpawn()
        {
            if (_iconRenderer != null)
            {
                // Bản thân -> Xanh lá, Đồng đội -> Xanh dương
                _iconRenderer.color = IsOwner ? _selfColor : _teammateColor;
            }
        }
    }
}
