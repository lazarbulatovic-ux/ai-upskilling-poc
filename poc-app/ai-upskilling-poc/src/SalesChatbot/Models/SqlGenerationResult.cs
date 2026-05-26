namespace SalesChatbot.Models;

public sealed class SqlGenerationResult
{
    public bool IsSuccess { get; init; }

    public string? Sql { get; init; }

    public string? FailureReason { get; init; }

    public string? RawSql { get; init; }

    public static SqlGenerationResult Success(string sql) =>
        new() { IsSuccess = true, Sql = sql, RawSql = sql };

    public static SqlGenerationResult Failure(string reason, string? rawSql = null) =>
        new() { IsSuccess = false, FailureReason = reason, RawSql = rawSql };
}
