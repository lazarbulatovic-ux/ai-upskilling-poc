# Chat API Contract

**Feature**: `001-nl-to-sql-chatbot` | **Version**: 1.0.0 | **Date**: 2026-05-18

Minimal API endpoints exposed by `SalesChatbot` host. Blazor UI uses in-process `IConversationService`; these endpoints support integration testing and external clients.

**Base URL**: `{host}/api/chat`

**Authentication**: None (PoC)

---

## POST /api/chat/message

Submit a user message and receive an assistant reply.

### Request

```http
POST /api/chat/message HTTP/1.1
Content-Type: application/json
```

```json
{
  "message": "How many orders were placed last month?"
}
```

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| message | string | yes | Non-empty after trim; max 2000 chars |

### Response `200 OK`

```json
{
  "reply": "142 orders were placed in the last 30 days.",
  "sessionExchangeCount": 1
}
```

| Field | Type | Description |
|-------|------|-------------|
| reply | string | Plain-language assistant answer or deflection |
| sessionExchangeCount | int | Number of exchanges in current session (after this reply) |

### Response `400 Bad Request`

```json
{
  "error": "Message is required."
}
```

### Response `503 Service Unavailable`

Database unreachable; user-safe message without internal details.

```json
{
  "error": "Sales data is temporarily unavailable. Please try again later."
}
```

### Behavior

1. Appends user message to scoped session (trim history to 10 exchanges before LLM calls).
2. Runs text-to-SQL → validation → execution → interpretation pipeline.
3. Returns deflection copy when internal result is `CANNOT_ANSWER` or validation fails.
4. Honors `CancellationToken` on client disconnect.

---

## POST /api/chat/new

Reset the current session (equivalent to **New Chat** in UI).

### Request

```http
POST /api/chat/new HTTP/1.1
```

No body.

### Response `204 No Content`

Session cleared; subsequent messages start fresh context.

---

## Error & Deflection Semantics

| Condition | HTTP | User-visible `reply` / `error` |
|-----------|------|--------------------------------|
| Empty message | 400 | Validation error |
| Out-of-scope question | 200 | Deflection: sales data scope message |
| Write/delete request | 200 | Deflection: read-only message |
| SQL validation failure | 200 | Deflection (no SQL exposed) |
| Zero rows | 200 | Natural language "no data found" |
| DB unavailable | 503 | Generic unavailability message |

Internal sentinel `CANNOT_ANSWER` is never returned directly to clients.

---

## Service Interface Contracts (internal)

All public methods include `CancellationToken cancellationToken = default`.

### IDialClient

```csharp
Task<string> GetChatCompletionAsync(
    IReadOnlyList<ChatMessage> messages,
    double temperature,
    CancellationToken cancellationToken = default);
```

### ITextToSqlService

```csharp
Task<SqlGenerationResult> GenerateSqlAsync(
    string userQuestion,
    IReadOnlyList<ChatExchange> history,
    CancellationToken cancellationToken = default);
```

- Temperature: **0**
- Returns `CANNOT_ANSWER` via `IsSuccess = false`

### ISqlExecutionService

```csharp
Task<QueryResult> ExecuteQueryAsync(
    string sql,
    CancellationToken cancellationToken = default);
```

- Re-validates SELECT-only; enforces **500-row** cap

### IResultInterpreterService

```csharp
Task<string> InterpretAsync(
    string userQuestion,
    QueryResult queryResult,
    IReadOnlyList<ChatExchange> history,
    CancellationToken cancellationToken = default);
```

- Temperature: **0.3**
- Formats EUR currency; count + top-5 summary for multi-row

### IConversationService

```csharp
Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default);
void Reset();
IReadOnlyList<ChatExchange> GetHistory();
```

- Maintains **10-exchange** cap
- Orchestrates full pipeline

---

## DI Registration

All five services registered **Scoped** in `Program.cs`:

```csharp
builder.Services.AddScoped<IDialClient, DialClient>();
builder.Services.AddScoped<ITextToSqlService, TextToSqlService>();
builder.Services.AddScoped<ISqlExecutionService, SqlExecutionService>();
builder.Services.AddScoped<IResultInterpreterService, ResultInterpreterService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
```
