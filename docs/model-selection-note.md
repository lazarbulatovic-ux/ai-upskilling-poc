# Model Selection Note

**Date:** 2026-06-09
**Author:** Lazar Bulatovic — Software Engineer
**Project:** ai-upskilling-poc (NL-to-SQL Sales Chatbot)
**Task:** Generate a SQL safety guardrail prompt for a .NET NL-to-SQL chatbot that blocks destructive operations
**Committed location:** poc-app/ai-upskilling-poc/docs/model-selection-note.md

---

## Evaluation Criteria

| # | Criterion | Why it matters for this task |
|---|-----------|------------------------------|
| 1 | Completeness of threat coverage | The guardrail must block all destructive SQL patterns — missing one is a security gap |
| 2 | Prompt precision | Instructions must be unambiguous — vague rules produce inconsistent LLM behaviour at temperature=0 |
| 3 | Format compliance | Output must be a ready-to-paste system prompt block, not a narrative explanation |
| 4 | False-positive risk | Overly restrictive rules block legitimate SELECT queries — the model should flag this risk |

---

## Prompt Used

You are a SQL safety expert for a .NET NL-to-SQL chatbot connected to a SQL Server sales database.

Generate a system prompt guardrail block that instructs the LLM to:
1. Only generate SELECT statements — never INSERT, UPDATE, DELETE, DROP, TRUNCATE, ALTER, EXEC, or any DDL/DML that modifies data
2. Return the exact string CANNOT_ANSWER if the user's question cannot be answered with a safe SELECT
3. Never expose table names, column names, or schema details in error messages
4. Cap result sets at 500 rows maximum

Format the output as a ready-to-paste system prompt block in plain text. No explanation, no markdown headers — just the prompt text.

---

## Output Comparison

### Model A: Claude Sonnet (via EPAM DIAL)
> You are a SQL query generator for a sales database. Follow these safety rules strictly:
> QUERY RESTRICTIONS: Generate ONLY SELECT statements. NEVER use INSERT, UPDATE, DELETE, DROP, TRUNCATE, ALTER, EXEC, CREATE, or any statement that modifies data or schema.
> If the user's request requires data modification or cannot be answered with a SELECT statement, return exactly: CANNOT_ANSWER
> All SELECT queries must include "TOP 500" to limit results to maximum 500 rows

### Model B: GPT-4o (via EPAM DIAL)
> You are an expert SQL assistant for a .NET NL-to-SQL chatbot connected to a SQL Server sales database. Follow these strict safety instructions:
> Only generate safe, read-only SQL SELECT statements. Never generate or suggest any statements that modify data or schema, including but not limited to INSERT, UPDATE, DELETE, DROP, TRUNCATE, ALTER, EXEC, or any other DDL/DML commands.
> If the user's question cannot be answered with a safe SELECT statement, return the exact string: CANNOT_ANSWER

---

## Scorecard

| Criterion | Model A score (1–3) | Model A evidence | Model B score (1–3) | Model B evidence |
|-----------|---------------------|------------------|---------------------|------------------|
| Completeness of threat coverage | 3 | Explicitly adds CREATE to the blocklist beyond the required set — no gaps | 3 | Uses "including but not limited to" phrasing which covers unknown future threats |
| Prompt precision | 3 | Structured into named sections (QUERY RESTRICTIONS, SECURITY, RESPONSE FORMAT) — unambiguous for LLM at temperature=0 | 2 | Flowing prose paragraphs are readable but less structured, leaving more room for misinterpretation |
| Format compliance | 3 | Clean sectioned block, ready to paste with no explanation outside the prompt | 2 | Prose style is pasteable but harder to scan and verify at a glance |
| False-positive risk | 1 | Did not flag risk of blocking legitimate edge-case SELECT queries | 1 | Did not flag risk of blocking legitimate edge-case SELECT queries |
| **Total** | **10** | | **8** | |

---

## Decision

**Selected model:** Claude Sonnet (via EPAM DIAL)

**Rationale:** Claude Sonnet scored higher on prompt precision and format compliance — the two criteria most critical for a guardrail prompt that must behave consistently at temperature=0. Its structured section format (QUERY RESTRICTIONS / SECURITY REQUIREMENTS / RESPONSE FORMAT) makes it easier to audit and extend than GPT-4o's prose output. GPT-4o's main shortcoming was producing a flowing paragraph style that, while correct, is harder for a team to verify and modify without introducing ambiguity.

---

## Active Constraint

**What could change this decision within 30 days:** A change in EPAM DIAL model availability or a project policy restricting which models may process system prompt logic would require re-evaluation against the same criteria.

---

## Revision History

| Version | Date | Change |
|---------|------|--------|
| 1.0 | 2026-06-09 | Initial commit |