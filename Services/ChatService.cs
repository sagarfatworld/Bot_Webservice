using Botatwork_in_Livechat.Models;

namespace Botatwork_in_Livechat.Services
{
    public static class ChatService
    {

        public static Dictionary<string, ChatModel> ChatMessages = new();
        public static Dictionary<string, ConversationContext> ConversationContexts = new();

        public class ConversationContext
        {
            public List<string> Messages { get; set; } = new List<string>();
            public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        }
    }
}
