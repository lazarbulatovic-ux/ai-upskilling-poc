using SalesChatbot.Models;

namespace SalesChatbot.Services.Interfaces;

public interface ITextToSqlService
{
    Task<SqlGenerationResult> GenerateSqlAsync(
        string userQuestion,
        IReadOnlyList<ChatExchange> history,
        CancellationToken cancellationToken = default);
}
