using System.Collections.Generic;
using DungeonBuilder.Chat;
using DungeonBuilder.UI.Base;
using Unity.Collections;

namespace DungeonBuilder.UI.Chat
{
    public sealed class ChatPresenter : BasePresenter<ChatView, ChatModel>
    {
        private readonly ChatManager _chatManager;

        public IReadOnlyList<ChatMessage> Messages => Model.Messages;

        public ChatPresenter(ChatView view, ChatModel model, ChatManager chatManager) : base(view, model)
        {
            _chatManager = chatManager;
            view.SetPresenter(this);
        }

        public override void Initialize()
        {
            _chatManager.OnMessageReceived += HandleNewMessage;
            base.Initialize();
        }

        public override void Dispose()
        {
            _chatManager.OnMessageReceived -= HandleNewMessage;
            base.Dispose();
        }

        public void SubmitMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string trimmed = text.Trim();
            if (trimmed.Length > 128)
            {
                trimmed = trimmed.Substring(0, 128);
            }

            _chatManager.SendChatMessageRpc(new FixedString128Bytes(trimmed));
        }

        private void HandleNewMessage(string senderName, string message)
        {
            Model.AddMessage(senderName, message);
            View.OnNewMessageArrived();
        }

        protected override void OnModelChanged()
        {
            View.Render();
        }
    }
}
