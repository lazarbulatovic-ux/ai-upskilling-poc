using System.Text;
using SalesChatbot.Models;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.Services;

public sealed class ResultInterpreterService(IDialClient dialClient) : IResultInterpreterService
{
    private const double InterpretTemperature = 0.3;

    private const string SystemPrompt = """
        You interpret SQL query results for business users. Respond in plain, non-technical language.
        - Format currency amounts in EUR using the € symbol.
        - For multiple rows, give the total count plus a brief summary of up to 5 top/representative results.
        - For zero rows, clearly state that no matching data was found.
        - For single-value answers (counts, totals, averages), keep responses to one concise sentence.
        - Never mention SQL, tables, or queries.
        """;

    public async Task<string> InterpretAsync(
        string userQuestion,
        QueryResult queryResult,
        IReadOnlyList<ChatExchange> history,
        CancellationToken cancellationToken = default)
    {
        if (queryResult.RowCount == 0)
        {
            return await dialClient.GetChatCompletionAsync(
                BuildMessages(userQuestion, history, "Query returned zero rows."),
                InterpretTemperature,
                cancellationToken);
        }

        var dataSummary = BuildDataSummary(queryResult);
        return await dialClient.GetChatCompletionAsync(
            BuildMessages(userQuestion, history, dataSummary),
            InterpretTemperature,
            cancellationToken);
    }

    private static string BuildDataSummary(QueryResult queryResult)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Row count: {queryResult.RowCount}");
        builder.AppendLine($"Columns: {string.Join(", ", queryResult.ColumnNames)}");

        var sampleRows = queryResult.Rows.Take(5);
        var rowIndex = 1;
        foreach (var row in sampleRows)
        {
            builder.AppendLine($"Row {rowIndex}: {string.Join("; ", row.Select(kv => $"{kv.Key}={kv.Value}"))}");
            rowIndex++;
        }

        return builder.ToString();
    }

    private static List<DialChatMessage> BuildMessages(
        string userQuestion,
        IReadOnlyList<ChatExchange> history,
        string dataSummary)
    {
        var messages = new List<DialChatMessage>
        {
            new("system", SystemPrompt)
        };

        foreach (var exchange in history)
        {
            messages.Add(new DialChatMessage("user", exchange.UserMessage));
            messages.Add(new DialChatMessage("assistant", exchange.AssistantMessage));
        }

        messages.Add(new DialChatMessage("user", $"Question: {userQuestion}\n\nResults:\n{dataSummary}"));
        return messages;
    }
}
