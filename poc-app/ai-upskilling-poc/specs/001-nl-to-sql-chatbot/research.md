# Research: NL-to-SQL Sales Chatbot PoC

**Feature**: `001-nl-to-sql-chatbot` | **Date**: 2026-05-18

## 1. EPAM DIAL OpenAI-Compatible Integration

**Decision**: Implement `IDialClient` using `HttpClient` against DIAL's `/openai/deployments/{model}/chat/completions` endpoint with API-key header authentication; model `gpt-4o`; request/response shaped per OpenAI chat completions schema.

**Rationale**: DIAL exposes an OpenAI-compatible surface, so a thin adapter keeps prompts testable via NSubstitute mocks without pulling the full OpenAI SDK. Configuration (`Dial:Endpoint`, `Dial:ApiKey`, `Dial:Deployment`) lives in `appsettings` / user secrets / CI secrets—never hard-coded.

**Alternatives considered**:
- *Azure OpenAI SDK*: Not applicable; PoC targets EPAM DIAL specifically.
- *Semantic Kernel*: Adds orchestration weight beyond YAGNI for two fixed prompt templates.

## 2. Blazor Server + Minimal API Single Host

**Decision**: One ASP.NET Core 8 project registers both `MapRazorComponents<App>()` (interactive server) and Minimal API chat endpoints. UI components inject `IConversationService` directly; API endpoints delegate to the same service for integration tests.

**Rationale**: Matches user stack requirement (single solution, single host). Blazor Server avoids SPA build complexity while delivering real-time chat UX. Minimal API provides a stable HTTP contract without a second deployable unit.

**Alternatives considered**:
- *Separate API + Blazor WASM*: Two client/server concerns and CORS; exceeds PoC simplicity.
- *SignalR hub only*: Duplicates Minimal API surface; harder to test with standard HTTP clients.

## 3. Conversation Session & 10-Exchange Cap

**Decision**: Store exchanges in a scoped `ConversationSession` object owned by `ConversationService`. Each exchange = `{ UserMessage, AssistantMessage, Timestamp }`. When building LLM prompts, include at most the **10 most recent completed exchanges** (20 messages). `Reset()` clears the list.

**Rationale**: Scoped lifetime aligns with Blazor Server circuit scope; refresh naturally resets. Cap bounds token usage and latency while supporting multi-turn follow-ups (User Story 2).

**Alternatives considered**:
- *IMemoryCache with session cookie*: Needed only if API clients require cross-request continuity without Blazor; defer unless integration tests require it—API tests can send `X-Session-Id` header with scoped factory for test host.
- *Unlimited history*: Violates stated cap and risks context overflow.

## 4. Dual SELECT-Only SQL Validation

**Decision**: Shared static `SqlSafetyValidator` used by both `TextToSqlService` (post-generation) and `SqlExecutionService` (pre-execution):

1. Trim and reject empty input.
2. Reject exact sentinel mishandling (only allowed as generation outcome, not executable SQL).
3. Strip leading SQL comments (`--`, `/* */`) then require statement to start with `SELECT` (case-insensitive).
4. Block whole-word forbidden tokens via regex: `INSERT`, `UPDATE`, `DELETE`, `DROP`, `CREATE`, `ALTER`, `TRUNCATE`, `MERGE`, `EXEC`, `EXECUTE`, `GRANT`, `REVOKE`, `INTO` (when not part of allowed pattern—conservative block on `;` multi-statement).
5. Reject multiple statements (semicolon not inside string literals—use simple semicolon check for PoC).

**Rationale**: Constitution mandates defense in depth. Shared validator prevents rule drift between layers.

**Alternatives considered**:
- *TSQL parser library*: Heavy dependency for PoC; regex + keyword block sufficient with dual layers.
- *Validate only at execution*: Fails constitution gate.

## 5. 500-Row Cap Enforcement

**Decision**: `SqlExecutionService` wraps or rewrites the query to guarantee ≤ 500 rows. Preferred approach: if generated SQL has no `TOP`, inject `TOP 500` immediately after `SELECT` / `SELECT DISTINCT` via safe string rewrite; if `TOP n` present with n > 500, clamp to 500.

**Rationale**: Constitution requires enforcement even when LLM omits `TOP`. Server-side rewrite is deterministic (temp=0 SQL helps consistency).

**Alternatives considered**:
- *Rely on prompt instruction only*: Insufficient per constitution.
- *Abort when TOP missing*: Poor UX; spec expects answers for multi-row queries with summary.

