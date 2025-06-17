using Botatwork_in_Livechat.Models;
using Botatwork_in_Livechat.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace Botatwork_in_Livechat.Controllers
{
    public class LiveChatController : Controller
    {
        private const string WebhookSecret = "Km5CLuX7YGXyEcr2Z6PsEaSI235kBGva";
        private const string BotApiUrl = "https://api.botatwork.com/trigger-task/42eaa2c8-e8aa-43ad-b9b5-944981bce2a2";
        private const string BotApiKey = "bf2e2d7e409bc0d7545e14ae15a773a3";

        [HttpPost]
        [Route("livechat/webhook")]
        public async Task<IActionResult> Webhook([FromBody] JsonElement body)
        {
            if (!VerifySignature(body)) return Unauthorized();

            var messageText = body.GetProperty("payload").GetProperty("event").GetProperty("text").GetString();
            var chatId = body.GetProperty("payload").GetProperty("chat_id").GetString();
            var agentIds = body.GetProperty("additional_data")
                                .GetProperty("chat_presence_user_ids")
                                .EnumerateArray()
                                .Select(e => e.GetString())
                                .Where(id => id.Contains("@"))
                                .ToList();
            var agentId = agentIds.FirstOrDefault();

            if (string.IsNullOrEmpty(messageText) || string.IsNullOrEmpty(chatId))
                return Ok("Missing required data");

            if (!ChatService.ChatMessages.ContainsKey(chatId))
            {
                ChatService.ChatMessages[chatId] = new ChatModel
                {
                    ChatId = chatId,
                    AgentId = agentId
                };
            }

            if (!ChatService.ConversationContexts.ContainsKey(chatId))
            {
                ChatService.ConversationContexts[chatId] = new ChatService.ConversationContext();
            }

            var context = ChatService.ConversationContexts[chatId];
            context.Messages.Add($"Visitor: {messageText}");
            context.LastUpdate = DateTime.UtcNow;

            string fullContext = string.Join("\n", context.Messages);
            var botPayload = new
            {
                data = new
                {
                    payload = new
                    {
                        override_model = "sonar",
                        clientQuestion = fullContext
                    }
                },
                should_stream = false
            };

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("x-api-key", BotApiKey);
            var botResponse = await client.PostAsync(BotApiUrl,
                new StringContent(JsonSerializer.Serialize(botPayload), Encoding.UTF8, "application/json"));

            string botContent = await botResponse.Content.ReadAsStringAsync();
            string botAnswer = JsonDocument.Parse(botContent).RootElement
                .GetProperty("data").GetProperty("content").GetString();

            context.Messages.Add($"Bot: {botAnswer}");

            ChatService.ChatMessages[chatId].Messages.Add(new MessageModel
            {
                VisitorMessage = messageText,
                BotResponse = botAnswer,
                Timestamp = DateTime.UtcNow.ToString("o")
            });

            return Json(new { visitorMessage = messageText, botResponse = botAnswer });
        }

        [HttpGet("livechat/chats/{agentEmail}")]
        public IActionResult GetChats(string agentEmail)
        {
            var chats = ChatService.ChatMessages
                //.Where(x => x.Value.AgentId == agentId)
                .Where(x => x.Value.AgentId != null && x.Value.AgentId.Equals(agentEmail, StringComparison.OrdinalIgnoreCase))

                .Select(x => new { chatId = x.Key, messages = x.Value.Messages })
                .ToList();

            return Json(chats);
        }

        [HttpGet("livechat/chat/{chatId}")]
        public IActionResult GetChat(string chatId)
        {
            if (ChatService.ChatMessages.TryGetValue(chatId, out var chat))
                return Json(chat.Messages);
            return Json(new List<MessageModel>());
        }

        private bool VerifySignature(JsonElement body)
        {
            return true;
        }
    }
}
