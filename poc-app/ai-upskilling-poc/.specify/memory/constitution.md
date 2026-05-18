<!--
Sync Impact Report
- Version change: (template) → 1.0.0
- Modified principles: N/A (initial ratification)
- Added sections: Core Principles (7), Architecture & Scope Boundaries,
  Development Workflow & Compliance, Governance
- Removed sections: none
- Templates: ✅ plan-template.md (Constitution Check gates)
  ✅ tasks-template.md (testing discipline note)
  ✅ spec-template.md (Constitution Constraints subsection)
  ✅ constitution-template.md (aligned structure)
- Follow-up TODOs: none
-->

# ai-upskilling-poc Constitution

## Core Principles

### I. PoC Simplicity (YAGNI)

The chatbot proof-of-concept MUST stay minimal and demonstrable.

- The solution MUST NOT exceed **5 runnable/deployable services** (API, workers, frontends,
  and similar units each count as one service).
- The relational schema MUST NOT exceed **4 database tables** (excluding EF migration
  history tables).
- Features MUST deliver the current spec only; speculative capabilities, “nice to have”
  expansions, and future-phase placeholders are forbidden unless a constitution amendment
  explicitly approves them.
- Every added component MUST be justified in the feature plan’s Constitution Check; unjustified
  complexity MUST be rejected or deferred.

**Rationale**: The PoC exists to validate text-to-SQL chat safely and quickly—not to grow into
a production platform.

### II. SQL Safety (NON-NEGOTIABLE)

All LLM-generated SQL is untrusted input until validated.

- Generated SQL MUST be validated as **SELECT-only** in **two** places:
  `TextToSqlService` (generation boundary) and `SqlExecutionService` (execution boundary).
- Neither layer MAY execute or pass through statements containing DML, DDL, or administrative
  verbs; validation failures MUST NOT fall through to execution.
- When the model cannot produce a safe, answerable query, services MUST return the sentinel
  **`CANNOT_ANSWER`** (exact string, consistent across layers).
- Executed queries MUST be capped at **500 rows** maximum; services MUST enforce the limit even
  if the generated SQL omits `TOP`/`LIMIT`.

**Rationale**: Defense in depth limits blast radius if one validation layer is bypassed or
regresses.

### III. Testability & Quality

Testability is a design requirement, not an afterthought.

- Every application service MUST be defined behind an **interface** and consumed via dependency
  injection.
- Unit tests MUST use **NSubstitute** for mocks and **FluentAssertions** for assertions.
- Integration tests that require a database MUST **skip gracefully** when the database is absent
  (no failures in CI or local runs without infrastructure).
- New services and behavior changes MUST include corresponding unit tests; integration tests are
  required when the feature touches persistence or end-to-end chat flows.

**Rationale**: Interface-based design keeps the PoC mockable and verifiable without a live DB for
every developer machine.

### IV. Async Discipline

Asynchronous code MUST remain non-blocking and cancellable.

- `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, and other synchronous blocking on tasks are
  **forbidden** anywhere in the codebase.
- Every **public** method on application services, API endpoints, and infrastructure adapters MUST
  accept a `CancellationToken` (with a default of `default` only where the framework requires it)
  and MUST honor cancellation cooperatively.
- Long-running LLM and database operations MUST propagate cancellation to underlying HTTP and
  database calls.

**Rationale**: Blocking async code causes deadlocks under ASP.NET Core load and hides timeout
behavior.

### V. Data Access Boundaries

Two data-access paths exist; they MUST NOT be conflated.

- **EF Core 8** is the sole mechanism for schema migrations, seed data, and application-owned
  reads/writes of trusted entities.
- **Raw `DbCommand`** (or equivalent low-level ADO.NET on the EF connection) is permitted **only**
  for executing validated, LLM-generated SELECT statements inside `SqlExecutionService`.
- Application code MUST NOT use raw SQL for migrations, seeding, or non-LLM business logic.

**Rationale**: EF preserves schema integrity; raw commands are isolated to the untrusted SQL path.

### VI. LLM Integration (DIAL)

All model calls MUST flow through a single abstraction.

- Every DIAL HTTP interaction MUST go through **`IDialClient`**; direct HTTP clients or ad-hoc
  calls elsewhere are forbidden.
- **SQL generation** prompts MUST use **`temperature = 0`** (deterministic SQL).
- **Result interpretation** prompts MUST use **`temperature = 0.3`**.
- Prompts, models, and endpoints MUST be configurable; hard-coded secrets are forbidden.

**Rationale**: Centralizing DIAL access simplifies auditing, testing, and temperature policy
enforcement.

### VII. CI & Delivery Discipline

Mainline quality gates are automated and non-negotiable.

- Every push MUST pass **`dotnet build`**, **`dotnet test`**, and **`dotnet format --verify-no-changes`**
  (or the repository’s equivalent format-check script).
- Each pull request MUST contain **exactly one** feature spec under `specs/` (one spec directory
  per PR); drive-by multi-feature PRs are forbidden.
- PR descriptions MUST reference the spec path and confirm Constitution Check completion.

**Rationale**: Keeps the PoC mergeable and reviewable while the team iterates on Spec Kit workflows.

## Architecture & Scope Boundaries

| Boundary | Limit | Enforcement |
|----------|-------|-------------|
| Services | ≤ 5 | Plan Constitution Check + code review |
| DB tables | ≤ 4 | EF migrations + data-model.md |
| LLM SQL | SELECT only, 500 rows | `TextToSqlService`, `SqlExecutionService` |
| LLM entrypoint | `IDialClient` only | DI registration + code review |
| Untrusted SQL execution | `SqlExecutionService` only | No raw LLM SQL elsewhere |

Out-of-scope unless amended: authentication beyond PoC needs, multi-tenant isolation, caching layers,
message queues, and analytics pipelines not required by the active spec.

## Development Workflow & Compliance

1. **Specify** → **Plan** → **Tasks** → **Implement** using Spec Kit; each feature owns a branch
   and spec folder under `specs/[###-feature-name]/`.
2. Before Phase 0 research, the plan’s **Constitution Check** MUST be filled and pass; re-check after
   Phase 1 design if schema, services, or LLM touchpoints change.
3. Implementation MUST NOT merge if any principle above is violated; document justified exceptions
   only via the plan’s Complexity Tracking table and a constitution amendment.
4. Code review MUST verify: dual SELECT validation, `CANNOT_ANSWER` handling, interface coverage,
   async/cancellation compliance, and CI script parity.

Runtime guidance: follow the active feature `plan.md`, `spec.md`, and `.cursor/rules/specify-rules.mdc`.

## Governance

This constitution supersedes ad-hoc conventions and informal README guidance for the PoC.

- **Amendments**: Propose changes via PR updating `.specify/memory/constitution.md`, bump the version
  semantically, and sync dependent templates in the same PR.
- **Versioning policy**: MAJOR = principle removal or incompatible redefinition; MINOR = new principle
  or materially expanded rule; PATCH = clarifications and non-semantic edits.
- **Compliance review**: Required at plan approval, PR review, and before `/speckit-implement` merge.
- **Runtime**: Agents and contributors MUST read this file when planning or implementing features.

**Version**: 1.0.0 | **Ratified**: 2026-05-18 | **Last Amended**: 2026-05-18
