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

        public void Setup(int index, string playerName, bool isHost)
        {
            if (_indexText != null)
            {
                _indexText.text = $"#{index + 1}";
            }

            if (_nameText != null)
            {
                _nameText.text = isHost ? $"{playerName} (Host)" : playerName;
            }
        }
    }
}
