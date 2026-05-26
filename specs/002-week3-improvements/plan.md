# Implementation Plan: Week 3 Chatbot Improvements Sprint

**Branch**: `002-week3-improvements` | **Date**: 2026-05-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-week3-improvements/spec.md`

## Summary

Six targeted improvements to the NL-to-SQL sales chatbot based on Zbigniew demo feedback:
(1) persist every AI-generated SQL query in a database audit log exposed via `GET /api/audit`;
(2) layer a standalone regex-based SQL safety filter plus a secondary LLM validator before execution;
(3) replace the second DIAL call in `ResultInterpreterService` with a deterministic C# formatter;
(4) fix the "last month" prompt entry to always resolve to a rolling 30-day window;
(5) ensure grouped results are always displayed as complete markdown tables;
(6) remove the implicit row cap in the formatter so all result rows are rendered.

## Technical Context

**Language/Version**: C# 12 / .NET 8

**Primary Dependencies**: ASP.NET Core 8 Minimal API, Blazor Server, Entity Framework Core 8, NSubstitute, xUnit, FluentAssertions

**Storage**: SQL Server LocalDB via EF Core 8 — existing `SalesDbContext`, new `QueryAuditEntry` entity + migration

**Testing**: xUnit + FluentAssertions + NSubstitute; integration tests use `SqlServerFactAttribute` to skip gracefully when DB is absent

**Target Platform**: Windows / Linux server (LocalDB for dev, SQL Server for prod)

**Project Type**: Web service (Minimal API + Blazor Server)

**Performance Goals**: Audit writes are fire-and-forget async; GET /api/audit returns last 50 rows with no pagination needed

**Constraints**: All DIAL calls route exclusively through `IDialClient`; no new AI providers introduced; all new services registered as `Scoped`

**Scale/Scope**: Single-user PoC; audit table grows at ~1 row per chatbot query

## Constitution Check

*The project constitution is not yet populated (template only). Architecture constraints are derived from the user-provided spec arguments and existing codebase patterns.*

**Gate: Interface-first design** — All new services must have interfaces. ✅ `IAuditService`, `IQueryValidatorService`, `IDeterministicResultFormatter` (or reuse `IResultInterpreterService`)

**Gate: DIAL call routing** — All LLM calls must go through `IDialClient`. ✅ `QueryValidatorService` injects `IDialClient`; `DeterministicResultFormatter` makes zero DIAL calls.

**Gate: Unit test coverage** — All new services must have unit tests. ✅ Planned for `DeterministicResultFormatter`, `QueryValidatorService`, `AuditService`.

**Gate: Integration test compatibility** — Integration tests skip gracefully when DB absent. ✅ Existing `SqlServerFactAttribute` pattern preserved.

**No constitution violations.**

## Project Structure

### Documentation (this feature)

```text
specs/002-week3-improvements/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── audit-api.md
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
poc-app/ai-upskilling-poc/
├── src/SalesChatbot/
│   ├── Data/
│   │   ├── Entities/
│   │   │   └── QueryAuditEntry.cs          [NEW]
│   │   ├── Migrations/
│   │   │   └── <timestamp>_AddQueryAuditLog.cs  [NEW — EF migration]
│   │   └── SalesDbContext.cs               [MODIFY — add DbSet<QueryAuditEntry>]
│   ├── Services/
│   │   ├── Interfaces/
│   │   │   ├── IAuditService.cs            [NEW]
│   │   │   └── IQueryValidatorService.cs   [NEW]
│   │   ├── Validation/
│   │   │   └── SqlSafetyValidator.cs       [MODIFY — add ContainsBlockedKeyword()]
│   │   ├── AuditService.cs                 [NEW]
│   │   ├── QueryValidatorService.cs        [NEW]
│   │   ├── DeterministicResultFormatter.cs [NEW — replaces ResultInterpreterService LLM call]
│   │   ├── ResultInterpreterService.cs     [KEEP — kept for reference/rollback]
│   │   ├── ConversationService.cs          [MODIFY — inject IAuditService, add timing + audit calls]
│   │   └── TextToSqlService.cs             [MODIFY — inject IQueryValidatorService; fix "last month" prompt]
│   ├── Models/
│   │   └── SqlGenerationResult.cs          [MODIFY — add RawSql property for audit capture]
│   ├── Api/
│   │   └── ChatEndpoints.cs                [MODIFY — add GET /api/audit endpoint]
│   └── Program.cs                          [MODIFY — register IAuditService, IQueryValidatorService, DeterministicResultFormatter]
└── tests/
    ├── SalesChatbot.UnitTests/
    │   └── Services/
    │       ├── DeterministicResultFormatterTests.cs  [NEW]
    │       ├── QueryValidatorServiceTests.cs         [NEW]
    │       └── AuditServiceTests.cs                  [NEW — or integration test]
    └── SalesChatbot.IntegrationTests/
        └── AuditEndpointTests.cs                     [NEW — skip when DB absent]
```

**Structure Decision**: Single-project layout (existing Option 1). All new files follow the established `src/SalesChatbot/Services/` and `tests/` conventions.

## Complexity Tracking

No constitution violations — no complexity justification required.
