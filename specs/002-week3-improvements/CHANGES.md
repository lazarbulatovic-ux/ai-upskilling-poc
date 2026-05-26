# Week 3 Improvements — Change Summary

**Branch**: `002-week3-improvements`
**Commit**: `35c50b3`
**Date**: 2026-05-25

---

## Overview

Six improvements were implemented based on Zbigniew demo feedback, covering SQL safety, observability, performance (fewer AI calls), and output correctness.

---

## 1. SQL Query Audit Log (US1)

**What changed**: Every AI-generated SQL query is now persisted to the database before execution.

**New files**:
- `src/SalesChatbot/Data/Entities/QueryAuditEntry.cs` — EF entity with fields: `Id`, `TimestampUtc`, `UserQuestion`, `GeneratedSql`, `WasBlocked`, `RowCount`, `ExecutionMs`
- `src/SalesChatbot/Data/Migrations/20260525102918_AddQueryAuditLog.cs` — EF migration creating the `QueryAuditLog` table
- `src/SalesChatbot/Services/Interfaces/IAuditService.cs` — interface with `LogAsync`
- `src/SalesChatbot/Services/AuditService.cs` — implementation; audit failures are swallowed (logged as warning, never surfaced to user)

**Modified files**:
- `src/SalesChatbot/Data/SalesDbContext.cs` — added `DbSet<QueryAuditEntry> QueryAuditLog`, EF configuration (max lengths, index on `TimestampUtc`)
- `src/SalesChatbot/Services/ConversationService.cs` — injects `IAuditService`; logs blocked queries (`WasBlocked=true`) and successful queries (`WasBlocked=false`) with Stopwatch-measured `ExecutionMs`
- `src/SalesChatbot/Api/ChatEndpoints.cs` — added `GET /api/audit` endpoint returning last 50 entries ordered by timestamp descending; 503 handler for DB unavailable
- `src/SalesChatbot/Program.cs` — registers `IAuditService` → `AuditService` as Scoped

**Tests added**:
- `tests/SalesChatbot.UnitTests/Services/AuditServiceTests.cs` — uses EF InMemory; verifies entry persistence and exception swallowing
- `tests/SalesChatbot.IntegrationTests/AuditEndpointTests.cs` — `[SqlServerFact]` tests for HTTP 200 and JSON content type

---

## 2. SQL Safety Guardrails (US2)

**What changed**: Two independent safety layers now sit between SQL generation and database execution — a standalone regex keyword check and a secondary LLM validator.

**New files**:
- `src/SalesChatbot/Services/Interfaces/IQueryValidatorService.cs` — interface with `Task<SqlValidationResult> ValidateAsync`
- `src/SalesChatbot/Services/QueryValidatorService.cs` — calls DIAL at temperature=0 with a terse validation prompt; parses `APPROVED` / `REJECTED: <reason>`; returns `Rejected("Validator unavailable")` on any exception (fail-closed)
- `src/SalesChatbot/Models/SqlValidationResult.cs` — sealed record with `IsApproved`, `RejectionReason`, and static factories `Approved()` / `Rejected(string)`

**Modified files**:
- `src/SalesChatbot/Services/Validation/SqlSafetyValidator.cs` — added `ERASE` to `ForbiddenTokens`; added new public static method `ContainsBlockedKeyword(string? sql, out string? matchedKeyword)` using whole-word case-insensitive regex
- `src/SalesChatbot/Services/TextToSqlService.cs` — injects `IQueryValidatorService`; after `IsValidSelect()` passes, calls `ValidateAsync`; on rejection returns `Failure(reason, rawSql: trimmed)` so the blocked SQL is available for audit logging
- `src/SalesChatbot/Models/SqlGenerationResult.cs` — added `RawSql` nullable property; updated both factory methods (`Success` and `Failure`) to populate it
- `src/SalesChatbot/Program.cs` — registers `IQueryValidatorService` → `QueryValidatorService` as Scoped

