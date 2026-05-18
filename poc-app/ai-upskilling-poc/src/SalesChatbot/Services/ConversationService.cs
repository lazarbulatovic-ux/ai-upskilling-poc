using Microsoft.Data.SqlClient;
using SalesChatbot.Models;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.Services;

public sealed class ConversationService(
    ITextToSqlService textToSqlService,
    ISqlExecutionService sqlExecutionService,
    IResultInterpreterService resultInterpreterService) : IConversationService
{
    private readonly ConversationSession _session = new();

    public IReadOnlyList<ChatExchange> GetHistory() => _session.Exchanges;

    public void Reset() => _session.Clear();

    public async Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var trimmed = message.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return DeflectionMessages.Rephrase;
        }

        if (IsAmbiguousFollowUp(trimmed, _session.Exchanges))
        {
            return DeflectionMessages.MissingContext;
        }

        SqlGenerationResult sqlResult;
        try
        {
            sqlResult = await textToSqlService.GenerateSqlAsync(trimmed, _session.Exchanges, cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return DeflectionMessages.DataUnavailable;
        }

        if (!sqlResult.IsSuccess || sqlResult.Sql is null)
        {
            var reply = MapGenerationFailure(sqlResult.FailureReason);
            _session.AddExchange(new ChatExchange { UserMessage = trimmed, AssistantMessage = reply });
            return reply;
        }

        QueryResult queryResult;
        try
        {
            queryResult = await sqlExecutionService.ExecuteQueryAsync(sqlResult.Sql, cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex) || ex is InvalidOperationException)
        {
            if (IsDatabaseUnavailable(ex))
            {
                return DeflectionMessages.DataUnavailable;
            }

            var reply = DeflectionMessages.OutOfScope;
            _session.AddExchange(new ChatExchange { UserMessage = trimmed, AssistantMessage = reply });
            return reply;
        }

        string answer;
        try
        {
            answer = await resultInterpreterService.InterpretAsync(
                trimmed,
                queryResult,
                _session.Exchanges,
                cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return DeflectionMessages.DataUnavailable;
        }

        _session.AddExchange(new ChatExchange { UserMessage = trimmed, AssistantMessage = answer });
        return answer;
    }

    private static bool IsAmbiguousFollowUp(string message, IReadOnlyList<ChatExchange> history)
    {
        if (history.Count > 0)
        {
            return false;
        }

        var lower = message.ToLowerInvariant();
        string[] followUpStarters = ["and ", "which ", "what about", "how about", "also "];
        string[] vagueFollowUps = ["total revenue", "and total", "which ones", "which were"];

        return followUpStarters.Any(lower.StartsWith)
               || vagueFollowUps.Any(lower.Contains);
    }

    private static string MapGenerationFailure(string? reason)
    {
        if (string.Equals(reason, ChatConstants.CannotAnswer, StringComparison.Ordinal))
        {
            return DeflectionMessages.OutOfScope;
        }

        if (reason?.Contains("Forbidden token", StringComparison.OrdinalIgnoreCase) == true
            && (reason.Contains("DELETE", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("INSERT", StringComparison.OrdinalIgnoreCase)))
        {
            return DeflectionMessages.ReadOnly;
        }

        if (reason?.Contains("write", StringComparison.OrdinalIgnoreCase) == true
            || reason?.Contains("DELETE", StringComparison.OrdinalIgnoreCase) == true
            || reason?.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) == true)
        {
            return DeflectionMessages.ReadOnly;
        }

        return DeflectionMessages.OutOfScope;
    }

    private static bool IsDatabaseUnavailable(Exception ex)
    {
        if (ex is SqlException or TimeoutException)
        {
            return true;
        }

        if (ex is InvalidOperationException ioe)
        {
            var msg = ioe.Message;
            return msg.Contains("network", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("connection", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("login", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
