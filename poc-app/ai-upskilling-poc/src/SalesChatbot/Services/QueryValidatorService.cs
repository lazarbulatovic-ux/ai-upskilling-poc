using SalesChatbot.Models;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.Services;

public sealed class QueryValidatorService(IDialClient dialClient, ILogger<QueryValidatorService> logger) : IQueryValidatorService
{
    private const double ValidatorTemperature = 0;

    private const string SystemPrompt = """
        You are a SQL safety reviewer for a read-only sales database chatbot.
        Given a T-SQL SELECT statement, respond with exactly ONE of:
          APPROVED
          REJECTED: <one-line reason>
        Respond with nothing else.
        """;

    public async Task<SqlValidationResult> ValidateAsync(string sql, CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = new List<DialChatMessage>
            {
                new("system", SystemPrompt),
                new("user", sql)
            };

            var response = await dialClient.GetChatCompletionAsync(messages, ValidatorTemperature, cancellationToken);
            var trimmed = response.Trim();

            if (trimmed.StartsWith("APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                return SqlValidationResult.Approved();
            }

            if (trimmed.StartsWith("REJECTED", StringComparison.OrdinalIgnoreCase))
            {
                var colon = trimmed.IndexOf(':', StringComparison.Ordinal);
                var reason = colon >= 0 && colon + 1 < trimmed.Length
                    ? trimmed[(colon + 1)..].Trim()
                    : "Rejected by validator.";
                return SqlValidationResult.Rejected(reason);
            }

            logger.LogWarning("[QueryValidator] Unexpected response: {Response}", trimmed);
            return SqlValidationResult.Rejected("Unexpected validator response.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[QueryValidator] Validator call failed; failing closed.");
            return SqlValidationResult.Rejected("Validator unavailable.");
        }
    }
}
