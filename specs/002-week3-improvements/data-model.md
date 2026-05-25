# Data Model: Week 3 Chatbot Improvements Sprint

## New Entity: QueryAuditEntry

Persists one record per AI-generated SQL query attempt. Written by `AuditService` after every query that produces an actual SQL string (deflections like `CANNOT_ANSWER` are not recorded).

### Fields

| Field | Type | Constraints | Description |
|---|---|---|---|
| `Id` | `int` | PK, identity | Auto-increment surrogate key |
| `TimestampUtc` | `DateTime` | NOT NULL, indexed | UTC timestamp of the query attempt |
| `UserQuestion` | `nvarchar(2000)` | NOT NULL | Original natural language question from the user |
| `GeneratedSql` | `nvarchar(4000)` | NOT NULL | The SQL text produced by the LLM (post-fence-strip, pre-execution) |
| `WasBlocked` | `bit` | NOT NULL, default 0 | True if the query was rejected by safety guardrails or LLM validator |
| `RowCount` | `int` | NOT NULL, default 0 | Number of rows returned by execution; 0 if blocked |
| `ExecutionMs` | `bigint` | NOT NULL, default 0 | Wall-clock time in milliseconds for query execution; 0 if blocked |

### EF Core Configuration (in `SalesDbContext.OnModelCreating`)

```csharp
modelBuilder.Entity<QueryAuditEntry>(entity =>
{
    entity.ToTable("QueryAuditLog");
    entity.Property(e => e.UserQuestion).HasMaxLength(2000).IsRequired();
    entity.Property(e => e.GeneratedSql).HasMaxLength(4000).IsRequired();
    entity.Property(e => e.TimestampUtc).IsRequired();
    entity.HasIndex(e => e.TimestampUtc);
});
```

### Migration

New EF Core migration: `AddQueryAuditLog`
- Creates table `QueryAuditLog` with all columns above
- Adds index on `TimestampUtc` for ordered retrieval

---

## Modified Entity: SqlGenerationResult

Adds `RawSql` property so the raw SQL text is available for audit logging even when the generation result represents a failure.

### New property

| Property | Type | When populated |
|---|---|---|
| `RawSql` | `string?` | Set on both Success and Failure paths in `TextToSqlService.GenerateSqlAsync()` after stripping markdown fences; `null` only for pure `CANNOT_ANSWER` deflections |

### Updated factory methods

```csharp
// Existing (unchanged interface)
public static SqlGenerationResult Success(string sql) =>
    new() { IsSuccess = true, Sql = sql, RawSql = sql };

public static SqlGenerationResult Failure(string reason, string? rawSql = null) =>
    new() { IsSuccess = false, FailureReason = reason, RawSql = rawSql };
```

---

## New Value Objects / Records

### SqlValidationResult

Returned by `IQueryValidatorService.ValidateAsync()`.

```csharp
public sealed record SqlValidationResult(bool IsApproved, string? RejectionReason = null)
{
    public static SqlValidationResult Approved() => new(true);
    public static SqlValidationResult Rejected(string reason) => new(false, reason);
}
```

---

## Relationship Diagram

```
SalesDbContext
├── DbSet<Customer>          (existing)
├── DbSet<Product>           (existing)
├── DbSet<SalesOrder>        (existing)
├── DbSet<OrderItem>         (existing)
└── DbSet<QueryAuditEntry>   [NEW] — no FK to other tables
```

`QueryAuditEntry` is a standalone log table with no foreign keys. It records user questions and generated SQL independently of the sales data domain.

---

## IAuditService Interface

```csharp
public interface IAuditService
{
    Task LogAsync(QueryAuditEntry entry, CancellationToken cancellationToken = default);
}
```

`AuditService` implementation:
- Receives `SalesDbContext` via constructor injection (Scoped)
- Adds the entry and calls `SaveChangesAsync`
- Does not throw on failure — wraps in try/catch and logs warning (audit failure must not break the chatbot response)

---

## IQueryValidatorService Interface

```csharp
public interface IQueryValidatorService
{
    Task<SqlValidationResult> ValidateAsync(string sql, CancellationToken cancellationToken = default);
}
```

`QueryValidatorService` implementation:
- Receives `IDialClient` via constructor injection (Scoped)
- Calls DIAL at temperature=0 with a short system prompt (see research.md §2)
- Returns `SqlValidationResult.Approved()` or `SqlValidationResult.Rejected(reason)`
- On DIAL call failure: returns `Rejected("Validator unavailable")` — fail-closed
