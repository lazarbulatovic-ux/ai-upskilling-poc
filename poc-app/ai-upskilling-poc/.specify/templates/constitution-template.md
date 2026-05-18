# ai-upskilling-poc Constitution

> Canonical copy: `.specify/memory/constitution.md`. Edit the memory file; keep this template
> aligned when principles change.

## Core Principles

### I. PoC Simplicity (YAGNI)

≤ 5 services; ≤ 4 DB tables; no speculative features.

### II. SQL Safety (NON-NEGOTIABLE)

SELECT-only in `TextToSqlService` and `SqlExecutionService`; `CANNOT_ANSWER` sentinel; max 500 rows.

### III. Testability & Quality

Interfaces for all services; NSubstitute + FluentAssertions; integration tests skip without DB.

### IV. Async Discipline

No `.Result` / `.Wait()`; `CancellationToken` on all public methods.

### V. Data Access Boundaries

EF Core 8 for migrations/seeding; raw `DbCommand` only for LLM SQL in `SqlExecutionService`.

### VI. LLM Integration (DIAL)

All DIAL via `IDialClient`; temperature 0 (SQL) / 0.3 (interpretation).

### VII. CI & Delivery Discipline

`dotnet build`, `dotnet test`, format check on every push; one spec per PR.

## Architecture & Scope Boundaries

See `.specify/memory/constitution.md` for limits table and out-of-scope list.

## Development Workflow & Compliance

Spec Kit workflow; Constitution Check in plan.md; compliance at plan, PR, and implement gates.

## Governance

Amendments via PR to memory constitution; semantic versioning; compliance review required.

**Version**: 1.0.0 | **Ratified**: 2026-05-18 | **Last Amended**: 2026-05-18
