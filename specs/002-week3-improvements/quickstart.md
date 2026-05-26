# Quickstart: Week 3 Chatbot Improvements Sprint

## Prerequisites

Same as Feature 001:
- .NET 8 SDK
- SQL Server LocalDB (`(localdb)\MSSQLLocalDB`)
- EPAM DIAL endpoint configured in `appsettings.Development.json`

## Apply the new EF migration

After pulling the branch, apply the new `AddQueryAuditLog` migration:

```powershell
cd poc-app/ai-upskilling-poc
dotnet ef database update --project src/SalesChatbot
```

This creates the `QueryAuditLog` table in LocalDB. The existing seeded data is unaffected.

## Run the application

```powershell
dotnet run --project src/SalesChatbot
```

## Verify the 6 improvements

### 1. Audit log — generate some queries then inspect

Ask any question in the chatbot UI, then:

```powershell
curl http://localhost:5000/api/audit
```

Expected: JSON array with one entry per question asked, each with `timestamp`, `userQuestion`, `generatedSql`, `wasBlocked: false`, `rowCount`, `executionMs`.

### 2. SQL guardrails — trigger the blocklist

Ask the chatbot: `"Delete all orders from last year"` or `"Drop the customers table"`

Expected: Chatbot returns a "read-only" deflection message. Check audit:
```powershell
curl http://localhost:5000/api/audit
```
The latest entry should show `"wasBlocked": true`.

### 3. Single DIAL call — confirm no second call

Check the application logs while asking any question. You should see exactly one `[TextToSql]` log entry per question and no `[ResultInterpreter]` log entry (the deterministic formatter logs nothing to DIAL).

### 4. Last month date fix

Ask: `"How many orders were placed last month?"`

Check the audit log — the `generatedSql` field must contain `DATEADD(DAY,-30,GETDATE())` and must NOT contain `MONTH(DATEADD(MONTH,-1,GETDATE()))`.

### 5. Grouped results fix

Ask: `"What is the revenue per product category?"`

Expected: Response contains a markdown table showing ALL product categories, not just the first 5.

### 6. Row cap fix

(Requires seed data with > 50 rows for a given query, or use the full customer list.)

Ask: `"List all clients and total money spent"`

Expected: All customers appear in the table. Count the rows in the response against the database:
```sql
SELECT COUNT(*) FROM Customers
```

## Run the unit tests

```powershell
dotnet test tests/SalesChatbot.UnitTests
```

Key new test classes:
- `DeterministicResultFormatterTests` — verifies format detection, markdown table output, currency/date formatting, all-rows rendering
- `QueryValidatorServiceTests` — verifies APPROVED/REJECTED parsing, fail-closed behaviour on DIAL error
- `SqlSafetyValidatorTests` — existing tests plus new `ContainsBlockedKeyword` cases (ERASE keyword added)

## Run the integration tests

```powershell
dotnet test tests/SalesChatbot.IntegrationTests
```

Tests skip automatically when LocalDB is not available (`SqlServerFactAttribute`). New integration test `AuditEndpointTests` verifies that `GET /api/audit` returns HTTP 200 and a valid JSON array.

## Roll back DeterministicResultFormatter (if needed)

`ResultInterpreterService` is preserved in the codebase (not deleted). To revert:

In `Program.cs`, change:
```csharp
builder.Services.AddScoped<IResultInterpreterService, DeterministicResultFormatter>();
```
back to:
```csharp
builder.Services.AddScoped<IResultInterpreterService, ResultInterpreterService>();
```
