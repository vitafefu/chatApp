namespace ChatServer
{
    public enum MessageStatus
    {
        Sent,       // отправлено
        Delivered,  // доставлено
        Read        // прочитано
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        public string FromEmail { get; set; } = "";
        public string ToEmail { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; }

        public bool IsFile { get; set; }      
        public string? FileName { get; set; }

        public MessageStatus Status { get; set; }

        public byte[]? FileContent { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}
