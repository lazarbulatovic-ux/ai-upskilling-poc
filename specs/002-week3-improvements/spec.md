# Feature Specification: Week 3 Chatbot Improvements Sprint

**Feature Branch**: `002-week3-improvements`

**Created**: 2026-05-25

**Status**: Draft

**Input**: User description: "Week 3 improvements sprint — 6 improvements based on Zbigniew demo feedback: SQL audit log, SQL guardrails, single AI call, last-month date fix, grouped results fix, row cap removal."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Audit Trail for AI-Generated Queries (Priority: P1)

An administrator or compliance reviewer needs to see a full history of every question asked to the chatbot, the SQL it generated, whether it was blocked, how many rows were returned, and how long it took. This enables oversight, debugging, and accountability for AI-generated data access.

**Why this priority**: Without an audit trail, there is no way to investigate unexpected results, detect misuse, or demonstrate compliance. This is foundational to operating the chatbot responsibly.

**Independent Test**: A reviewer opens the audit log endpoint after a series of chatbot queries and sees one entry per query, each with all required fields populated correctly.

**Acceptance Scenarios**:

1. **Given** a user asks the chatbot a natural language question, **When** the chatbot generates and executes SQL, **Then** the audit log records: the timestamp, the original question, the generated SQL, a blocked flag (false), the number of rows returned, and the execution duration.
2. **Given** a user asks a question whose generated SQL is blocked by safety guardrails, **When** the system rejects the query, **Then** the audit log records the event with blocked flag set to true, and zero rows and zero execution time.
3. **Given** audit entries exist, **When** an administrator calls the audit log endpoint, **Then** all entries are returned in a readable format ordered by most recent first.

---

### User Story 2 - SQL Safety Guardrails (Priority: P1)

A sales rep or anyone with chatbot access asks a question whose AI-generated SQL contains a dangerous keyword (e.g., DELETE, DROP). The system must intercept this before touching the database, protecting data integrity.

**Why this priority**: Data destruction or modification via a chatbot is a critical risk. Blocking dangerous SQL is a non-negotiable safety requirement identified directly from the demo.

**Independent Test**: Sending a natural language question that would cause the AI to generate a DELETE or DROP statement results in a blocked response; the database is never touched.

**Acceptance Scenarios**:

1. **Given** the AI generates SQL containing a blocked keyword (DROP, DELETE, TRUNCATE, INSERT, UPDATE, ALTER, CREATE, EXEC, ERASE), **When** the safety filter evaluates the SQL, **Then** the query is rejected before execution and the user receives an error message.
2. **Given** the AI generates valid read-only SELECT SQL, **When** the safety filter evaluates it, **Then** the query proceeds to a secondary validation pass without blocking.
3. **Given** the SQL passes the keyword filter, **When** the secondary AI validator reviews it, **Then** the query is either approved for execution or rejected with a reason.

---

### User Story 3 - Accurate "Last Month" Date Queries (Priority: P2)

A sales rep asks "What were sales last month?" and expects to see data from the 30 days prior to today, consistently, regardless of what day of the month it is.

**Why this priority**: The current behavior produces inconsistent results depending on the calendar month boundary, which confuses users and undermines trust in the chatbot's answers.

**Independent Test**: Asking "sales last month" on any day of the month returns data covering exactly the 30-day period ending yesterday, with no variation based on calendar month.

**Acceptance Scenarios**:

1. **Given** a user asks a question containing the phrase "last month", **When** the system generates SQL, **Then** the date filter in that SQL covers the rolling 30-day window ending on the current date.
2. **Given** the same "last month" question is asked on the 1st of a month vs. the 15th, **When** results are compared, **Then** each reflects 30 days back from the respective query date, not a fixed calendar month.

---

### User Story 4 - Complete Grouped Results (Priority: P2)

A sales rep asks "Show me sales by region" and expects to see all regions listed, not a truncated subset.

**Why this priority**: Incomplete grouped results were demonstrated as a direct failure during the demo. Users cannot make decisions based on partial data.

**Independent Test**: A query returning results grouped into 10 or more distinct categories displays all categories in the response, not just the first few.

**Acceptance Scenarios**:

1. **Given** a query returns data grouped by a dimension (e.g., region, product, salesperson), **When** the result is formatted, **Then** all groups appear in a complete table in the chatbot response.
2. **Given** a grouped query returns 20 distinct groups, **When** the result is displayed, **Then** all 20 groups are visible, not a subset.

---

### User Story 5 - No Arbitrary Row Limit on Results (Priority: P2)

A sales rep asks a question whose answer contains more than 50 rows of data. All rows should be visible in the response.

**Why this priority**: An implicit 50-row cap was silently hiding data, causing users to draw incorrect conclusions from incomplete results.

**Independent Test**: A query returning 75 rows displays all 75 rows in the chatbot response.

**Acceptance Scenarios**:

1. **Given** a query returns 75 rows, **When** the result is formatted, **Then** all 75 rows appear in the response.
2. **Given** a query returns exactly 50 rows, **When** the result is formatted, **Then** all 50 rows appear (no off-by-one truncation).

---

