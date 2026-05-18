# Quickstart: NL-to-SQL Sales Chatbot PoC

**Feature**: `001-nl-to-sql-chatbot` | **Date**: 2026-05-18

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 8.x | `dotnet --version` |
| SQL Server LocalDB | latest | `(localdb)\mssqllocaldb` (Windows); optional for unit-only work |
| EPAM DIAL API key | — | GPT-4o deployment access |

## Initial Setup

```powershell
# From repository root (after implementation exists)
dotnet restore
dotnet build

# Configure secrets (never commit ApiKey)
cd src/SalesChatbot
dotnet user-secrets init
dotnet user-secrets set "Dial:Endpoint" "https://<your-dial-host>/"
dotnet user-secrets set "Dial:ApiKey" "<your-api-key>"
dotnet user-secrets set "Dial:Deployment" "gpt-4o"
```

`appsettings.Development.json` connection string (default):

```json
{
  "ConnectionStrings": {
    "SalesDb": "Server=(localdb)\\mssqllocaldb;Database=SalesChatbot;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

## Database Migration & Seed

```powershell
cd src/SalesChatbot
dotnet ef database update
dotnet run -- --seed
```

If LocalDB is unavailable, unit tests still pass; integration tests skip automatically.

## Run the Application

```powershell
cd src/SalesChatbot
dotnet run
```

Open the URL shown (typically `https://localhost:7xxx`). Use the chat page to ask sales questions.

### Sample Questions

| Question | Expected behavior |
|----------|-------------------|
| How many orders were placed last month? | Count all orders in rolling 30 days |
| How many customers are from Germany? | Customer count filter |
| What is the best-selling product? | Count + top product summary |
| Which were from Germany? (after prior order question) | Contextual follow-up count |
| What is the weather today? | Deflection message |

Click **New Chat** to reset session context.

## Run Tests

```powershell
# From repository root
dotnet test

# Unit tests only
dotnet test tests/SalesChatbot.UnitTests

# Integration (requires LocalDB)
dotnet test tests/SalesChatbot.IntegrationTests
```

## Format Check (CI parity)

```powershell
dotnet format --verify-no-changes
```

## API Smoke Test (Minimal API)

```powershell
# Send a message
curl -X POST https://localhost:7xxx/api/chat/message `
  -H "Content-Type: application/json" `
  -d "{\"message\": \"How many orders were placed last month?\"}"

# Reset session
curl -X POST https://localhost:7xxx/api/chat/new
```

## GitHub Actions

CI runs on push/PR: build, test, format verify. DIAL credentials are **not** required for CI (unit tests mock `IDialClient`).

## Troubleshooting

| Issue | Action |
|-------|--------|
| LocalDB not found | Install SQL Server Express LocalDB or skip integration tests |
| DIAL 401/403 | Verify `Dial:ApiKey` and endpoint URL in user secrets |
| Empty chat responses | Check database seeded; verify connection string |
| Slow responses (>10s) | Check DIAL latency; reduce history size (already capped at 10 exchanges) |

## Key Paths

| Artifact | Path |
|----------|------|
| Spec | `specs/001-nl-to-sql-chatbot/spec.md` |
| Plan | `specs/001-nl-to-sql-chatbot/plan.md` |
| Data model | `specs/001-nl-to-sql-chatbot/data-model.md` |
| API contracts | `specs/001-nl-to-sql-chatbot/contracts/` |
| Constitution | `.specify/memory/constitution.md` |
