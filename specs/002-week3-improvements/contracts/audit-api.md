# API Contract: Audit Log Endpoint

## GET /api/audit

Returns the last 50 audit log entries ordered by most-recent first.

### Request

```
GET /api/audit
```

No query parameters. No request body. No authentication required (internal/admin-facing PoC endpoint).

### Response — 200 OK

```json
[
  {
    "id": 42,
    "timestampUtc": "2026-05-25T14:32:01.123Z",
    "userQuestion": "How many orders were placed last month?",
    "generatedSql": "SELECT COUNT(*) AS OrderCount FROM Orders WHERE OrderDate >= DATEADD(DAY,-30,GETDATE())",
    "wasBlocked": false,
    "rowCount": 1,
    "executionMs": 18
  },
  {
    "id": 41,
    "timestampUtc": "2026-05-25T14:31:45.000Z",
    "userQuestion": "Delete all orders from last year",
    "generatedSql": "DELETE FROM Orders WHERE YEAR(OrderDate) = YEAR(GETDATE())-1",
    "wasBlocked": true,
    "rowCount": 0,
    "executionMs": 0
  }
]
```

### Response fields

| Field | Type | Description |
|---|---|---|
| `id` | integer | Auto-increment surrogate key |
| `timestampUtc` | ISO 8601 string | UTC timestamp of the query attempt |
| `userQuestion` | string | Original natural language question |
| `generatedSql` | string | SQL produced by the LLM (may be a blocked attempt) |
| `wasBlocked` | boolean | `true` if rejected by safety filter or LLM validator |
| `rowCount` | integer | Rows returned; `0` if blocked |
| `executionMs` | integer | Execution duration in ms; `0` if blocked |

### Response — 200 OK (empty log)

```json
[]
```

### Error responses

| Status | Condition |
|---|---|
| `503 Service Unavailable` | Database not available (same pattern as `/api/chat/message`) |

### Implementation notes

- Returns at most 50 entries (`ORDER BY TimestampUtc DESC` + `.Take(50)`)
- No pagination for this sprint
- Mapped in `ChatEndpoints.cs` alongside the existing `/api/chat` routes
- Handler resolves `IAuditService` (or queries `SalesDbContext` directly for simplicity)

### Existing endpoints (unchanged)

| Verb | Path | Description |
|---|---|---|
| `POST` | `/api/chat/message` | Send a message to the chatbot |
| `POST` | `/api/chat/new` | Reset the conversation session |
| **`GET`** | **`/api/audit`** | **[NEW] Retrieve audit log** |
