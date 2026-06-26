using System;
using System.Collections.Generic;
using DungeonBuilder.Chat;
using DungeonBuilder.UI.Base;

namespace DungeonBuilder.UI.Chat
{
    public sealed class ChatModel : IModel
    {
        public event Action OnChanged;

        public IReadOnlyList<ChatMessage> Messages => _messages;

        private readonly List<ChatMessage> _messages = new();
        private const int MaxMessages = 50;

        public void AddMessage(string senderName, string text)
        {
            if (_messages.Count >= MaxMessages)
            {
                _messages.RemoveAt(0);
            }

            _messages.Add(new ChatMessage(senderName, text));
            OnChanged?.Invoke();
        }
    }
}
