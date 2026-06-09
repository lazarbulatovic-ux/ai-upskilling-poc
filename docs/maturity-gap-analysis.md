# Maturity Gap Analysis

**Date:** 2026-06-09
**Author:** Lazar Bulatovic — Software Engineer
**Project:** ai-upskilling-poc (NL-to-SQL Sales Chatbot) — individual bench project, no active client engagement
**Committed location:** poc-app/ai-upskilling-poc/docs/maturity-gap-analysis.md

> **Note:** This assessment is scored against my individual bench project (ai-upskilling-poc). There is no active delivery team — scores reflect solo working practices. Dimensions that require team structure (AI Champions, DAU) are scored honestly against that reality.

---

## Scorecard

| Dimension | Level (L1 / L2 / L3) | Score (1.0 / 2.0 / 3.0) | Evidence (2–3 sentences) |
|---|---|---|---|
| AI Capabilities | L2 | 2.0 | The NL-to-SQL chatbot was built using Spec-Driven Development with GitHub Spec Kit — specs, plans, and task lists drove agent implementation across 94 tasks in two feature specs. Claude Sonnet via EPAM DIAL was used daily for code generation, SQL guardrail design, and test case generation, with AI assistance covering the majority of implementation deliverables. The second LLM call was replaced with a deterministic C# formatter based on AI-assisted architectural reasoning — demonstrating AI use beyond code completion into design decisions. |
| Reusability | L2 | 2.0 | Three reusable prompt templates (test case generation, PR summary, SQL guardrail generation) are committed to the repository under `docs/prompt-templates-kata2.md` and available to any teammate from day one. A `CLAUDE.md` rule file is committed at the repo root and points agents to the current plan spec, giving any agent consistent project context without manual re-explanation. Specs, plans, and task lists are stored under version control in `specs/` — reusable as starting points for future features. |
| AI Champions | L1 | 1.0 | There is no delivery team and therefore no designated Champion role or Champion network. I am the sole practitioner on this project and act as an informal enthusiast — using AI tools daily and building shared artifacts — but with no mandate, no protected time allocation, and no one to transfer knowledge to. This scores L1 by definition: enthusiasts exist but no structured support or formal role exists. |
| Performance Tracking | L1 | 1.0 | No system-grounded metrics are tracked for AI's impact on this project — no PR cycle time before/after comparison, no test coverage delta attributed to AI generation, no token cost per feature. The only observable signal is that 97/100 unit tests pass and the Week 3 architectural change halved response time, but neither was measured against a baseline in a trackable system. All evidence of AI productivity is anecdotal. |
| DAU | L2 | 2.0 | As the sole practitioner, I use AI tools (Claude Sonnet via EPAM DIAL, GitHub Copilot) every working day as part of normal workflow — not occasionally or experimentally. This meets the >70% threshold by definition for a team of one. However, this score is fragile: it reflects individual habit, not team adoption, and would not survive a team context without deliberate onboarding. |
| **Average** | | **1.6** | |
| **Overall Level** | **L1** | | Average 1.6 falls in the L1 range (1.0–1.9) |

---

## Gap Analysis

### Gap 1

**Dimension:** Performance Tracking
**Current level:** L1
**Why this gap is most damaging:** Without system-grounded metrics, I cannot demonstrate AI ROI to a delivery manager or client, cannot identify which AI interventions are actually saving time, and cannot make a data-backed case for L2 or L3 tooling investment on a future project.
**Root cause:** There is no defined set of metrics to track, no instrumentation in place to collect them from system sources (Git, CI/CD), and no sprint cadence that would trigger a metrics review — the absence is structural, not motivational.

---

### Gap 2

**Dimension:** AI Champions
**Current level:** L1
**Why this gap is most damaging:** All AI knowledge, prompt patterns, and context files currently exist only in my head and in artifacts I authored — if I join a team tomorrow, none of my practices would transfer automatically, and no one on that team would have a designated role to drive adoption or onboard others.
**Root cause:** There is no team context in which a Champion role could exist, and no process for designating or onboarding a Champion when one is needed — the gap is structural: the role and its responsibilities have never been formally defined or assigned on any project I have worked on.

---

## 30-Day Improvement Plan

### Step 1 — addresses Gap 1 (Performance Tracking)

| Field | Value |
|---|---|
| **Action** | Define and instrument two system-grounded metrics for the ai-upskilling-poc project: (1) test coverage percentage tracked per commit via GitHub Actions CI output, (2) PR merge time tracked via GitHub PR timestamps. Commit a `metrics-baseline.md` to the repo documenting the pre-measurement baseline and the measurement method for both metrics. |
| **Owner** | Lazar Bulatovic |
| **Timeline** | 2026-07-09 |
| **Success metric** | `metrics-baseline.md` is committed to the repo with at least 2 metrics defined, each with a data source (GitHub Actions or GitHub PR API), a baseline value, and a measurement cadence — verified by opening the file and confirming all three fields are present for each metric. |

---

### Step 2 — addresses Gap 2 (AI Champions)

| Field | Value |
|---|---|
| **Action** | Produce a one-page `ai-champion-runbook.md` committed to the repo that defines: the Champion role responsibilities for this project, the 3 prompt templates a new Champion should run first, the CLAUDE.md conventions to read before starting, and the onboarding checklist a new team member would follow to get AI-productive in under 1 hour. |
| **Owner** | Lazar Bulatovic |
| **Timeline** | 2026-07-09 |
| **Success metric** | `ai-champion-runbook.md` is committed to the repo and contains at least 4 named sections (Role responsibilities, First 3 prompts, CLAUDE.md conventions, Onboarding checklist) — verified by a colleague being able to follow it and run the first prompt template without asking for help. |

---

## Peer Review

**Reviewer:** [Name — Role]
**Date reviewed:** YYYY-MM-DD

| Review question | Reviewer answer |
|---|---|
| Is the evidence for each dimension specific and observable — not aspirational? | |
| Which score do you challenge, and why? | |
| Is each root cause a structural/behavioural cause — not a symptom? | |
| Are the success metrics measurable without asking the author? | |
| Would you sign off on this plan as a teammate? | |

---

## Revision History

| Version | Date | Change | Author |
|---|---|---|---|
| 1.0 | 2026-06-09 | Initial commit | Lazar Bulatovic |
| 1.1 | YYYY-MM-DD | Post-review update | Lazar Bulatovic |
