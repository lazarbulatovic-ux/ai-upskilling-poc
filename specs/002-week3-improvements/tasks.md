# Tasks: Week 3 Chatbot Improvements Sprint

**Input**: Design documents from `specs/002-week3-improvements/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/audit-api.md ✅

**Tests**: Unit and integration test tasks are included throughout, following the project's existing xUnit + FluentAssertions + NSubstitute pattern.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no blocking dependencies)
- **[Story]**: User story label (US1–US6) mapping to spec.md
- Exact file paths relative to repo root are included in all descriptions

---

## Phase 1: Setup (Verify Baseline)

**Purpose**: Confirm the build and tests are green before any changes. No file modifications.

- [x] T001 Build the solution and confirm all existing tests pass: `dotnet build poc-app/ai-upskilling-poc` and `dotnet test poc-app/ai-upskilling-poc`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data model changes required by US1 (audit log). Must be complete before Phase 3.

**⚠️ CRITICAL**: The EF migration (T005) must be applied before integration tests that touch the DB can run.

- [x] T002 Add `RawSql` nullable property to `SqlGenerationResult` and update both factory methods to populate it in `poc-app/ai-upskilling-poc/src/SalesChatbot/Models/SqlGenerationResult.cs`
- [x] T003 Create `QueryAuditEntry` entity class with fields: `Id` (int PK), `TimestampUtc` (DateTime), `UserQuestion` (nvarchar 2000), `GeneratedSql` (nvarchar 4000), `WasBlocked` (bool), `RowCount` (int), `ExecutionMs` (long) in `poc-app/ai-upskilling-poc/src/SalesChatbot/Data/Entities/QueryAuditEntry.cs`
- [x] T004 Add `DbSet<QueryAuditEntry> QueryAuditLog` and EF model configuration (table name `QueryAuditLog`, max lengths, index on `TimestampUtc`) to `poc-app/ai-upskilling-poc/src/SalesChatbot/Data/SalesDbContext.cs`
- [x] T005 Generate EF Core migration `AddQueryAuditLog` via `dotnet ef migrations add AddQueryAuditLog --project src/SalesChatbot` from `poc-app/ai-upskilling-poc/`; verify the generated file in `poc-app/ai-upskilling-poc/src/SalesChatbot/Data/Migrations/`

**Checkpoint**: Foundation ready — audit entity and migration exist; user story implementation can begin.

---

## Phase 3: User Story 1 — Audit Trail for AI-Generated Queries (Priority: P1) 🎯 MVP

**Goal**: Every AI-generated SQL query is persisted in the database with timestamp, question, SQL, blocked flag, row count, and execution duration; readable via `GET /api/audit`.

**Independent Test**: After asking 3 questions in the chatbot (including one with a blocked keyword), `GET http://localhost:5000/api/audit` returns 3 entries. The blocked entry shows `wasBlocked: true`, the others `false` with non-zero `rowCount` and `executionMs`.

- [x] T006 [P] [US1] Create `IAuditService` interface with `Task LogAsync(QueryAuditEntry entry, CancellationToken ct = default)` in `poc-app/ai-upskilling-poc/src/SalesChatbot/Services/Interfaces/IAuditService.cs`
- [x] T007 [US1] Implement `AuditService` class: inject `SalesDbContext`, add entry, call `SaveChangesAsync`; wrap in try/catch and log warning on failure so audit errors never propagate to the caller in `poc-app/ai-upskilling-poc/src/SalesChatbot/Services/AuditService.cs`
- [x] T008 [US1] Inject `IAuditService` into `ConversationService`; add audit log calls: (a) after SQL generation fails with a non-null `RawSql` — log with `WasBlocked=true, RowCount=0, ExecutionMs=0`; (b) after successful query execution — log with `WasBlocked=false, RowCount=result.RowCount, ExecutionMs=<Stopwatch elapsed ms>` in `poc-app/ai-upskilling-poc/src/SalesChatbot/Services/ConversationService.cs`
- [x] T009 [US1] Add `GET /api/audit` endpoint: query `SalesDbContext.QueryAuditLog` ordered by `TimestampUtc DESC`, take 50, return as JSON array matching the contract in `contracts/audit-api.md`; add 503 handler for DB unavailable in `poc-app/ai-upskilling-poc/src/SalesChatbot/Api/ChatEndpoints.cs`
- [x] T010 [US1] Register `IAuditService` → `AuditService` as Scoped in `poc-app/ai-upskilling-poc/src/SalesChatbot/Program.cs`
- [x] T011 [P] [US1] Write unit tests for `AuditService`: mock `SalesDbContext`, verify `LogAsync` adds entry and calls `SaveChangesAsync`; verify exceptions are swallowed and a warning is logged in `poc-app/ai-upskilling-poc/tests/SalesChatbot.UnitTests/Services/AuditServiceTests.cs`
- [x] T012 [P] [US1] Write integration test for `GET /api/audit`: verify HTTP 200 and empty array when no queries logged; decorated with `[SqlServerFact]` to skip when DB absent in `poc-app/ai-upskilling-poc/tests/SalesChatbot.IntegrationTests/AuditEndpointTests.cs`

