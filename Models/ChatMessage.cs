namespace Botatwork_in_Livechat.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string ChatId { get; set; }
        public string VisitorMessage { get; set; }
        public string BotResponse { get; set; }
        public string AgentEmail { get; set; }
        public int CopyStatus { get; set; }  
        public DateTime Timestamp { get; set; }
        public string MessageHash { get; set; }  
    }
}
