namespace SalesChatbot.Models;

public sealed class ChatExchange
{
    public required string UserMessage { get; init; }

    public required string AssistantMessage { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
