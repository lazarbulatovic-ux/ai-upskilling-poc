# Feature Specification: NL-to-SQL Sales Chatbot PoC

**Feature Branch**: `001-nl-to-sql-chatbot`

**Created**: 2026-05-18

**Status**: Draft

**Input**: User description: "Build a conversational chatbot that lets non-technical users query a sales database in plain English — no SQL knowledge needed."

## Clarifications

### Session 2026-05-18

- Q: How should revenue be calculated from Orders, Customers, Products, and OrderItems? → A: Sum of OrderItem line totals (quantity × unit price), including only orders with status **Completed**.
- Q: When counting orders (e.g., "how many orders were placed"), which orders should be included by default? → A: Count all orders in scope (any status) unless the user explicitly specifies a status filter.
- Q: How should the chatbot present multi-row query results? → A: Return a count plus a brief summary of top results (e.g., "12 products — top sellers: Widget A, Widget B").
- Q: How should users start a new conversation session (reset context)? → A: Provide a **New Chat** button that clears in-session context; browser refresh also starts a fresh session.
- Q: How should common time phrases ("this month", "this quarter", "recently") be interpreted? → A: Mixed — "last month" = rolling 30 days; "this month" = calendar month to date; "this quarter" = calendar quarter to date; "recently" = last 7 days.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Single-Turn Sales Question (Priority: P1)

A non-technical user opens the chatbot and asks a straightforward sales question in plain English, such as "How many orders were placed last month?" The chatbot interprets the question, looks up the relevant sales data, and replies with a concise natural-language answer (e.g., "142 orders were placed in the last 30 days.") without exposing query mechanics or requiring the user to know database structure.

**Why this priority**: Single-turn Q&A is the core value proposition of the PoC. Without reliable one-shot answers, the chatbot fails its primary purpose for business users.

**Independent Test**: Can be fully tested by submitting one in-scope question per Orders, Customers, Products, and OrderItems and verifying each response is accurate, readable, and free of technical jargon.

**Acceptance Scenarios**:

1. **Given** a user with an active chat session and populated sales data, **When** the user asks "How many orders were placed last month?", **Then** the chatbot responds with a plain-language count of **all orders** in the last 30 days regardless of status (e.g., "142 orders were placed in the last 30 days.").
2. **Given** a user asks a question about customers (e.g., "How many customers are from Germany?"), **When** the question maps to in-scope data, **Then** the chatbot returns an accurate natural-language answer referencing customers only.
3. **Given** a user asks a question about products or order line items (e.g., "What is the best-selling product?" or "List products in Electronics"), **When** the question is answerable from in-scope data, **Then** the chatbot returns a count plus a brief summary of the top results in plain language (e.g., "8 products in Electronics — top sellers: Widget A, Widget B, Widget C.") without requiring follow-up clarification for a well-formed question.

---

### User Story 2 - Multi-Turn Follow-Up Questions (Priority: P2)

A user asks an initial sales question and then refines or extends it across several turns without restating full context. For example, after learning there were 142 orders last month, the user asks "Which were from Germany?" and then "And total revenue?" The chatbot uses the active conversation context to interpret each follow-up and returns progressively narrower or related answers (e.g., "23 of the 142." and "German orders generated €18,450.").

**Why this priority**: Follow-up questions mirror real analyst workflows and demonstrate that the chatbot supports exploration, not just isolated lookups.

**Independent Test**: Can be fully tested by running a scripted three-turn conversation (initial count → filtered subset → aggregate on subset) and verifying each turn correctly inherits prior context.

**Acceptance Scenarios**:

1. **Given** a user previously asked about order count for last month and received "142 orders.", **When** the user asks "Which were from Germany?", **Then** the chatbot responds with a count scoped to the prior question's timeframe and dataset (e.g., "23 of the 142.").
2. **Given** a user previously narrowed the context to German orders from the prior turn, **When** the user asks "And total revenue?", **Then** the chatbot returns total revenue for that scoped subset—sum of Completed order line items only—in plain language with appropriate currency formatting (e.g., "German orders generated €18,450.").
3. **Given** a user starts a new conversation session (via **New Chat** or browser refresh), **When** they ask a follow-up-style question with no prior context (e.g., "And total revenue?"), **Then** the chatbot asks for clarification or states what context is missing rather than guessing.
4. **Given** a user is mid-conversation with established context, **When** they click **New Chat**, **Then** the visible conversation clears, in-session context is reset, and subsequent questions are treated as a fresh session with no prior turn history.

---

