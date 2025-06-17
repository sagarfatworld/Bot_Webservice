using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Botatwork_in_Livechat.Services
{
    public class ChatCleanupService : IHostedService, IDisposable
    {
        private Timer _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(DoCleanup, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        return Task.CompletedTask;
    }

    private void DoCleanup(object state)
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        foreach (var chatId in ChatService.ConversationContexts.Keys.ToList())
        {
            var ctx = ChatService.ConversationContexts[chatId];
            if (ctx.LastUpdate < cutoff)
            {
                ChatService.ConversationContexts.Remove(chatId);
                ChatService.ChatMessages.Remove(chatId);
                Console.WriteLine($"Cleaned chatId: {chatId}");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public void Dispose() => _timer?.Dispose();
}
}
