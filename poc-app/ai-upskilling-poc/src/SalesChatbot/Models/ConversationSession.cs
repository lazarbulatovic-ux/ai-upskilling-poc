namespace SalesChatbot.Models;

public sealed class ConversationSession
{
    public const int MaxExchanges = 10;

    private readonly List<ChatExchange> _exchanges = [];

    public IReadOnlyList<ChatExchange> Exchanges => _exchanges;

    public void AddExchange(ChatExchange exchange)
    {
        _exchanges.Add(exchange);
        while (_exchanges.Count > MaxExchanges)
        {
            _exchanges.RemoveAt(0);
        }
    }

    public void Clear() => _exchanges.Clear();
}