**Checkpoint**: `GET /api/audit` returns HTTP 200. After any chatbot query, an entry appears in the audit log.

---

## Phase 4: User Story 2 — SQL Safety Guardrails (Priority: P1)

**Goal**: A second layer of SQL safety — a standalone regex keyword check plus a secondary LLM validator — rejects dangerous SQL before database execution; all rejections are audit-logged.

**Independent Test**: Sending the message `"Drop the customers table"` returns a safe deflection message, the DB is untouched, and `GET /api/audit` shows the blocked entry.

- [x] T013 [US2] Add public static `ContainsBlockedKeyword(string? sql, out string? matchedKeyword)` method to `SqlSafetyValidator` using whole-word case-insensitive regex; add `ERASE` to the `ForbiddenTokens` array (verify `TRUNCATE` is already present) in `poc-app/ai-upskilling-poc/src/SalesChatbot/Services/Validation/SqlSafetyValidator.cs`
- [x] T014 [P] [US2] Create `IQueryValidatorService` interface with `Task<SqlValidationResult> ValidateAsync(string sql, CancellationToken ct = default)` in `poc-app/ai-upskilling-poc/src/SalesChatbot/Services/Interfaces/IQueryValidatorService.cs`
- [x] T015 [P] [US2] Create `SqlValidationResult` sealed record with `IsApproved` bool and `RejectionReason` nullable string, plus static factory methods `Approved()` and `Rejected(string reason)` in `poc-app/ai-upskilling-poc/src/SalesChatbot/Models/SqlValidationResult.cs`
- [x] T016 [US2] Implement `QueryValidatorService`: inject `IDialClient`; build a short system prompt (see research.md §2); call DIAL at temperature=0; parse response for `APPROVED` / `REJECTED:`; return `Rejected("Validator unavailable")` on exception (fail-closed) in `poc-app/ai-upskilling-poc/src/SalesChatbot/Services/QueryValidatorService.cs`
- [x] T017 [US2] Inject `IQueryValidatorService` into `TextToSqlService`; after `SqlSafetyValidator.IsValidSelect()` passes, call `validatorService.ValidateAsync(trimmed)`; on rejection return `SqlGenerationResult.Failure(reason, rawSql: trimmed)`; on approval return `SqlGenerationResult.Success(trimmed)` with `RawSql` set in `poc-app/ai-upskilling-poc/src/SalesChatbot/Services/TextToSqlService.cs`
- [x] T018 [US2] Register `IQueryValidatorService` → `QueryValidatorService` as Scoped in `poc-app/ai-upskilling-poc/src/SalesChatbot/Program.cs`
- [x] T019 [P] [US2] Write unit tests for new `ContainsBlockedKeyword()` method covering: null input, ERASE keyword, DROP keyword, valid SELECT returns false, whole-word matching (e.g. `"EXECUTOR"` must NOT match `EXEC`) in `poc-app/ai-upskilling-poc/tests/SalesChatbot.UnitTests/Validation/SqlSafetyValidatorTests.cs`
- [x] T020 [P] [US2] Write unit tests for `QueryValidatorService`: mock `IDialClient` returning `"APPROVED"` → `IsApproved=true`; mock returning `"REJECTED: harmful"` → `IsApproved=false`; mock throwing exception → `IsApproved=false` (fail-closed) in `poc-app/ai-upskilling-poc/tests/SalesChatbot.UnitTests/Services/QueryValidatorServiceTests.cs`

**Checkpoint**: Blocked-keyword queries and LLM-validator rejections are deflected and audit-logged with `wasBlocked: true`.

---

## Phase 5: User Stories 6 + 4 + 5 — Deterministic Formatter / Grouped Results / Row Cap (Priority: P3 / P2 / P2)

**Note on coupling**: US4 (grouped results), US5 (no row cap), and US6 (single DIAL call) share a single implementation component — `DeterministicResultFormatter`. US6 is the prerequisite; US4 and US5 are automatically satisfied by implementing the formatter correctly. This phase implements all three.

