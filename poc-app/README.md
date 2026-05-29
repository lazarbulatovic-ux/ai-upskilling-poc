# NL-to-SQL Sales Chatbot — PoC

A conversational chatbot that lets non-technical users query a sales database
in plain English. Built using Spec-Driven Development with GitHub Spec Kit.

---

## Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8 Minimal API |
| Frontend | Blazor Server |
| ORM | Entity Framework Core 8 |
| Database | SQL Server LocalDB |
| LLM | EPAM DIAL — GPT-4o |
| Methodology | GitHub Spec Kit (SDD) |
| CI/CD | GitHub Actions |
| Testing | xUnit + FluentAssertions + NSubstitute |

---

## Architecture

```
User (Blazor UI)
  → ConversationService        — orchestrates pipeline, stores NL history
  → TextToSqlService           — NL → SQL via EPAM DIAL (GPT-4o, temp=0)
  → SqlExecutionService        — executes SELECT, 500-row cap
  → DeterministicResultFormatter — formats rows to markdown table (pure C#, no LLM)
  → QueryAuditLog              — every query logged to database
```
![SalesBot_Architecture](../docs/saleschatbot_architecture_optimised.jpg)

**One LLM call per user question.**
The second LLM call (ResultInterpreterService) was replaced with a deterministic
C# formatter in Week 3 — halving response time and token cost.

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server LocalDB (included with Visual Studio)
- API key

### Configuration

Use dotnet user-secrets (recommended — never commit credentials):

```powershell
cd poc-app/ai-upskilling-poc/src/SalesChatbot

dotnet user-secrets set "Dial:Endpoint"    "your-url"
dotnet user-secrets set "Dial:ApiKey"      "your-api-key"
dotnet user-secrets set "Dial:Deployment"  "gpt-4o"
```

Or copy `appsettings.Example.json` to `appsettings.Development.json` and fill in:

```json
{
  "Dial": {
    "Endpoint": "your-url",
    "ApiKey": "your-dial-api-key",
    "Deployment": "gpt-4o"
  },
  "ConnectionStrings": {
    "SalesDb": "Server=(localdb)\\mssqllocaldb;Database=SalesChatbot;Trusted_Connection=True"
  }
}
```

### Run

```powershell
cd poc-app/ai-upskilling-poc

# Apply database migrations (creates tables + seed data)
dotnet ef database update --project src/SalesChatbot

# Start the app
dotnet run --project src/SalesChatbot
```

Open `http://localhost:5211` in your browser.

### Test

```powershell
cd poc-app/ai-upskilling-poc

dotnet test tests/SalesChatbot.UnitTests
dotnet test tests/SalesChatbot.IntegrationTests
```

97/100 unit tests passing. 3 pre-existing DialClient test failures unrelated to core functionality.

---

## Features

- **Natural language → SQL** using GPT-4o (temperature=0, deterministic output)
- **Multi-turn conversation** with session memory — follow-up questions resolve pronouns from prior answers
- **Deterministic result formatting** — markdown tables rendered in the UI, no second LLM call
- **SQL audit log** — every AI-generated query logged to `QueryAuditLog` table with timestamp, question, SQL, row count, and execution time
- **3-layer SQL safety:**
  - Layer 1: Prompt guardrails — RETURN CANNOT_ANSWER rules
  - Layer 2: Regex blocklist — blocks DROP, DELETE, TRUNCATE, INSERT, UPDATE, ALTER, EXEC, ERASE, xp_
  - Layer 3: LLM validator — second DIAL call (temp=0) validates SQL is safe before execution
- **Spec-Driven Development** — full specs, research, data models, and task lists in `specs/`

---

## Try It

Example questions to ask in the chatbot:

```
How many orders were placed last month?
Which of those were from Germany?
And what was the total revenue from them?

What is the revenue per product category?
Show me orders for customer named Acme
Give me all orders for customer with ID 3
List them all

What is the best-selling product?
How many orders came from each country?
How many orders are still pending?

Delete all orders        ← blocked by guardrails
Drop the customers table ← blocked by guardrails
```

---

## Audit Log

View all AI-generated SQL queries while the app is running:

```
GET http://localhost:5211/api/audit
```

Returns last 50 entries ordered by most recent first. Each entry shows:

```json
{
  "id": 42,
  "timestampUtc": "2026-05-25T14:32:01.123Z",
  "userQuestion": "How many orders were placed last month?",
  "generatedSql": "SELECT COUNT(*) AS OrderCount FROM Orders WHERE OrderDate >= DATEADD(DAY,-30,GETDATE())",
  "wasBlocked": false,
  "rowCount": 1,
  "executionMs": 18
}
```

---

## Database Schema

```
Customers   — Id, Name, Country
Products    — Id, Name, Category
Orders      — Id, CustomerId, OrderDate, Status (Completed/Pending/Cancelled)
OrderItems  — Id, OrderId, ProductId, Quantity, UnitPrice
QueryAuditLog — Id, TimestampUtc, UserQuestion, GeneratedSql, WasBlocked, RowCount, ExecutionMs
```

Seed data: 10 customers, 364 orders, multiple products across Electronics and Furniture categories.

---

## Spec-Driven Development

This project was built using [GitHub Spec Kit](https://github.com/github-spec-kit) following the SDD workflow:

```
/speckit-constitution   — establish project principles
/speckit-specify        — capture user stories (no tech decisions yet)
/speckit-clarify        — resolve business logic ambiguities
/speckit-plan           — tech stack + architecture decisions
/speckit-tasks          — atomic implementation tasks
/speckit-implement      — implementation guided by tasks.md
/speckit-git-commit     — conventional commit messages from tasks.md
```

Spec artifacts:

| Feature | Directory | Tasks |
|---------|-----------|-------|
| Feature 001 — Core chatbot | `specs/001-nl-to-sql-chatbot/` | 63 tasks |
| Feature 002 — Week 3 improvements | `specs/002-week3-improvements/` | 31 tasks |

Each spec directory contains: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, `tasks.md`.

---

## Project Structure

```
poc-app/ai-upskilling-poc/
├── specs/
│   ├── 001-nl-to-sql-chatbot/
│   └── 002-week3-improvements/
├── src/
│   └── SalesChatbot/
│       ├── Api/                  — ChatEndpoints.cs (chat + audit endpoints)
│       ├── Components/Pages/     — Chat.razor, MessageList.razor
│       ├── Data/                 — SalesDbContext, Entities, Migrations
│       ├── Infrastructure/Dial/  — DialClient.cs
│       ├── Models/               — ChatExchange, QueryResult, SqlGenerationResult
│       └── Services/
│           ├── Interfaces/       — IConversationService, ITextToSqlService, etc.
│           ├── Validation/       — SqlSafetyValidator
│           ├── ConversationService.cs
│           ├── TextToSqlService.cs
│           ├── SqlExecutionService.cs
│           ├── DeterministicResultFormatter.cs
│           ├── AuditService.cs
│           └── QueryValidatorService.cs
└── tests/
    ├── SalesChatbot.UnitTests/
    └── SalesChatbot.IntegrationTests/
```

---

## Week-by-Week Progress

| Week | Focus | Outcome |
|------|-------|---------|
| Week 1 | AI fundamentals, environment setup, PoC concept | Concept defined, stack chosen |
| Week 2 | Core chatbot implementation | Working NL→SQL chatbot, 69 unit tests, CI pipeline |
| Week 3 | Improvements based on demo feedback | Audit log, guardrails, deterministic formatter, 97 tests |
