# Prompt Templates — Kata 2

**Date:** 2026-06-09
**Author:** Lazar Bulatovic — Software Engineer
**Project:** ai-upskilling-poc (NL-to-SQL Sales Chatbot)
**Model:** Claude Sonnet (via EPAM DIAL)
**DIAL location:** [paste your DIAL shared folder link here]
**Committed location:** poc-app/ai-upskilling-poc/docs/prompt-templates-kata2.md

---

# Template 1: Generate Test Cases from a User Story

## Purpose

Generates a structured set of unit test cases from a user story for a .NET chatbot project, for use by engineers and QAs during the Build and Quality SDLC stages.

---

## Variable Placeholders

| Placeholder | Description | Example value |
|---|---|---|
| `{{user_story}}` | The full user story text including acceptance criteria | "As a sales manager, I want to ask how many orders were placed last month so that I can track monthly sales volume. AC: Returns a COUNT query. AC: Returns CANNOT_ANSWER if the question is ambiguous." |
| `{{component_name}}` | The .NET service or class being tested | `TextToSqlService` |
| `{{test_framework}}` | The test framework in use | `xUnit + FluentAssertions + NSubstitute` |

---

## Output Format Instruction

Return a markdown table with columns: Test Case ID, Description, Input, Expected Output, Test Type (Unit/Integration). Maximum 10 rows. No preamble, no explanation — just the table.

---

## Prompt Body

You are a senior .NET test engineer working on a NL-to-SQL chatbot connected to a SQL Server sales database.

Given the following user story:
{{user_story}}

Generate test cases for the component: {{component_name}}
Test framework: {{test_framework}}

Rules:
- Cover the happy path, at least two edge cases, and one negative/error case
- Each test case must be independently runnable
- For SQL generation tests, include both valid NL questions and blocked destructive queries
- Do not generate test code — only the test case specification table

Return ONLY a markdown table with these columns:
| Test Case ID | Description | Input | Expected Output | Test Type |

---

## Test Run (Author)

**Input values used:**
- `{{user_story}}` = "As a sales manager, I want to query total revenue by country so that I can compare regional performance. AC: Returns a valid SELECT with GROUP BY. AC: Returns CANNOT_ANSWER for non-sales questions."
- `{{component_name}}` = `TextToSqlService`
- `{{test_framework}}` = `xUnit + FluentAssertions + NSubstitute`

**Output quality:** Output was usable as-is — produced 8 test cases covering happy path, edge cases (empty result, ambiguous input), and one blocked destructive query.

---

## Peer Review

**Reviewer:** [Name — Role]
**Date reviewed:** YYYY-MM-DD
**Model used by reviewer:** [Model name]

**Reviewer input values used:**
- `{{user_story}}` = [value reviewer used]
- `{{component_name}}` = [value reviewer used]
- `{{test_framework}}` = [value reviewer used]

| Review question | Reviewer answer |
|---|---|
| Could you run the template without asking the author anything? | |
| Was the output format what you expected? | |
| Would you use this template on your own work? | |
| One concrete improvement suggestion | |

---

## Revision History

| Version | Date | Change | Author |
|---|---|---|---|
| 1.0 | 2026-06-09 | Initial commit | Lazar Bulatovic |
| 1.1 | YYYY-MM-DD | Post-review update | Lazar Bulatovic |

---
---

# Template 2: Summarise a Pull Request for a Non-Technical Stakeholder

## Purpose

Generates a plain-English PR summary for a non-technical stakeholder (e.g. PO, delivery manager) from a PR diff or description, for use during the Build and Release SDLC stages.

---

## Variable Placeholders

| Placeholder | Description | Example value |
|---|---|---|
| `{{pr_title}}` | The title of the pull request | "Add SQL audit log and query validator service" |
| `{{pr_description}}` | The PR description or list of changes | "Added QueryAuditLog table, AuditService, and SqlSafetyValidator with 3-layer protection. Updated ChatEndpoints to log every query." |
| `{{target_audience}}` | Who will read this summary | "Delivery manager with no .NET background" |

---

## Output Format Instruction

Return a plain-English summary in exactly three sections: (1) What changed — 2 sentences max. (2) Why it matters — 1 sentence. (3) Any risks or follow-up needed — 1 sentence or "None." No technical jargon, no code snippets, no markdown headers — plain text only.

---

## Prompt Body

You are a technical writer summarising a pull request for a non-technical stakeholder.

PR Title: {{pr_title}}
PR Description / Changes: {{pr_description}}
Target audience: {{target_audience}}

Write a plain-English summary in exactly three sections:
1. What changed (2 sentences max — describe the change in plain language, no code terms)
2. Why it matters (1 sentence — what business or quality problem this solves)
3. Risks or follow-up needed (1 sentence — or write "None" if there are no risks)

