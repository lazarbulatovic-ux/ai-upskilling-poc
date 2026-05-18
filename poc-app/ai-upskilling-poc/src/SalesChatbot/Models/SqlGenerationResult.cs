namespace SalesChatbot.Models;

public sealed class SqlGenerationResult
{
    public bool IsSuccess { get; init; }

    public string? Sql { get; init; }

    public string? FailureReason { get; init; }

    public static SqlGenerationResult Success(string sql) =>
        new() { IsSuccess = true, Sql = sql };

    public static SqlGenerationResult Failure(string reason) =>
        new() { IsSuccess = false, FailureReason = reason };
}
