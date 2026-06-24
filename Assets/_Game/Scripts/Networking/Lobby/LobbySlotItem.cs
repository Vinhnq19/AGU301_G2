using TMPro;
using UnityEngine;

namespace DungeonBuilder.Networking.Lobby
{
    /// <summary>
    /// Mot dong slot trong hang cho. Hien thi so thu tu + ten nguoi choi.
    /// Pattern theo TowerOptionButton.
    /// </summary>
    public sealed class LobbySlotItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text _indexText;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private UnityEngine.UI.Image _playerAvatar;

        public void Setup(int index, string playerName, bool isHost, ulong clientId)
        {
            if (_indexText != null)
            {
                _indexText.text = $"#{index + 1}";
            }

            if (_nameText != null)
            {
                _nameText.text = isHost ? $"{playerName} (Host)" : playerName;
            }

            if (_playerAvatar != null)
            {
                // Sử dụng công thức tính màu giống hệt trong PlayerAnimation.cs
                _playerAvatar.color = Color.HSVToRGB((clientId * 0.3f) % 1f, 0.8f, 1f);

                // Tự động lấy Sprite từ PlayerPrefab
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null)
                {
                    var anim = Unity.Netcode.NetworkManager.Singleton.NetworkConfig.PlayerPrefab.GetComponentInChildren<DungeonBuilder.Player.PlayerAnimation>();
                    if (anim != null && anim.DefaultSprite != null)
                    {
                        _playerAvatar.sprite = anim.DefaultSprite;
                    }
                }
            }
        }
    }
}
