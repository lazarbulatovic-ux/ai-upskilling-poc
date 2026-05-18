using SalesChatbot.Models;

namespace SalesChatbot.Services.Interfaces;

public interface IResultInterpreterService
{
    Task<string> InterpretAsync(
        string userQuestion,
        QueryResult queryResult,
        IReadOnlyList<ChatExchange> history,
        CancellationToken cancellationToken = default);
}
