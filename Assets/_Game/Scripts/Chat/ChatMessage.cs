namespace DungeonBuilder.Chat
{
    public readonly struct ChatMessage
    {
        public string SenderName { get; }
        public string Text { get; }

        public ChatMessage(string senderName, string text)
        {
            SenderName = senderName;
            Text = text;
        }
    }
}
