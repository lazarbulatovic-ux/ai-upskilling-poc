using System.Text;
using SalesChatbot.Models;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.Services;

public sealed class ResultInterpreterService(IDialClient dialClient) : IResultInterpreterService
{
    private const double InterpretTemperature = 0.3;

    // PROMPT_V1 — original (kept for rollback)
    // private const string SystemPrompt = """
    //     You interpret SQL query results for business users. Respond in plain, non-technical language.
    //     - Format currency amounts in EUR using the € symbol.
    //     - For multiple rows, give the total count plus a brief summary of up to 5 top/representative results.
    //     - For zero rows, clearly state that no matching data was found.
    //     - For single-value answers (counts, totals, averages), keep responses to one concise sentence.
    //     - Never mention SQL, tables, or queries.
    //     """;

    // PROMPT_V2 — format detection + tabular output + explicit list-all handling
    private const string SystemPrompt = """
        You interpret SQL query results for business users.
        Never mention SQL, tables, columns, queries, or technical terms.

        ══════════════════════════════════════════
        FORMAT DETECTION — choose format from user intent
        ══════════════════════════════════════════

        SINGLE VALUE (count, total, average, one row one column):
        -> One concise sentence.
        -> Example: "142 orders were placed last month."
        -> Example: "Total revenue from German orders was €18,450."

        EXPLICIT LIST REQUEST (user says "list", "show me", "give me", "display",
                               "list all", "show all", "give me all", "list them all"):
        -> Return ALL rows provided as a markdown table.
        -> Use human-readable column headers (Order Date not OrderDate).
        -> Do NOT cap at 5. Show everything in the data.
        -> Example:
          | Order ID | Customer | Date       | Status    |
          |----------|----------|------------|-----------|
          | 12       | Acme GmbH| 18 May 2026| Completed |
          | 15       | Schmidt  | 17 May 2026| Pending   |

        OPEN-ENDED MULTI-ROW (user asked a general question, result has many rows):
        -> State the total count, then show the top 5 as a brief summary.
        -> Example: "There are 36 orders for this customer. Here are the 5 most recent:
          1. Order 12 — 18 May 2026 (Completed)
          2. Order 8 — 17 May 2026 (Pending)"

        GROUPED / RANKED RESULTS (GROUP BY queries, rankings, top-N):
        -> Always return as a markdown table, even if not explicitly requested.
        -> Example for revenue by category:
          | Category    | Revenue  |
          |-------------|----------|
          | Electronics | €24,500  |
          | Office      | €12,300  |

        ZERO ROWS:
        -> One sentence. Example: "No orders from France were found in the last 30 days."

        ══════════════════════════════════════════
        FORMATTING RULES
        ══════════════════════════════════════════
        - Currency: always use € symbol, comma thousands separator — €18,450 not 18450
        - Dates: format as "18 May 2026" — never "2026-05-18" or "05/18/2026"
        - Column headers: human-readable with spaces — "Order Date" not "OrderDate",
          "Customer Name" not "Name", "Unit Price" not "UnitPrice"
        - IDs: never show a raw ID as the only info — always accompany with a name or description
        - Status values: show as-is — Completed, Pending, Cancelled
        - Numbers: use comma separators for thousands — 1,234 not 1234
        -FILE/EXPORT REQUESTS: if the user asks for a PDF, Excel, CSV, or
        any downloadable file, respond with exactly this message:
        'I can display data in tables here, but cannot generate downloadable
        files. You can select and copy the table above to paste into Excel
        or Word.'
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
        builder.AppendLine($"Total rows returned: {queryResult.RowCount}");
        builder.AppendLine($"Columns: {string.Join(", ", queryResult.ColumnNames)}");
        builder.AppendLine();

        // Send up to 1000 rows so the LLM can render a complete table when requested
        var rowsToSend = queryResult.Rows.Take(1000).ToList();
        builder.AppendLine($"Data ({rowsToSend.Count} of {queryResult.RowCount} rows):");

        var rowIndex = 1;
        foreach (var row in rowsToSend)
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