**Tests added**:
- `tests/SalesChatbot.UnitTests/Services/QueryValidatorServiceTests.cs` — covers `APPROVED`, `REJECTED:`, exception→fail-closed, unexpected response, whitespace trimming
- `tests/SalesChatbot.UnitTests/Validation/SqlSafetyValidatorTests.cs` — added `ContainsBlockedKeyword` tests: null input, valid SELECT, DROP/ERASE keywords, whole-word check (`EXECUTOR` must not match `EXEC`)

---

## 3. Single DIAL Call — Deterministic Formatter (US3 / US6)

**What changed**: The second AI call in `ResultInterpreterService` is eliminated. Query results are now formatted by a deterministic C# class with no network dependency.

**New file**:
- `src/SalesChatbot/Services/DeterministicResultFormatter.cs` — implements `IResultInterpreterService`; uses `[GeneratedRegex]` source generators; no `IDialClient` dependency

**Format logic**:
| Result shape | Output |
|---|---|
| 0 rows | `"No results were found matching your query."` |
| 1 row, 1 column | `"Label: Value"` (single sentence) |
| All other cases | Full markdown table with ALL rows |

**Helpers** (both `public static` for testability):
- `HumaniseHeader(string)` — `PascalCase` → `Pascal Case` via `[GeneratedRegex]`
- `FormatValue(string columnName, object? value)` — `DateTime`/`DateTimeOffset` → `"d MMM yyyy"`; currency columns (name contains `revenue`, `price`, `total`, `amount`, `spent`, `cost`) → `€N2`; `null` → `""`; other decimals → `N2`

**Modified files**:
- `src/SalesChatbot/Program.cs` — `IResultInterpreterService` now resolves to `DeterministicResultFormatter`; `ResultInterpreterService` kept registered as itself for rollback

**Tests added**:
- `tests/SalesChatbot.UnitTests/Services/DeterministicResultFormatterTests.cs` — zero rows, single value, multi-row table, 75-row no-cap assertion, 10-group all-groups-shown, currency formatting, null, DateTime, `HumaniseHeader` PascalCase, non-currency decimal

---

## 4. "Last Month" Date Fix (US4)

**What changed**: The TIME PHRASE DICTIONARY in the system prompt had two contradictory "last month" entries using calendar-month logic (`MONTH(DATEADD(MONTH,-1,...))` + `YEAR()`) that produced wrong results near month boundaries.

**Modified file**:
- `src/SalesChatbot/Services/TextToSqlService.cs` — replaced both ambiguous entries with a single rolling-window entry:
  ```
  "last month" -> OrderDate >= DATEADD(DAY,-30,GETDATE())
  ```

---

## 5. Grouped Results Fix (US5)

**What changed**: Previously, grouped queries (e.g. revenue per category) were capped and not all groups were shown.

**How fixed**: `DeterministicResultFormatter` (see item 3) iterates `queryResult.Rows` directly — no `Take()` — so every group returned by the database is rendered as a table row.

---

## 6. Row Cap Removal (US6)

**What changed**: A `Take(50)` / `Take(1000)` cap in the result formatter was limiting output rows regardless of query result size.

**How fixed**: `DeterministicResultFormatter` renders all rows passed in `QueryResult.Rows`. The SQL layer still enforces `SELECT TOP 500` via the LLM prompt's `ROW CAP` business rule, which is the appropriate place to cap at the database level.

---

## Test Suite Results

| Suite | Tests | Result |
|---|---|---|
| `SalesChatbot.UnitTests` | 97 | Pass |
| `SalesChatbot.IntegrationTests` | Skipped (LocalDB absent in CI) | Skip (expected) |

---

## Manual Step Required (T031)

Apply the EF migration before running the app:

```powershell
cd poc-app/ai-upskilling-poc
dotnet ef database update --project src/SalesChatbot
dotnet run --project src/SalesChatbot
```

Follow the end-to-end validation checklist in `specs/002-week3-improvements/quickstart.md`.
