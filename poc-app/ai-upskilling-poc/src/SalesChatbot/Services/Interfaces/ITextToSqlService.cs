using SalesChatbot.Models;

namespace SalesChatbot.Services.Interfaces;

public interface ITextToSqlService
{
    Task<SqlGenerationResult> GenerateSqlAsync(
        string userQuestion,
        IReadOnlyList<ChatExchange> history,
        CancellationToken cancellationToken = default);

    //Task<string> FormatResultAsync(
    //    string userQuestion,
    //    QueryResult queryResult,
    //    IReadOnlyList<ChatExchange> history,
    //    CancellationToken cancellationToken = default);
}