### User Story 6 - Consistent, Single-Pass Result Formatting (Priority: P3)

When the chatbot receives query results, it formats them into a readable response using a predictable, rule-based process rather than making an additional AI call.

**Why this priority**: Eliminating the second AI call removes variability in result presentation and reduces cost and latency, while making responses more predictable for users.

**Independent Test**: The same query run twice returns identically formatted results. No second AI call occurs during formatting (verifiable via audit log showing one AI call per query).

**Acceptance Scenarios**:

1. **Given** a query returns tabular data, **When** the formatter processes the results, **Then** the output is a well-formed markdown table with all columns and rows.
2. **Given** the same query is run twice, **When** results are compared, **Then** the formatting is identical both times.
3. **Given** a query is completed, **When** the audit log is checked, **Then** only one AI invocation is recorded per user question.

---

### Edge Cases

- What happens when a user's question generates SQL that passes the keyword filter but is still semantically harmful (e.g., a SELECT that causes a runaway query)? The secondary AI validator provides a second layer of review.
- What happens when the audit log endpoint is called before any queries have been made? It returns an empty list without error.
- What happens when grouped results contain NULL values in a dimension? The formatter must handle and display nulls gracefully.
- What happens when "last month" appears in quoted text within a question (e.g., "show me the report titled 'last month'")?  The date-range logic should apply only to temporal filter context.
- What if a query returns zero rows? The audit log records zero for row count, and the formatter displays a clear "no results" message.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST record an audit entry for every AI-generated SQL query, capturing: timestamp, original user question, generated SQL text, blocked status (true/false), row count, and execution duration.
- **FR-002**: The audit log MUST be accessible via a dedicated read-only endpoint that returns all entries ordered by most recent first.
- **FR-003**: System MUST evaluate all AI-generated SQL against a keyword safety filter before execution; queries containing any of the blocked keywords MUST be rejected without touching the database.
- **FR-004**: The blocked keyword list MUST include at minimum: DROP, DELETE, TRUNCATE, INSERT, UPDATE, ALTER, CREATE, EXEC, ERASE, and the `xp_` stored procedure prefix.
- **FR-005**: SQL that passes the keyword filter MUST be submitted to a secondary AI-based validation service before execution; the validator MUST either approve or reject the query.
- **FR-006**: System MUST format query results using a deterministic, rule-based formatter; no additional AI call MUST be made during result formatting.
- **FR-007**: When a user question contains a temporal reference of "last month", the generated SQL date filter MUST cover the rolling 30-day window ending on the current date.
- **FR-008**: The result formatter MUST render grouped or multi-column query results as a complete markdown table showing all rows and all groups.
- **FR-009**: The result formatter MUST process all rows returned by a query; no row limit MUST be imposed at the formatting stage.
- **FR-010**: Blocked queries MUST still be recorded in the audit log with blocked=true, row count=0, and execution duration=0.

### Key Entities

- **Audit Log Entry**: A record of one chatbot query event containing: timestamp (when the query was made), user question (the original natural language input), generated SQL (the AI-produced SQL text), blocked flag (whether it was rejected by safety guardrails), row count (number of rows returned), execution duration (time taken to run the query).
- **SQL Safety Filter**: A rule-based component that checks generated SQL against a predefined list of forbidden keywords before any database interaction occurs.
- **Query Validator**: A secondary review service that evaluates SQL approved by the safety filter and makes a final approve/reject decision before execution.
- **Result Formatter**: A deterministic component that converts raw query result rows into a human-readable markdown table, applied after query execution without any AI involvement.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of chatbot queries (blocked and executed) appear in the audit log with all required fields populated; zero queries are missing from the log.
- **SC-002**: All queries containing any blocked keyword are rejected before database execution; the database is never modified by chatbot queries.
- **SC-003**: "Last month" queries consistently return data covering exactly 30 days prior to the query date across all test scenarios and dates.
- **SC-004**: Grouped queries (e.g., sales by region, sales by product) display all groups in the response; zero groups are missing or truncated.
- **SC-005**: Queries returning more than 50 rows display all rows in the response; the 50-row cap no longer exists.
- **SC-006**: Chatbot responses are identical for identical inputs when run consecutively; formatting variability is eliminated.
- **SC-007**: Each user question results in exactly one AI invocation; no secondary AI calls occur during result formatting.

## Assumptions

- The chatbot is used exclusively for read-intent queries; the blocked keyword list covers the mutating SQL operations relevant to this system's database.
- The audit log does not need user identity (e.g., who asked the question) in this sprint; user attribution is out of scope for Week 3.
- The audit endpoint is internal/admin-facing and does not require end-user authentication for this sprint; access control can be added in a future sprint.
- The rolling 30-day window is sufficient for "last month" semantics; calendar-month precision (e.g., March 1–31) is not required.
- The result formatter only needs to handle tabular (row/column) data; charts, visualisations, and pivot tables are out of scope.
- The secondary AI validator uses the same AI service already integrated in the chatbot; no new AI provider is introduced.
- Existing chatbot functionality (NL-to-SQL generation, response delivery) continues to work unchanged; these are additive improvements.
