using SalesChatbot.Models;

namespace SalesChatbot.Services.Interfaces;

public interface IConversationService
{
    Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default);

    void Reset();

    IReadOnlyList<ChatExchange> GetHistory();
}