Do not use technical jargon, code snippets, or markdown formatting. Return plain text only.

---

## Test Run (Author)

**Input values used:**
- `{{pr_title}}` = "Replace ResultInterpreterService with DeterministicResultFormatter"
- `{{pr_description}}` = "Removed second LLM call for result formatting. Replaced with pure C# formatter that renders SQL results as markdown tables. Halves response time and token cost."
- `{{target_audience}}` = "Delivery manager with no .NET background"

**Output quality:** Output was usable as-is — clearly explained the change without technical terms, correctly identified the business benefit (speed and cost), and noted no follow-up risks.

---

## Peer Review

**Reviewer:** [Name — Role]
**Date reviewed:** YYYY-MM-DD
**Model used by reviewer:** [Model name]

**Reviewer input values used:**
- `{{pr_title}}` = [value reviewer used]
- `{{pr_description}}` = [value reviewer used]
- `{{target_audience}}` = [value reviewer used]

| Review question | Reviewer answer |
|---|---|
| Could you run the template without asking the author anything? | |
| Was the output format what you expected? | |
| Would you use this template on your own work? | |
| One concrete improvement suggestion | |

---

## Revision History

| Version | Date | Change | Author |
|---|---|---|---|
| 1.0 | 2026-06-09 | Initial commit | Lazar Bulatovic |
| 1.1 | YYYY-MM-DD | Post-review update | Lazar Bulatovic |

---
---

# Template 3: Generate SQL Guardrail Rules for a New Data Source

## Purpose

Generates a system prompt guardrail block for a new data source connected to a .NET NL-to-SQL chatbot, for use by engineers during the Build SDLC stage when onboarding a new database or schema.

---

## Variable Placeholders

| Placeholder | Description | Example value |
|---|---|---|
| `{{data_source_name}}` | Name of the new database or data source | `HR Database` |
| `{{sensitive_tables}}` | Comma-separated list of tables that must never be exposed | `Employees, Salaries, PerformanceReviews` |
| `{{allowed_operations}}` | What operations are permitted | `SELECT only — no aggregations on salary columns` |
| `{{row_cap}}` | Maximum rows the query may return | `200` |

---

## Output Format Instruction

Return a ready-to-paste system prompt block in plain text. Use named sections in ALL CAPS (e.g. QUERY RESTRICTIONS, SECURITY REQUIREMENTS, RESPONSE FORMAT). No explanation, no markdown headers outside the sections — just the prompt text.

---

## Prompt Body

You are a SQL safety expert for a .NET NL-to-SQL chatbot.

A new data source is being connected: {{data_source_name}}

Generate a system prompt guardrail block that instructs the LLM to:
1. Only generate SELECT statements — never INSERT, UPDATE, DELETE, DROP, TRUNCATE, ALTER, EXEC, CREATE, or any DDL/DML that modifies data
2. Never query or reference these sensitive tables: {{sensitive_tables}}
3. Only allow these operations: {{allowed_operations}}
4. Return the exact string CANNOT_ANSWER if the request cannot be fulfilled within these rules
5. Never expose table names, column names, or schema details in error messages
6. Cap result sets at {{row_cap}} rows maximum

Format as a ready-to-paste system prompt block in plain text with named ALL CAPS sections. No explanation outside the prompt block.

---

## Test Run (Author)

**Input values used:**
- `{{data_source_name}}` = `Inventory Database`
- `{{sensitive_tables}}` = `SupplierContracts, CostPrices`
- `{{allowed_operations}}` = `SELECT only on product and stock tables`
- `{{row_cap}}` = `100`

**Output quality:** Output was usable as-is — produced a clean guardrail block with correct sections, correctly excluded the sensitive tables, and included the CANNOT_ANSWER rule.

---

## Peer Review

**Reviewer:** [Name — Role]
**Date reviewed:** YYYY-MM-DD
**Model used by reviewer:** [Model name]

**Reviewer input values used:**
- `{{data_source_name}}` = [value reviewer used]
- `{{sensitive_tables}}` = [value reviewer used]
- `{{allowed_operations}}` = [value reviewer used]
- `{{row_cap}}` = [value reviewer used]

| Review question | Reviewer answer |
|---|---|
| Could you run the template without asking the author anything? | |
| Was the output format what you expected? | |
| Would you use this template on your own work? | |
| One concrete improvement suggestion | |

---

## Revision History

| Version | Date | Change | Author |
|---|---|---|---|
| 1.0 | 2026-06-09 | Initial commit | Lazar Bulatovic |
| 1.1 | YYYY-MM-DD | Post-review update | Lazar Bulatovic |
