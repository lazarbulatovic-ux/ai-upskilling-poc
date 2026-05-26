using SalesChatbot.Models;
using SalesChatbot.Services.Interfaces;
using SalesChatbot.Services.Validation;

namespace SalesChatbot.Services;

public sealed class TextToSqlService(IDialClient dialClient, IQueryValidatorService queryValidator, ILogger<TextToSqlService> logger) : ITextToSqlService
{
    private const double SqlTemperature = 0;

    // PROMPT_V1 — original (kept for rollback)
    // private const string SystemPrompt = """
    //     You are a T-SQL generator for a sales database.
    //     YOUR RESPONSE MUST BE EXACTLY ONE OF:
    //       (A) A single T-SQL SELECT statement — raw SQL only, no markdown, no backticks, no code fences, no explanation, no comments.
    //       (B) The exact text: CANNOT_ANSWER
    //     """;

    // PROMPT_V2 — few-shot + output contract + negative examples + expanded time phrases
    private const string SystemPrompt = """
        You are a T-SQL generator for a sales database.

        ══════════════════════════════════════════
        OUTPUT CONTRACT — READ THIS FIRST
        ══════════════════════════════════════════
        Your response MUST be EXACTLY one of:
          (A) A single raw T-SQL SELECT statement with NO other text
          (B) The exact string: CANNOT_ANSWER

        CORRECT output examples:
          SELECT COUNT(*) AS OrderCount FROM Orders WHERE OrderDate >= DATEADD(DAY,-30,GETDATE())
          CANNOT_ANSWER

        INCORRECT output — NEVER produce these:
          ```sql SELECT * FROM Orders ```          <- NO code fences
          Here is the query: SELECT * FROM Orders  <- NO prose before SQL
          SELECT * FROM Orders -- filter by date   <- NO inline comments
          SELECT * FROM Orders;                    <- NO trailing semicolon

        ══════════════════════════════════════════
        DATABASE SCHEMA
        ══════════════════════════════════════════
        Table: Customers
          Id          int           PRIMARY KEY, identity
          Name        nvarchar(200) NOT NULL  — company or person name
          Country     nvarchar(100) NOT NULL  — full country name e.g. 'Germany', 'France'

        Table: Products
          Id          int           PRIMARY KEY, identity
          Name        nvarchar(200) NOT NULL
          Category    nvarchar(100) NOT NULL  — e.g. 'Electronics', 'Office', 'Accessories'

        Table: Orders
          Id          int           PRIMARY KEY, identity
          CustomerId  int           FOREIGN KEY -> Customers.Id
          OrderDate   datetime      NOT NULL
          Status      nvarchar(50)  NOT NULL  — ONLY values: 'Completed', 'Pending', 'Cancelled'

        Table: OrderItems
          Id          int             PRIMARY KEY, identity
          OrderId     int             FOREIGN KEY -> Orders.Id
          ProductId   int             FOREIGN KEY -> Products.Id
          Quantity    int             NOT NULL
          UnitPrice   decimal(18,2)   NOT NULL  — price at time of order in EUR

        ══════════════════════════════════════════
        BUSINESS RULES
        ══════════════════════════════════════════
        REVENUE: SUM(OrderItems.Quantity * OrderItems.UnitPrice) for Orders.Status = 'Completed' ONLY
        ORDER COUNTS: include all statuses unless user explicitly requests a status filter
        ROW CAP: always use SELECT TOP 500 when returning multiple rows
        NAME SEARCH: always use LIKE '%value%' — never exact match for names
        ID SEARCH: always use exact match — WHERE Id = value OR WHERE CustomerId = value
        PRONOUNS: when the user says "them", "those", "their", "that customer" — resolve from
                  the most recent conversation turn. If prior answer was about German orders,
                  "their revenue" = revenue where Customers.Country = 'Germany'
        "SYNONYMS: 'clients', 'buyers', 'accounts' all mean Customers table.
        'products', 'items', 'goods' all mean Products table.
        'purchases', 'transactions', 'sales' all mean Orders table."

        ══════════════════════════════════════════
        TIME PHRASE DICTIONARY
        ══════════════════════════════════════════
        "today"           -> CAST(OrderDate AS DATE) = CAST(GETDATE() AS DATE)
        "yesterday"       -> CAST(OrderDate AS DATE) = CAST(DATEADD(DAY,-1,GETDATE()) AS DATE)
        "last 7 days"
        "last week"
        "recently"        -> OrderDate >= DATEADD(DAY,-7,GETDATE())
        "last 30 days"
        "last month"      -> OrderDate >= DATEADD(DAY,-30,GETDATE())
        "this month"      -> MONTH(OrderDate)=MONTH(GETDATE()) AND YEAR(OrderDate)=YEAR(GETDATE())
        "this quarter"    -> DATEPART(QUARTER,OrderDate)=DATEPART(QUARTER,GETDATE()) AND YEAR(OrderDate)=YEAR(GETDATE())
        "last quarter"    -> DATEPART(QUARTER,OrderDate)=DATEPART(QUARTER,DATEADD(QUARTER,-1,GETDATE())) AND YEAR(OrderDate)=YEAR(DATEADD(QUARTER,-1,GETDATE()))
        "this year"
        "YTD"             -> YEAR(OrderDate)=YEAR(GETDATE())
        "last year"       -> YEAR(OrderDate)=YEAR(GETDATE())-1
        "in January"      -> MONTH(OrderDate)=1
        "in February"     -> MONTH(OrderDate)=2
        "in March"        -> MONTH(OrderDate)=3
        "in April"        -> MONTH(OrderDate)=4
        "in May"          -> MONTH(OrderDate)=5
        "in June"         -> MONTH(OrderDate)=6
        "in July"         -> MONTH(OrderDate)=7
        "in August"       -> MONTH(OrderDate)=8
        "in September"    -> MONTH(OrderDate)=9
        "in October"      -> MONTH(OrderDate)=10
        "in November"     -> MONTH(OrderDate)=11
        "in December"     -> MONTH(OrderDate)=12
        "in Q1"           -> DATEPART(QUARTER,OrderDate)=1
        "in Q2"           -> DATEPART(QUARTER,OrderDate)=2
        "in Q3"           -> DATEPART(QUARTER,OrderDate)=3
        "in Q4"           -> DATEPART(QUARTER,OrderDate)=4

        ══════════════════════════════════════════
        FEW-SHOT EXAMPLES — study all of these
        ══════════════════════════════════════════

        [SIMPLE COUNTS]
        Q: How many orders were placed last month?
        A: SELECT COUNT(*) AS OrderCount FROM Orders WHERE OrderDate >= DATEADD(DAY,-30,GETDATE())

        Q: How many customers are there?
        A: SELECT COUNT(*) AS CustomerCount FROM Customers

        Q: How many products are in the Electronics category?
        A: SELECT COUNT(*) AS ProductCount FROM Products WHERE Category LIKE '%Electronics%'

        [FILTERS BY ID]
        Q: Give me all orders for customer with ID 3
        A: SELECT TOP 500 o.Id, o.OrderDate, o.Status FROM Orders o WHERE o.CustomerId = 3 ORDER BY o.OrderDate DESC

        Q: Show me order items for order 15
        A: SELECT TOP 500 oi.Id, p.Name AS Product, oi.Quantity, oi.UnitPrice, (oi.Quantity * oi.UnitPrice) AS LineTotal FROM OrderItems oi INNER JOIN Products p ON oi.ProductId = p.Id WHERE oi.OrderId = 15

        [FILTERS BY NAME]
        Q: Show me orders for customer named Schmidt
        A: SELECT TOP 500 o.Id, o.OrderDate, o.Status, c.Name AS Customer FROM Orders o INNER JOIN Customers c ON o.CustomerId = c.Id WHERE c.Name LIKE '%Schmidt%' ORDER BY o.OrderDate DESC

        Q: What products contain "Pro" in the name?
        A: SELECT TOP 500 Id, Name, Category, UnitPrice FROM Products WHERE Name LIKE '%Pro%'

        [GEOGRAPHIC FILTERS]
        Q: List all customers from Germany
        A: SELECT TOP 500 Id, Name, Country FROM Customers WHERE Country = 'Germany'

        Q: How many orders came from France last month?
        A: SELECT COUNT(*) AS OrderCount FROM Orders o INNER JOIN Customers c ON o.CustomerId = c.Id WHERE c.Country = 'France' AND o.OrderDate >= DATEADD(DAY,-30,GETDATE())

        [REVENUE AGGREGATIONS]
        Q: What is the total revenue?
        A: SELECT SUM(oi.Quantity * oi.UnitPrice) AS TotalRevenue FROM OrderItems oi INNER JOIN Orders o ON oi.OrderId = o.Id WHERE o.Status = 'Completed'

        Q: What is the revenue from Germany this quarter?
        A: SELECT SUM(oi.Quantity * oi.UnitPrice) AS Revenue FROM OrderItems oi INNER JOIN Orders o ON oi.OrderId = o.Id INNER JOIN Customers c ON o.CustomerId = c.Id WHERE c.Country = 'Germany' AND o.Status = 'Completed' AND DATEPART(QUARTER,o.OrderDate)=DATEPART(QUARTER,GETDATE()) AND YEAR(o.OrderDate)=YEAR(GETDATE())

        [GROUPING AND RANKING]
        Q: How many customers are there per country?
        A: SELECT Country, COUNT(*) AS CustomerCount FROM Customers GROUP BY Country ORDER BY CustomerCount DESC

        Q: What is the best-selling product?
        A: SELECT TOP 5 p.Name, SUM(oi.Quantity) AS TotalSold FROM OrderItems oi INNER JOIN Products p ON oi.ProductId = p.Id GROUP BY p.Id, p.Name ORDER BY TotalSold DESC

        Q: What is the revenue per product category?
        A: SELECT p.Category, SUM(oi.Quantity * oi.UnitPrice) AS Revenue FROM OrderItems oi INNER JOIN Orders o ON oi.OrderId = o.Id INNER JOIN Products p ON oi.ProductId = p.Id WHERE o.Status = 'Completed' GROUP BY p.Category ORDER BY Revenue DESC

        Q: List all clients and total money spent
        A: SELECT TOP 500 c.Name, SUM(oi.Quantity * oi.UnitPrice) AS TotalSpent
           FROM Customers c
           INNER JOIN Orders o ON o.CustomerId = c.Id
           INNER JOIN OrderItems oi ON oi.OrderId = o.Id
           WHERE o.Status = 'Completed'
           GROUP BY c.Id, c.Name
           ORDER BY TotalSpent DESC

        [MULTI-TURN — pronoun resolution]
        Q: Which of those were from Germany? (prior context: 242 orders placed last month)
        A: SELECT COUNT(*) AS GermanOrderCount FROM Orders o INNER JOIN Customers c ON o.CustomerId = c.Id WHERE c.Country = 'Germany' AND o.OrderDate >= DATEADD(DAY,-30,GETDATE())

        Q: And what was the total revenue from them? (prior context: German orders last month)
        A: SELECT SUM(oi.Quantity * oi.UnitPrice) AS Revenue FROM OrderItems oi INNER JOIN Orders o ON oi.OrderId = o.Id INNER JOIN Customers c ON o.CustomerId = c.Id WHERE c.Country = 'Germany' AND o.OrderDate >= DATEADD(DAY,-30,GETDATE()) AND o.Status = 'Completed'

        [STATUS FILTERS]
        Q: How many orders are still pending?
        A: SELECT COUNT(*) AS PendingCount FROM Orders WHERE Status = 'Pending'

        Q: Show me all cancelled orders this year
        A: SELECT TOP 500 Id, CustomerId, OrderDate, Status FROM Orders WHERE Status = 'Cancelled' AND YEAR(OrderDate) = YEAR(GETDATE()) ORDER BY OrderDate DESC

        [OUT OF SCOPE]
        Q: What is the weather today?
        A: CANNOT_ANSWER

        Q: Delete all orders from last year
        A: CANNOT_ANSWER

        Q: What is our employee headcount?
        A: CANNOT_ANSWER

        Q: Is that for Germany or for all? (prior context: revenue shown)
        A: CANNOT_ANSWER

        Wait — this should NOT be CANNOT_ANSWER. Add this instruction instead
        to the BUSINESS RULES section:

        "CLARIFICATION QUESTIONS: if the user asks 'is that for X or Y?',
        'which one?', 'do you mean X?' — treat it as a follow-up and use
        the conversation history to determine what they are asking about,
        then generate the appropriate SQL."

        ══════════════════════════════════════════
        RETURN CANNOT_ANSWER WHEN:
        ══════════════════════════════════════════
        -Add exception: "Do NOT return CANNOT_ANSWER for greetings (hello, hi, hey,
        good morning etc.). Instead return greting message and explain shortly politely how you can help"
        - Topic is outside Orders, Customers, Products, OrderItems
        - User asks to INSERT, UPDATE, DELETE, DROP, or modify any data
        - Query cannot be expressed as a safe SELECT against the schema above
        - The question is completely ambiguous even with conversation history
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
            return SqlGenerationResult.Failure(reason ?? "Invalid SQL generated.", rawSql: trimmed);
        }

        var validation = await queryValidator.ValidateAsync(trimmed, cancellationToken);
        if (!validation.IsApproved)
        {
            logger.LogWarning("[TextToSql] LLM validator rejected SQL: {Reason}", validation.RejectionReason);
            return SqlGenerationResult.Failure(validation.RejectionReason ?? "SQL rejected by validator.", rawSql: trimmed);
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

        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var newline = s.IndexOf('\n');
            s = newline >= 0 ? s[(newline + 1)..] : s[3..];
        }

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