## 6. Raw DbCommand via EF Connection

**Decision**: Inject `SalesDbContext`; in `SqlExecutionService`, use `await context.Database.OpenConnectionAsync(ct)`, create command from `context.Database.GetDbConnection().CreateCommand()`, set `CommandText` to validated SQL, execute `ExecuteReaderAsync`, map to `QueryResult` (column names + row dictionaries). Always use parameterized path only for LLM SQL as literal (validated SELECT)—no user string concatenation into SQL outside LLM output.

**Rationale**: Satisfies constitution: EF owns connection/schema; raw ADO.NET isolated to untrusted SQL path.

**Alternatives considered**:
- *FromSqlRaw on DbSet*: Couples to entity types; LLM generates ad-hoc projections.
- *Separate SqlConnection*: Bypasses EF connection management.

## 7. SQL Server LocalDB & Seed Data

**Decision**: Connection string `(localdb)\mssqllocaldb` database `SalesChatbot` for development. EF Core migrations create schema; `SalesDataSeeder` inserts realistic Orders/Customers/Products/OrderItems covering Germany/France customers, Electronics products, mixed order statuses, and Completed orders for revenue scenarios.

**Rationale**: LocalDB ships with Visual Studio / SQL tooling on Windows; integration tests skip when unavailable (constitution). GitHub Actions CI runs unit tests only unless SQL service container is added later—integration tests use `[Fact(Skip = "...")]` pattern or custom `SqlServerFact` attribute checking connectivity.

**Alternatives considered**:
- *SQLite*: Different T-SQL semantics (`TOP`, date functions) would diverge from production-like SQL Server target.
- *Docker SQL in CI for every push*: Valid future enhancement; not required for initial PoC gate.

## 8. CANNOT_ANSWER & Deflection Mapping

**Decision**: LLM returns exact string `CANNOT_ANSWER` when question is out of scope, requires writes, or cannot map to schema. `ConversationService` maps to spec deflection copy: *"I can only answer questions about sales data. Please ask about orders, customers, or products."* Validation failures also yield deflection (never raw SQL errors to user).

**Rationale**: Constitution sentinel consistency; spec User Story 3 acceptance scenarios.

**Alternatives considered**:
- *Different messages per failure type*: Acceptable UX enhancement inside same sentinel path; keep internal sentinel unified.

## 9. Prompt Content for Business Rules

**Decision**: Embed in SQL-generation system prompt:

- Schema DDL or column list for four tables and relationships.
- Revenue = `SUM(oi.Quantity * oi.UnitPrice)` for `Orders.Status = 'Completed'` only.
- Order counts include all statuses unless user filters.
- Time defaults: last month = 30 days rolling; this month = MTD calendar; this quarter = QTD calendar; recently = 7 days.
- Output: single T-SQL SELECT or `CANNOT_ANSWER`.

Result interpretation prompt receives question, row count, up to 5 sample rows, aggregates if single-value, and instructions for EUR formatting and count + top-summary pattern.

**Rationale**: Encodes spec clarifications without hard-coding every question.

## 10. Testing Strategy

**Decision**:

- **Unit tests**: Mock `IDialClient` for TextToSql and ResultInterpreter; mock `SalesDbContext`/connection for SqlExecution; test `SqlSafetyValidator` exhaustively with forbidden verbs and edge cases.
- **Integration tests**: Full pipeline against LocalDB when present; `[SqlServerFact]` skips otherwise.
- **Assertions**: FluentAssertions throughout.

**Rationale**: Constitution testability requirements; CI must pass without DB.

## 11. GitHub Actions CI

**Decision**: Workflow on push/PR to main: checkout → setup-dotnet 8.x → `dotnet restore` → `dotnet build -c Release` → `dotnet test -c Release --no-build` → `dotnet format --verify-no-changes`.

**Rationale**: Constitution Principle VII; format gate prevents drift.

**Alternatives considered**:
- *Separate lint job*: Single job sufficient for PoC size.

## 12. Async & Cancellation

**Decision**: All service public methods signature includes `CancellationToken cancellationToken = default`. `DialClient` passes token to `HttpClient.SendAsync`; DB calls use `ExecuteReaderAsync(ct)` etc. No sync-over-async.

**Rationale**: Constitution Principle IV non-negotiable.

## Resolved Clarifications

All Technical Context fields are resolved; no `NEEDS CLARIFICATION` items remain.
