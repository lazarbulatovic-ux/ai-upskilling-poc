# Research: Week 3 Chatbot Improvements Sprint

## 1. DeterministicResultFormatter — Format Detection Logic

**Decision**: Implement a deterministic formatter that classifies `QueryResult` by shape and applies fixed rules.

**Classification rules** (derived from existing `ResultInterpreterService` PROMPT_V2 logic, now codified in C#):

| Result shape | Detection | Output |
|---|---|---|
| Zero rows | `RowCount == 0` | One sentence: "No [entity] were found matching your query." |
| Single value | `RowCount == 1 && ColumnNames.Count == 1` | One sentence with column name + formatted value |
| Single row, multiple columns | `RowCount == 1 && ColumnNames.Count > 1` | Markdown table, 1 data row |
| Multiple rows | `RowCount > 1` | Full markdown table — ALL rows, no Take() |

**Column header humanisation** (PascalCase/camelCase → human-readable):
- `OrderDate` → `Order Date`
- `TotalRevenue` → `Total Revenue`
- `CustomerCount` → `Customer Count`
- Rule: insert a space before each uppercase letter that follows a lowercase letter (regex `([a-z])([A-Z])` → `$1 $2`).

**Value formatting**:
- `decimal` or `double` values > 999: comma thousands separator (e.g., `1,234.56`)
- `decimal` or `double` values in columns named `*Revenue*`, `*Price*`, `*Total*`, `*Amount*`, `*Spent*`, `*Cost*`: prefix `€`
- `DateTime` / `DateTimeOffset`: format as `"d MMM yyyy"` (e.g., `18 May 2026`)
- `null`: render as empty cell (no "null" text)
- All other types: `.ToString()`

**Rationale**: The LLM-based formatter was producing inconsistent output (showed 5 rows instead of all, inconsistent table formatting). A deterministic formatter guarantees identical output for identical inputs, removes the second DIAL call (latency + cost), and fixes grouped results and row cap in a single change.

**Alternatives considered**:
- Keep LLM formatter but improve prompt: rejected — prompt V2 already attempted this and still produced incomplete grouped tables in the demo.
- Template engine (e.g., Scriban): rejected — overkill for simple table rendering.

---

## 2. IQueryValidatorService — Second LLM Validation

**Decision**: Add `IQueryValidatorService` as a second validation gate called from `TextToSqlService` after `SqlSafetyValidator.IsValidSelect()` passes.

**Prompt design** (temperature=0):
```
System: You are a SQL safety reviewer. Given a T-SQL SELECT statement, respond with exactly:
  APPROVED — if the query is a safe read-only SELECT against a sales database
  REJECTED: <one-line reason> — if the query is potentially harmful or malformed

Respond with nothing else.
```

**Input**: The cleaned SQL string that passed the static safety filter.

**Output handling**:
- Starts with `APPROVED` → `SqlValidationResult.Approved()`
- Starts with `REJECTED` → `SqlValidationResult.Rejected(reason)`
- Anything else → treat as `REJECTED: Unexpected validator response` (defensive)

**Call location**: `TextToSqlService.GenerateSqlAsync()` — after static validation, before returning `SqlGenerationResult.Success()`.

**Rationale**: The static keyword filter blocks obvious mutations but cannot detect subtler semantic attacks (e.g., subquery-based exfiltration patterns). A second LLM call at temperature=0 provides a second opinion with near-zero added latency (same DIAL endpoint, tiny prompt).

**Alternatives considered**:
- Call from `ConversationService`: rejected — validation belongs in the same service that generated the SQL.
- Use a rule-engine (e.g., parse SQL AST): rejected — adds a new library dependency for marginal gain over the LLM validator.

---

## 3. QueryAuditEntry — What to Record When SQL Is Blocked

**Decision**: Extend `SqlGenerationResult` with a `RawSql` property (nullable) so that even failed/blocked attempts can be recorded in the audit log.

**Current behaviour**: `SqlGenerationResult.Failure(reason)` does not carry the raw SQL text. The audit service needs the actual SQL string to populate the `GeneratedSql` column.

**New property**:
```csharp
public string? RawSql { get; init; }
```
Set in `TextToSqlService.GenerateSqlAsync()` on both success and failure paths (after stripping markdown fences). For `CANNOT_ANSWER` deflections (not SQL at all), `RawSql` is set to `null` and no audit entry is written.

**Audit write timing**: `ConversationService.SendMessageAsync()` writes the audit entry after:
1. SQL generation fails (blocked) — `WasBlocked = true, RowCount = 0, ExecutionMs = 0`
2. SQL execution completes — `WasBlocked = false, RowCount = result.RowCount, ExecutionMs = <measured>`

**Do not audit**: deflections where `sqlResult.Sql == null && sqlResult.RawSql == null` (i.e., `CANNOT_ANSWER` responses — no SQL was ever generated).

---

## 4. "Last Month" Prompt Fix

**Problem found in `TextToSqlService.SystemPrompt`**: The TIME PHRASE DICTIONARY has two contradictory entries for "last month":
```
"last month"   -> MONTH(OrderDate)=MONTH(DATEADD(MONTH,-1,GETDATE())) AND YEAR(OrderDate)=YEAR(GETDATE())
"last month"
(calendar)     -> MONTH(OrderDate)=MONTH(DATEADD(MONTH,-1,GETDATE())) AND YEAR(OrderDate)=YEAR(DATEADD(MONTH,-1,GETDATE()))
```
Both use calendar-month logic. The existing few-shot example already uses `DATEADD(DAY,-30,GETDATE())` which is the correct rolling-window approach.

**Fix**: Replace both conflicting "last month" entries with a single unambiguous entry:
```
"last month"   -> OrderDate >= DATEADD(DAY,-30,GETDATE())
```
Also align the few-shot example (already correct) to be consistent with the dictionary.

**Rationale**: On the 1st of a month, `MONTH(DATEADD(MONTH,-1,GETDATE()))` returns the previous calendar month (correct), but on the 31st, the same expression still returns the previous calendar month (missing Jan 31 if queried on Feb 28). A rolling 30-day window is simpler, unambiguous, and consistent with user expectations.

---

## 5. Row Cap Removal

**Finding**: The `Take(50)` mentioned in the spec does not exist in the current `ResultInterpreterService` (it was upgraded to `Take(1000)` in a prior commit). However, by replacing `ResultInterpreterService` with `DeterministicResultFormatter`, no `Take()` of any kind is applied — the formatter iterates all of `queryResult.Rows` directly.

**Note on SQL-level cap**: `SqlExecutionService.ExecuteQueryAsync()` calls `SqlSafetyValidator.EnforceRowLimit()` which injects `TOP 500` into the SQL. This SQL-level cap is intentional (prevents runaway queries from flooding the DB connection) and is **not** removed. The formatter-level cap is removed.

---

## 6. ContainsBlockedKeyword() Addition to SqlSafetyValidator

**Current state**: `SqlSafetyValidator.IsValidSelect()` already performs whole-word keyword matching using the `ForbiddenTokens` array. The method does more than just keyword checking (it also rejects non-SELECT statements, semicolons, etc.).

**Decision**: Add a new public static method `ContainsBlockedKeyword(string sql)` that performs ONLY the regex keyword check — usable independently of the full `IsValidSelect` pipeline. This satisfies the spec requirement for a standalone keyword-blocklist layer.

**Blocklist alignment**: The existing `ForbiddenTokens` array already covers all required keywords (DROP, DELETE, TRUNCATE, INSERT, UPDATE, ALTER, CREATE, EXEC/EXECUTE, XP_). Add `ERASE` and `TRUNCATE` if not already present (verify: TRUNCATE is present; ERASE is not — add it).

**Method signature**:
```csharp
public static bool ContainsBlockedKeyword(string? sql, out string? matchedKeyword)
```
Returns `true` if any blocked keyword is found (whole-word, case-insensitive), with `matchedKeyword` set.