**Goal**: Replace the second DIAL call in `ResultInterpreterService` with a deterministic C# formatter that renders a complete markdown table of ALL rows with no row limit.

**Independent Test**: Asking `"What is the revenue per product category?"` returns a markdown table listing every product category (not just 5). Asking the same question twice produces identical output. Application logs show no second DIAL call.

- [x] T021 [US6] Create `DeterministicResultFormatter` class implementing `IResultInterpreterService`; implement format detection: (a) zero rows → one "no results" sentence; (b) single value (1 row, 1 column) → one sentence with label+value; (c) all other cases → full markdown table with ALL rows from `queryResult.Rows` (no `Take()`) in `poc-app/ai-upskilling-poc/src/SalesChatbot/Services/DeterministicResultFormatter.cs`
- [x] T022 [US6] Add formatting helpers to `DeterministicResultFormatter`: (a) `HumaniseHeader(string)` — inserts space before each uppercase letter following a lowercase letter; (b) `FormatValue(string columnName, object? value)` — `DateTime` → `"d MMM yyyy"`; currency columns (`*Revenue*`, `*Price*`, `*Total*`, `*Amount*`, `*Spent*`, `*Cost*`) → `€` prefix with comma-thousands; `null` → empty string; all other types → `.ToString()` in `poc-app/ai-upskilling-poc/src/SalesChatbot/Services/DeterministicResultFormatter.cs`
- [x] T023 [US6] Update `Program.cs` to register `DeterministicResultFormatter` as the `IResultInterpreterService` implementation (replacing `ResultInterpreterService`); keep `ResultInterpreterService` registered as itself (not as interface) for rollback availability in `poc-app/ai-upskilling-poc/src/SalesChatbot/Program.cs`
- [x] T024 [P] [US6] Write unit tests for format detection in `DeterministicResultFormatter`: zero-row result → no-results sentence; single-value result → one sentence; multi-row result → markdown table starting with `|`; table includes all rows (test with 75-row mock result, verify 75 data rows in output) in `poc-app/ai-upskilling-poc/tests/SalesChatbot.UnitTests/Services/DeterministicResultFormatterTests.cs`
- [x] T025 [P] [US4] Write unit tests for grouped-result rendering: create a `QueryResult` with 10 distinct group rows (e.g. 10 categories); verify all 10 appear in the markdown table output in `poc-app/ai-upskilling-poc/tests/SalesChatbot.UnitTests/Services/DeterministicResultFormatterTests.cs`
- [x] T026 [P] [US5] Write unit tests for row-cap removal: create a `QueryResult` with 75 rows; call `FormatAsync`; count `|` rows in output and assert exactly 75 data rows are present in `poc-app/ai-upskilling-poc/tests/SalesChatbot.UnitTests/Services/DeterministicResultFormatterTests.cs`
- [x] T027 [P] [US6] Write unit tests for value formatting helpers: currency column with `decimal` value 18450 → `"€18,450"`; date value `2026-05-18` → `"18 May 2026"`; `null` → empty string; column header `"TotalRevenue"` → `"Total Revenue"` in `poc-app/ai-upskilling-poc/tests/SalesChatbot.UnitTests/Services/DeterministicResultFormatterTests.cs`

**Checkpoint**: All grouped and multi-row queries display complete markdown tables. No second DIAL call appears in logs. Row count in response matches row count in DB.

---

## Phase 6: User Story 3 — Accurate "Last Month" Date Queries (Priority: P2)

**Goal**: The NL-to-SQL prompt maps "last month" to a rolling 30-day window (`DATEADD(DAY,-30,GETDATE())`) not calendar-month logic.

**Independent Test**: Ask `"How many orders were placed last month?"` and verify the `generatedSql` field in `GET /api/audit` contains `DATEADD(DAY,-30,GETDATE())` and does NOT contain `MONTH(DATEADD(MONTH`.

- [x] T028 [US3] In `TextToSqlService.SystemPrompt`, locate the TIME PHRASE DICTIONARY section and replace both existing "last month" entries (the MONTH()/YEAR()-based variants) with a single entry: `"last month" -> OrderDate >= DATEADD(DAY,-30,GETDATE())`; ensure the few-shot example for "How many orders were placed last month?" is consistent in `poc-app/ai-upskilling-poc/src/SalesChatbot/Services/TextToSqlService.cs`

**Checkpoint**: The audit log for a "last month" question shows `DATEADD(DAY,-30,GETDATE())` in the generated SQL.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, test hygiene, and documentation consistency.

