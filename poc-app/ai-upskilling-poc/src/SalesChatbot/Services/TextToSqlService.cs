using SalesChatbot.Models;
using SalesChatbot.Services.Interfaces;
using SalesChatbot.Services.Validation;

namespace SalesChatbot.Services;

public sealed class TextToSqlService(IDialClient dialClient, ILogger<TextToSqlService> logger) : ITextToSqlService
{
    private const double SqlTemperature = 0;

    private const string SystemPrompt = """
        You are a T-SQL generator for a sales database.
        YOUR RESPONSE MUST BE EXACTLY ONE OF:
          (A) A single T-SQL SELECT statement — raw SQL only, no markdown, no backticks, no code fences, no explanation, no comments.
          (B) The exact text: CANNOT_ANSWER

        DATABASE SCHEMA (SQL Server):
        Table: Customers   — Id int PK, Name nvarchar(200), Country nvarchar(100)
        Table: Products    — Id int PK, Name nvarchar(200), Category nvarchar(100)
        Table: Orders      — Id int PK, CustomerId int FK→Customers.Id, OrderDate datetime, Status nvarchar(50)
        Table: OrderItems  — Id int PK, OrderId int FK→Orders.Id, ProductId int FK→Products.Id, Quantity int, UnitPrice decimal(18,2)

        BUSINESS RULES:
        - Revenue = SUM(OrderItems.Quantity * OrderItems.UnitPrice) restricted to Orders.Status = 'Completed'.
        - Order counts include all statuses unless the user explicitly requests a filter.
        - Time phrase mapping (use GETDATE()):
            "last month"    → OrderDate >= DATEADD(DAY, -30, GETDATE())
            "this month"    → MONTH(OrderDate) = MONTH(GETDATE()) AND YEAR(OrderDate) = YEAR(GETDATE())
            "this quarter"  → DATEPART(QUARTER, OrderDate) = DATEPART(QUARTER, GETDATE()) AND YEAR(OrderDate) = YEAR(GETDATE())
            "last year"     → YEAR(OrderDate) = YEAR(GETDATE()) - 1
            "recently"      → OrderDate >= DATEADD(DAY, -7, GETDATE())
        - Always cap results: use SELECT TOP 500 when returning multiple rows.

        EXAMPLES:
        Q: How many orders were placed last month?
        A: SELECT COUNT(*) AS OrderCount FROM Orders WHERE OrderDate >= DATEADD(DAY, -30, GETDATE())

        Q: What is the total revenue?
        A: SELECT SUM(oi.Quantity * oi.UnitPrice) AS TotalRevenue FROM OrderItems oi INNER JOIN Orders o ON oi.OrderId = o.Id WHERE o.Status = 'Completed'

        Q: List all customers
        A: SELECT TOP 500 Id, Name, Country FROM Customers

        RETURN CANNOT_ANSWER WHEN:
        - Topic is outside Orders/Customers/Products/OrderItems (weather, payroll, HR, etc.)
        - User asks to write, update, delete, or modify data
        - Query cannot be expressed with a safe SELECT against the schema above
        """;

    public async Task<SqlGenerationResult> GenerateSqlAsync(
        string userQuestion,
        IReadOnlyList<ChatExchange> history,
        CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(userQuestion, history);
        logger.LogInformation("[TextToSql] Sending {MessageCount} messages. Last user message: {Question}",
            messages.Count, userQuestion);
        var response = await dialClient.GetChatCompletionAsync(messages, SqlTemperature, cancellationToken);
        logger.LogInformation("[TextToSql] Raw LLM response: {Response}", response);

        var trimmed = StripMarkdownFences(response.Trim());

        if (trimmed.Equals(ChatConstants.CannotAnswer, StringComparison.Ordinal))
        {
            return SqlGenerationResult.Failure(ChatConstants.CannotAnswer);
        }

        if (!SqlSafetyValidator.IsValidSelect(trimmed, out var reason))
        {
            logger.LogWarning("[TextToSql] Validation failed: {Reason}. Cleaned response: {Cleaned}", reason, trimmed);
            return SqlGenerationResult.Failure(reason ?? "Invalid SQL generated.");
        }

        return SqlGenerationResult.Success(trimmed);
    }

    /// <summary>
    /// Strips markdown code fences (```sql ... ``` or ``` ... ```) that the model
    /// sometimes produces despite the instruction to return raw SQL only.
    /// </summary>
    private static string StripMarkdownFences(string text)
    {
        var s = text;

        // Remove opening fence: ```sql or ```
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var newline = s.IndexOf('\n');
            s = newline >= 0 ? s[(newline + 1)..] : s[3..];
        }

        // Remove closing fence
        if (s.EndsWith("```", StringComparison.Ordinal))
        {
            s = s[..^3];
        }

        return s.Trim();
    }

    private static List<DialChatMessage> BuildMessages(string userQuestion, IReadOnlyList<ChatExchange> history)
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

        messages.Add(new DialChatMessage("user", userQuestion));
        return messages;
    }
}
