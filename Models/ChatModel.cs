namespace Botatwork_in_Livechat.Models
{
    public class ChatModel
    {
        public string ChatId { get; set; }
        public string AgentId { get; set; }
        public List<MessageModel> Messages { get; set; } = new List<MessageModel>();
    }
}
