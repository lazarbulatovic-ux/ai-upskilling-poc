using SalesChatbot.Models;

namespace SalesChatbot.Services.Interfaces;

public interface IQueryValidatorService
{
    Task<SqlValidationResult> ValidateAsync(string sql, CancellationToken cancellationToken = default);
}
