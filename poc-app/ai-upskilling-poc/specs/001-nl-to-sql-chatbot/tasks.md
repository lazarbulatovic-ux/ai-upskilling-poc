---
description: "Task list for NL-to-SQL Sales Chatbot PoC implementation"
---

# Tasks: NL-to-SQL Sales Chatbot PoC

**Input**: Design documents from `/specs/001-nl-to-sql-chatbot/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Per constitution, all services MUST have interfaces with unit tests (NSubstitute, FluentAssertions). Integration tests that need a DB MUST skip gracefully when the DB is absent. Test tasks included for every user story.

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story label (US1, US2, US3)
- Every task includes an exact file path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution scaffolding, CI, and configuration

- [ ] T001 Create SalesChatbot.sln and folder structure per plan.md at repository root
- [ ] T002 Create src/SalesChatbot/SalesChatbot.csproj with .NET 8, Blazor Server, EF Core 8, and Microsoft.Data.SqlClient packages
- [ ] T003 [P] Create tests/SalesChatbot.UnitTests/SalesChatbot.UnitTests.csproj with xUnit, NSubstitute, and FluentAssertions
- [ ] T004 [P] Create tests/SalesChatbot.IntegrationTests/SalesChatbot.IntegrationTests.csproj with Microsoft.AspNetCore.Mvc.Testing
- [ ] T005 [P] Add src/SalesChatbot/appsettings.json and src/SalesChatbot/appsettings.Development.json with ConnectionStrings:SalesDb and Dial sections
- [ ] T006 [P] Create .github/workflows/ci.yml running dotnet build, dotnet test, and dotnet format --verify-no-changes
- [ ] T007 [P] Add .editorconfig at repository root for dotnet format compliance

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Database schema, shared validation, DIAL client, and core service pipeline — MUST complete before user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T008 [P] Create Customer entity in src/SalesChatbot/Data/Entities/Customer.cs
- [ ] T009 [P] Create Product entity in src/SalesChatbot/Data/Entities/Product.cs
- [ ] T010 [P] Create Order entity in src/SalesChatbot/Data/Entities/Order.cs
- [ ] T011 [P] Create OrderItem entity in src/SalesChatbot/Data/Entities/OrderItem.cs
- [ ] T012 Create SalesDbContext with Fluent API relationships in src/SalesChatbot/Data/SalesDbContext.cs
- [ ] T013 Generate initial EF Core migration in src/SalesChatbot/Data/Migrations/
- [ ] T014 Implement SalesDataSeeder with acceptance-scenario seed data in src/SalesChatbot/Data/Seed/SalesDataSeeder.cs
- [ ] T015 [P] Create QueryResult and SqlGenerationResult in src/SalesChatbot/Models/QueryResult.cs and src/SalesChatbot/Models/SqlGenerationResult.cs
- [ ] T016 [P] Create ChatExchange and ConversationSession in src/SalesChatbot/Models/ChatExchange.cs and src/SalesChatbot/Models/ConversationSession.cs
- [ ] T017 [P] Define IDialClient, ITextToSqlService, ISqlExecutionService, IResultInterpreterService, IConversationService in src/SalesChatbot/Services/Interfaces/
- [ ] T018 Implement SqlSafetyValidator with dual-boundary SELECT-only rules in src/SalesChatbot/Services/Validation/SqlSafetyValidator.cs
- [ ] T019 [P] Add SqlSafetyValidator unit tests in tests/SalesChatbot.UnitTests/Validation/SqlSafetyValidatorTests.cs
- [ ] T020 [P] Implement DialOptions and DialChatRequest in src/SalesChatbot/Infrastructure/Dial/DialOptions.cs and src/SalesChatbot/Infrastructure/Dial/DialChatRequest.cs
- [ ] T021 Implement DialClient (IDialClient) with CancellationToken propagation in src/SalesChatbot/Infrastructure/Dial/DialClient.cs
- [ ] T022 [P] Add DialClient unit tests with mocked HttpMessageHandler in tests/SalesChatbot.UnitTests/Infrastructure/DialClientTests.cs
- [ ] T023 Implement TextToSqlService with schema prompt, temp=0, and SELECT validation in src/SalesChatbot/Services/TextToSqlService.cs
- [ ] T024 [P] Add TextToSqlService unit tests in tests/SalesChatbot.UnitTests/Services/TextToSqlServiceTests.cs
- [ ] T025 Implement SqlExecutionService with DbCommand, re-validation, and 500-row cap in src/SalesChatbot/Services/SqlExecutionService.cs
- [ ] T026 [P] Add SqlExecutionService unit tests in tests/SalesChatbot.UnitTests/Services/SqlExecutionServiceTests.cs
- [ ] T027 Implement ResultInterpreterService with temp=0.3 and EUR formatting in src/SalesChatbot/Services/ResultInterpreterService.cs
- [ ] T028 [P] Add ResultInterpreterService unit tests in tests/SalesChatbot.UnitTests/Services/ResultInterpreterServiceTests.cs
- [ ] T029 Implement ConversationService single-turn pipeline orchestration in src/SalesChatbot/Services/ConversationService.cs
- [ ] T030 Register scoped services, EF Core, HttpClient, and Blazor in src/SalesChatbot/Program.cs
- [ ] T031 Create SqlServerFactAttribute skip helper in tests/SalesChatbot.IntegrationTests/SqlServerFactAttribute.cs

**Checkpoint**: Foundation ready — core text-to-SQL pipeline compiles and unit tests pass

---

## Phase 3: User Story 1 - Single-Turn Sales Question (Priority: P1) 🎯 MVP

**Goal**: Non-technical users ask one plain-English sales question and receive an accurate, readable answer without SQL exposure

**Independent Test**: Submit one in-scope question each for Orders, Customers, Products, and OrderItems; verify plain-language answers with correct counts, summaries, and no technical jargon

### Tests for User Story 1

- [ ] T032 [P] [US1] Add ConversationService single-turn unit tests in tests/SalesChatbot.UnitTests/Services/ConversationServiceSingleTurnTests.cs
- [ ] T033 [P] [US1] Add single-turn integration tests (order count, customer count, product summary) in tests/SalesChatbot.IntegrationTests/SingleTurnChatTests.cs

### Implementation for User Story 1

- [ ] T034 [US1] Enhance TextToSqlService system prompt with revenue, order-count, and time-phrase business rules in src/SalesChatbot/Services/TextToSqlService.cs
- [ ] T035 [US1] Enhance ResultInterpreterService for multi-row count plus top-5 summary in src/SalesChatbot/Services/ResultInterpreterService.cs
- [ ] T036 [US1] Implement POST /api/chat/message in src/SalesChatbot/Api/ChatEndpoints.cs
- [ ] T037 [P] [US1] Create Chat.razor page in src/SalesChatbot/Components/Pages/Chat.razor
- [ ] T038 [P] [US1] Create MessageList.razor in src/SalesChatbot/Components/Chat/MessageList.razor
- [ ] T039 [P] [US1] Create ChatInput.razor in src/SalesChatbot/Components/Chat/ChatInput.razor
- [ ] T040 [US1] Wire Chat.razor to IConversationService and map Blazor routes in src/SalesChatbot/Program.cs
- [ ] T041 [P] [US1] Add chat UI styling in src/SalesChatbot/wwwroot/css/chat.css
- [ ] T042 [US1] Invoke SalesDataSeeder and EF migrate on startup in Development in src/SalesChatbot/Program.cs

**Checkpoint**: User Story 1 fully functional — single-turn Q&A works via UI and API

---

## Phase 4: User Story 2 - Multi-Turn Follow-Up Questions (Priority: P2)

**Goal**: Users refine questions across turns; chatbot inherits prior context; New Chat resets session

**Independent Test**: Run scripted three-turn conversation (count → filter → revenue); verify contextual answers; verify New Chat and ambiguous follow-up without context

### Tests for User Story 2

- [ ] T043 [P] [US2] Add ConversationService history-cap unit tests in tests/SalesChatbot.UnitTests/Services/ConversationServiceHistoryTests.cs
- [ ] T044 [P] [US2] Add three-turn follow-up integration test in tests/SalesChatbot.IntegrationTests/MultiTurnChatTests.cs

### Implementation for User Story 2

- [ ] T045 [US2] Add 10-exchange history trimming to ConversationService in src/SalesChatbot/Services/ConversationService.cs
- [ ] T046 [US2] Pass conversation history into TextToSqlService LLM prompts in src/SalesChatbot/Services/TextToSqlService.cs
- [ ] T047 [US2] Pass conversation history into ResultInterpreterService LLM prompts in src/SalesChatbot/Services/ResultInterpreterService.cs
- [ ] T048 [US2] Handle ambiguous follow-up without prior context with clarification response in src/SalesChatbot/Services/ConversationService.cs
- [ ] T049 [US2] Add New Chat button calling Reset() in src/SalesChatbot/Components/Pages/Chat.razor
- [ ] T050 [US2] Implement POST /api/chat/new returning 204 in src/SalesChatbot/Api/ChatEndpoints.cs

**Checkpoint**: User Stories 1 and 2 both work independently — multi-turn context and reset verified

---

## Phase 5: User Story 3 - Out-of-Scope Deflection (Priority: P3)

**Goal**: Off-topic, write, and unsupported-domain requests receive polite deflection without fabricated data

**Independent Test**: Submit weather, delete/update, and payroll prompts; verify deflection messages with no fabricated sales facts

### Tests for User Story 3

- [ ] T051 [P] [US3] Add CANNOT_ANSWER deflection unit tests in tests/SalesChatbot.UnitTests/Services/ConversationServiceDeflectionTests.cs
- [ ] T052 [P] [US3] Add off-topic and write-request integration tests in tests/SalesChatbot.IntegrationTests/DeflectionChatTests.cs

### Implementation for User Story 3

- [ ] T053 [US3] Add DeflectionMessages constants and CANNOT_ANSWER mapping in src/SalesChatbot/Services/ConversationService.cs
- [ ] T054 [US3] Update TextToSqlService prompt to emit CANNOT_ANSWER for out-of-scope and write requests in src/SalesChatbot/Services/TextToSqlService.cs
- [ ] T055 [US3] Map SQL validation failures to deflection without exposing SQL in src/SalesChatbot/Services/ConversationService.cs
- [ ] T056 [US3] Handle empty and nonsensical input with rephrase prompt in src/SalesChatbot/Services/ConversationService.cs
- [ ] T057 [US3] Handle zero-row results with clear no-data messaging in src/SalesChatbot/Services/ResultInterpreterService.cs

**Checkpoint**: All three user stories independently functional — safe deflection verified

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Error handling, layout shell, constitution compliance sweep, and quickstart validation

- [ ] T058 [P] Add database unavailability handling (503) in src/SalesChatbot/Services/ConversationService.cs and src/SalesChatbot/Api/ChatEndpoints.cs
- [ ] T059 [P] Validate empty message returns 400 Bad Request in src/SalesChatbot/Api/ChatEndpoints.cs
- [ ] T060 [P] Add App.razor, Routes.razor, and MainLayout in src/SalesChatbot/Components/
- [ ] T061 Audit CancellationToken on all public service and API methods across src/SalesChatbot/Services/ and src/SalesChatbot/Api/
- [ ] T062 Run quickstart.md validation and update steps if needed in specs/001-nl-to-sql-chatbot/quickstart.md
- [ ] T063 Final dotnet build, test, and dotnet format --verify-no-changes across SalesChatbot.sln

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — **BLOCKS all user stories**
- **User Story 1 (Phase 3)**: Depends on Phase 2 — MVP deliverable
- **User Story 2 (Phase 4)**: Depends on Phase 2; builds on US1 UI/API but independently testable
- **User Story 3 (Phase 5)**: Depends on Phase 2; builds on pipeline but independently testable via deflection tests
- **Polish (Phase 6)**: Depends on desired user stories being complete

### User Story Dependencies

| Story | Depends On | Notes |
|-------|------------|-------|
| US1 (P1) | Foundational only | MVP — no prior story required |
| US2 (P2) | Foundational + US1 UI/API shell | History and New Chat extend existing Chat.razor |
| US3 (P3) | Foundational pipeline | Deflection logic mostly in ConversationService/TextToSqlService |

### Within Each User Story

- Tests before or alongside implementation (unit tests can be written against interfaces first)
- Services before endpoints/UI integration
- Story checkpoint before moving to next priority

### Parallel Opportunities

- **Phase 1**: T003, T004, T005, T006, T007 in parallel after T001–T002
- **Phase 2**: Entity tasks T008–T011; model tasks T015–T016; interface T017; Dial T020; unit test tasks T019, T022, T024, T026, T028 in parallel where marked [P]
- **Phase 3**: T032–T033 tests; T037–T039 UI components; T041 CSS in parallel
- **Phase 4**: T043–T044 tests in parallel
- **Phase 5**: T051–T052 tests in parallel
- **Phase 6**: T058–T060 in parallel
- **Cross-story**: After Phase 2, US3 deflection tasks (T053–T057) can proceed in parallel with US2 if staffed separately (minimal file overlap)

---

## Parallel Example: User Story 1

```bash
# Tests in parallel:
T032: tests/SalesChatbot.UnitTests/Services/ConversationServiceSingleTurnTests.cs
T033: tests/SalesChatbot.IntegrationTests/SingleTurnChatTests.cs