### User Story 3 - Out-of-Scope Deflection (Priority: P3)

A user asks a question that cannot be answered from sales data—general knowledge, weather, unrelated business domains, or requests to change data. The chatbot politely declines and redirects the user to valid topics (orders, customers, products) instead of hallucinating an answer or attempting unsupported actions.

**Why this priority**: Safe deflection protects data integrity and sets clear expectations for PoC boundaries, but it depends on the core Q&A flows already working.

**Independent Test**: Can be fully tested by submitting a mix of off-topic prompts and write/action requests and verifying every response uses the deflection pattern without returning fabricated sales facts.

**Acceptance Scenarios**:

1. **Given** a user asks "What is the weather today?", **When** the chatbot processes the message, **Then** it responds with a scope-limitation message such as "I can only answer questions about sales data. Please ask about orders, customers, or products."
2. **Given** a user asks to modify data (e.g., "Delete order 123" or "Update customer email"), **When** the chatbot processes the message, **Then** it declines the request and explains that only read-only sales questions are supported.
3. **Given** a user asks about data outside the four in-scope entities (e.g., employee payroll or inventory warehouses), **When** the chatbot cannot map the question to Orders, Customers, Products, or OrderItems, **Then** it deflects with the same scope guidance rather than inventing an answer.

---

### Edge Cases

- What happens when a well-formed sales question returns zero matching records? The chatbot states clearly that no data was found (e.g., "No orders from France in the last 30 days.") rather than returning an error or silent failure.
- How does the system handle ambiguous time phrases? It applies these **fixed interpretations** unless the user specifies otherwise: **"last month"** = rolling last 30 days; **"this month"** = current calendar month to date; **"this quarter"** = current calendar quarter to date; **"recently"** = rolling last 7 days. For other ambiguous phrases that would materially change the answer, it asks one brief clarifying question.
- What happens when the user's question is too vague to query (e.g., "Tell me about sales")? The chatbot prompts the user to be more specific about orders, customers, products, or metrics.
- How does the system respond to empty or nonsensical input? It asks the user to rephrase with a sales-related question.
- What happens when a follow-up reference is ambiguous (e.g., "Which ones were expensive?" after multiple prior topics)? The chatbot either resolves ambiguity from the most recent in-scope turn or asks a targeted clarification.
- What happens when the data source is temporarily unavailable? The chatbot returns a user-friendly unavailability message without exposing internal error details.
- What happens when a question would require joining all four entity types in a complex way? The chatbot still attempts an answer if mappable to in-scope data; if not answerable within scope, it deflects or clarifies.
- What happens when a user asks for revenue but the scoped subset contains no **Completed** orders? The chatbot reports zero revenue or states that no completed orders match, rather than including pending or cancelled orders.
- What happens when a query matches many rows (e.g., dozens of customers or products)? The chatbot returns the total count plus a brief summary of the top results (up to 5) rather than listing every row or rendering a full table.

## Requirements *(mandatory)*

### Constitution Constraints

Features MUST comply with `.specify/memory/constitution.md` unless an approved amendment exists.
Confirm: service/table limits (≤ 5 services, ≤ 4 tables), dual SELECT validation, `CANNOT_ANSWER`, async/cancellation,
`IDialClient`-only model access, EF vs raw SQL boundaries, and one-spec-per-PR scope.

### Functional Requirements

