namespace SalesChatbot.Services.Interfaces;

public interface IDialClient
{
    Task<string> GetChatCompletionAsync(
        IReadOnlyList<DialChatMessage> messages,
        double temperature,
        CancellationToken cancellationToken = default);
}

public sealed record DialChatMessage(string Role, string Content);