- [x] T029 [P] Run the full unit test suite and confirm all tests pass: `dotnet test poc-app/ai-upskilling-poc/tests/SalesChatbot.UnitTests`
- [x] T030 [P] Run the full integration test suite: `dotnet test poc-app/ai-upskilling-poc/tests/SalesChatbot.IntegrationTests` (tests skip gracefully if LocalDB is absent)
- [ ] T031 Apply the `AddQueryAuditLog` migration to LocalDB and perform manual validation using the checklist in `specs/002-week3-improvements/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **blocks Phase 3** (audit log needs entity + migration)
- **Phase 3 (US1 - Audit)**: Depends on Phase 2 — can start as soon as T005 is done
- **Phase 4 (US2 - Guardrails)**: Depends on Phase 2 (needs `RawSql` from T002) — **can run in parallel with Phase 3**
- **Phase 5 (US4+5+6 - Formatter)**: Depends only on Phase 1 — **can run in parallel with Phases 3 and 4**
- **Phase 6 (US3 - Date Fix)**: Depends only on Phase 1 — **can start anytime after T001**
- **Phase 7 (Polish)**: Depends on all prior phases

### User Story Dependencies

| Story | Depends On | Can Parallelize With |
|---|---|---|
| US1 — Audit Trail | Phase 2 (T002–T005) | US2, US3, US6 |
| US2 — SQL Guardrails | T002 (RawSql) | US1, US3, US6 |
| US3 — Last Month Fix | Phase 1 only | US1, US2, US4, US5, US6 |
| US4 — Grouped Results | T021 (formatter) | US1, US2, US3 |
| US5 — Row Cap Fix | T021 (formatter) | US1, US2, US3 |
| US6 — Single DIAL Call | Phase 1 only | US1, US2, US3 |

### Within Each Phase

- All `[P]`-marked tasks can run in parallel (different files, no inter-task dependency)
- Within US1: T006 → T007 → T008 (ConversationService depends on IAuditService and AuditService)
- Within US2: T014 and T015 are parallel; T016 depends on T014 and T015; T017 depends on T016
- Within US6: T022 is within the same file as T021 (implement together or sequentially)

---

## Parallel Opportunities Per Story

```text
# Phase 2 (Foundational) — sequential order:
T002 → T003 → T004 → T005

# Phase 3 (US1) — partial parallel:
T006 [P]
T007 (after T006)
T008 (after T007)
T009 [P] (same time as T006, different file)
T010 (after T007)
T011 [P] (same time as T006–T010, test file)
T012 [P] (same time as T006–T010, different test file)

# Phase 4 (US2) — partial parallel:
T013
T014 [P] + T015 [P]  ← parallel pair
T016 (after T014, T015)
T017 (after T016)
T019 [P] + T020 [P]  ← parallel pair (test files, any time)

# Phase 5 (US6+4+5) — sequential then parallel:
T021 → T022 → T023
T024 [P] + T025 [P] + T026 [P] + T027 [P]  ← all test tasks in parallel

# Phase 6 (US3) — single task:
T028  (independent, can overlap with any other phase)
```

---

## Implementation Strategy

### MVP First (US1 + US2 — the two P1 stories)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T005)
3. Complete Phase 3: US1 Audit Trail (T006–T012)
4. **STOP and VALIDATE**: `GET /api/audit` returns entries; blocked entry shows `wasBlocked: true`
5. Complete Phase 4: US2 SQL Guardrails (T013–T020)
6. **STOP and VALIDATE**: Blocked-keyword query is deflected and logged

### Full Sprint Delivery

7. Complete Phase 5: DeterministicResultFormatter (T021–T027) — fixes US6, US4, US5
8. **STOP and VALIDATE**: Revenue-by-category shows all categories; no second DIAL call in logs
9. Complete Phase 6: Last Month Fix (T028)
10. **STOP and VALIDATE**: "Last month" audit entry shows `DATEADD(DAY,-30,GETDATE())`
11. Complete Phase 7: Polish (T029–T031)

### Parallel Team Strategy

With 2 developers:
- **Developer A**: Phases 2 → 3 (Foundational + Audit Trail)
- **Developer B**: Phase 5 (DeterministicResultFormatter) while A does Phase 3
- After both done: A or B picks up Phase 4 (Guardrails) and Phase 6 (Date Fix)

---

## Notes

- `[P]` = different file, no incomplete-task dependency — safe to run in parallel
- `[Story]` label maps each task to a user story for traceability
- `ResultInterpreterService` is **preserved** (not deleted); only the DI registration changes — rollback is a one-line change in `Program.cs`
- Integration tests use existing `[SqlServerFact]` attribute to skip when LocalDB is absent
- Commit after each checkpoint to preserve a working state