# UI components in parallel:
T037: src/SalesChatbot/Components/Pages/Chat.razor
T038: src/SalesChatbot/Components/Chat/MessageList.razor
T039: src/SalesChatbot/Components/Chat/ChatInput.razor
T041: src/SalesChatbot/wwwroot/css/chat.css
```

---

## Parallel Example: Foundational Phase

```bash
# Entities in parallel:
T008: src/SalesChatbot/Data/Entities/Customer.cs
T009: src/SalesChatbot/Data/Entities/Product.cs
T010: src/SalesChatbot/Data/Entities/Order.cs
T011: src/SalesChatbot/Data/Entities/OrderItem.cs

# Service unit tests in parallel (after implementations):
T019: tests/SalesChatbot.UnitTests/Validation/SqlSafetyValidatorTests.cs
T022: tests/SalesChatbot.UnitTests/Infrastructure/DialClientTests.cs
T024: tests/SalesChatbot.UnitTests/Services/TextToSqlServiceTests.cs
T026: tests/SalesChatbot.UnitTests/Services/SqlExecutionServiceTests.cs
T028: tests/SalesChatbot.UnitTests/Services/ResultInterpreterServiceTests.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T007)
2. Complete Phase 2: Foundational (T008–T031)
3. Complete Phase 3: User Story 1 (T032–T042)
4. **STOP and VALIDATE**: Run single-turn tests; demo via Chat.razor
5. Optional: deploy/demo

### Incremental Delivery

1. Setup + Foundational → pipeline ready
2. User Story 1 → MVP demo (single-turn Q&A)
3. User Story 2 → multi-turn follow-ups + New Chat
4. User Story 3 → safe deflection for edge cases
5. Polish → production-ready PoC quality gates

### Parallel Team Strategy

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (UI + API)
   - Developer B: User Story 3 deflection (services layer, parallel to US1 UI)
   - Developer C: User Story 2 after US1 UI shell exists
3. Polish phase as shared cleanup

---

## Notes

- All five application services registered **Scoped** per plan.md
- LLM policy: temp 0 (SQL), temp 0.3 (interpretation), `CANNOT_ANSWER` sentinel, 10-exchange cap
- Integration tests MUST use `[SqlServerFact]` or equivalent to skip when LocalDB absent
- Commit after each task or logical group; stop at any checkpoint to validate story independently
