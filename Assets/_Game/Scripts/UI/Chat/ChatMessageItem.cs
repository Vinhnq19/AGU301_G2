using TMPro;
using UnityEngine;

namespace DungeonBuilder.UI.Chat
{
    public sealed class ChatMessageItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;

        public void Setup(string senderName, string message)
        {
            if (_text != null)
            {
                _text.text = $"<color=#FFD700>{senderName}</color>: {message}";
            }
        }
    }
}
