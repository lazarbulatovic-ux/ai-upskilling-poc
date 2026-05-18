using SalesChatbot.Models;

namespace SalesChatbot.Services.Interfaces;

public interface ISqlExecutionService
{
    Task<QueryResult> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default);
}