- **FR-001**: The chatbot MUST accept natural-language questions from users through a conversational interface.
- **FR-002**: The chatbot MUST answer read-only questions about **Orders**, **Customers**, **Products**, and **OrderItems** only; no additional data domains may be introduced.
- **FR-003**: The chatbot MUST translate user intent into data lookups and return answers in plain, non-technical language suitable for business users.
- **FR-004**: The chatbot MUST support single-turn questions that require no prior conversation context (User Story 1).
- **FR-005**: The chatbot MUST maintain conversation context for the **active session** so follow-up questions can reference prior turns without restating full context (User Story 2).
- **FR-006**: The chatbot MUST NOT persist chat history beyond the active session; starting a new session MUST NOT restore prior conversations.
- **FR-016**: The chat interface MUST provide a **New Chat** control that clears the visible conversation and resets in-session context; **browser refresh** MUST also start a fresh session with no restored history.
- **FR-007**: The chatbot MUST reject or deflect out-of-scope questions—including general knowledge, non-sales topics, and unsupported domains—with a clear message directing users to orders, customers, or products (User Story 3).
- **FR-008**: The chatbot MUST NOT perform write operations (create, update, delete) or execute any action that modifies sales data.
- **FR-009**: When a question cannot be answered safely or confidently from in-scope sales data, the chatbot MUST respond with an explicit unable-to-answer outcome rather than fabricating data.
- **FR-010**: Numeric answers MUST include appropriate units or labels (counts, currency, dates) so results are understandable without database knowledge.
- **FR-011**: The chatbot MUST handle common aggregations and filters expressed in natural language (counts, totals, averages, date ranges, geographic or product filters) when mappable to the four in-scope entities.
- **FR-012**: Authentication is out of scope; the PoC MUST NOT require user login or role-based access control.
- **FR-013**: **Revenue** MUST be calculated as the sum of OrderItem line totals (quantity × unit price) for orders with status **Completed** only; pending, cancelled, or other non-completed orders MUST be excluded from revenue unless the user explicitly asks about a different status.
- **FR-014**: **Order counts** MUST include all orders matching the requested scope (time range, geography, etc.) **regardless of status** unless the user explicitly filters by status (e.g., "completed orders only").
- **FR-015**: When a query returns multiple rows, the chatbot MUST respond with the **total count** plus a **brief summary of the top results** (plain language, no tabular dump); single-value answers (counts, totals, averages) remain concise one-line responses.
- **FR-017**: The chatbot MUST interpret common relative time phrases using these defaults: **"last month"** = rolling last 30 days; **"this month"** = current calendar month to date; **"this quarter"** = current calendar quarter to date; **"recently"** = rolling last 7 days.

### Key Entities *(include if feature involves data)*

- **Order**: A sales transaction placed by a customer; key attributes include order date, **status** (including **Completed** and other lifecycle values), and links to the purchasing customer. Used for order-volume and time-based questions (all statuses by default) and as the status gate for revenue calculations (Completed only).
- **Customer**: A buyer in the sales database; key attributes include name and country/region. Used for geographic and customer-count questions and for filtering orders.
- **Product**: An item offered for sale; key attributes include name and category. Used for product-level sales performance questions.
- **OrderItem**: A line item within an order linking a product to quantity and unit price; line total = quantity × unit price. Connects Orders and Products. Used as the revenue basis (for Completed orders), quantity-sold metrics, and best-seller analysis.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In acceptance testing, at least **90%** of curated single-turn, in-scope questions receive factually correct plain-language answers without user correction (User Story 1).
- **SC-002**: In a standard three-turn follow-up script (count → filter → revenue), **100%** of turns produce contextually correct answers that reference the prior scope (User Story 2).
- **SC-003**: In acceptance testing, **100%** of off-topic, write, and unsupported-domain prompts receive a deflection response with no fabricated sales data (User Story 3).
- **SC-004**: **95%** of in-scope questions receive a user-visible response within **10 seconds** under normal PoC operating conditions.
- **SC-005**: At least **5 representative business users** (non-technical) can complete a guided "ask three sales questions" task without assistance or SQL knowledge, with **4 of 5** rating the experience as understandable.

## Assumptions

- A pre-populated sales database exists with realistic sample data covering Orders, Customers, Products, and OrderItems; the PoC does not include data-ingestion or ETL tooling.
- **Revenue** always means the sum of OrderItem line totals (quantity × unit price) for **Completed** orders unless the user explicitly requests a different status filter.
- **Order counts** include all matching orders regardless of status unless the user explicitly requests a status filter.
- Multi-row answers include the total match count plus up to **5** top/representative examples; full row-by-row listings and formatted tables are out of scope for the PoC.
- **Time phrase defaults**: "last month" = rolling last **30 days**; "this month" = current calendar month to date; "this quarter" = current calendar quarter to date; "recently" = rolling last **7 days**. All dates use the system's current date at query time.
- Conversation context is retained **in memory for the active session only** until the user clicks **New Chat**, refreshes the browser, or closes the tab; no conversation history is restored after a reset.
- Currency values are displayed in **EUR (€)** when presenting monetary amounts unless the underlying data explicitly specifies another currency.
- The PoC targets a **single-user, unauthenticated** demo environment; no multi-tenant isolation or audit logging beyond basic operational needs is required.
- Users interact via a **web-based chat interface** accessible in a standard desktop browser; mobile-optimized layouts are not required for the PoC.
- Clarifying questions are allowed sparingly when ambiguity would materially change the answer; the chatbot should prefer answering when a reasonable default interpretation exists.
- Performance targets assume normal PoC load (single concurrent demo user or small team); high-concurrency production scaling is out of scope.
