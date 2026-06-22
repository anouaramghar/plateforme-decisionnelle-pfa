# ENIAD Intervention Copilot Design

**Date:** 2026-06-22  
**Status:** Approved design  
**Primary outcome:** Improve intervention decisions and move staff safely from evidence to action.

## Context

The existing CopilotKit integration is a sound analytics assistant. It uses an authenticated sidecar runtime, role-protected backend tools, page-readable data, UI navigation/filter actions, risk explanation, and confirmation-gated alert drafts. The core platform has since evolved toward intervention operations: grouped signal triage, intervention cases, tasks, notes, communications, timelines, and controlled state transitions.

The main product gap is therefore not another data-query capability. Copilot is present on the Dashboard, Students, and Predictions pages, but absent from the workflow where the platform now creates the most value: triage, case planning, communication, and follow-up. Its primary UI remains a generic sidebar, so users still experience it as a chatbot beside the application.

The project currently mounts CopilotKit v2 components while several pages use legacy root-package hooks. The feature should standardize on the v2 integration surface as it adds page context, frontend tools, structured tool rendering, suggestions, and human-in-the-loop interactions.

## Product Decision

Build an embedded **Intervention Copilot**. It notices relevant signals, explains their evidence, proposes an intervention, prepares the operational work, and measures follow-up. Staff retain responsibility and explicitly confirm every consequential action.

The sidebar remains a secondary conversational surface. The primary experience consists of contextual entry points and structured cards embedded in Triage, Student Profile, and Case Detail.

The product promise is:

> It notices, explains, proposes, and helps act, while staff remain responsible.

## Alternatives Considered

### Analytics assistant

Continue expanding questions, summaries, and charts. This is comparatively easy and useful for exploration, but it still resembles a chatbot attached to a dashboard and does not strengthen the platform's intervention differentiator.

### Triage assistant

Rank students and explain urgency. This improves prioritization but stops before accountable action, leaving the user to manually recreate the recommendation as a case and task plan.

### Intervention Copilot — selected

Connect evidence, recommendation, approved action, and follow-up. This direction uses the platform's strongest workflow, provides the clearest demonstration value, and creates measurable operational outcomes.

## Goals

- Reduce the time from a new signal to an informed triage decision.
- Give every recommendation visible evidence, freshness, uncertainty, and missing-data information.
- Prevent duplicate cases by checking existing open interventions before proposing creation.
- Prepare cases, tasks, meetings, and communications as editable drafts.
- Keep all writes role-scoped, confirmation-gated, idempotent where applicable, and auditable.
- Make Copilot useful through embedded UI even when the user never opens the chat sidebar.
- Preserve normal platform operation when the LLM or Copilot runtime is unavailable.

## Non-goals

- Autonomous case creation, assignment, state transition, alerting, or email sending.
- Replacing backend authorization, validation, case-state rules, or audit logic with prompts.
- Raw natural-language-to-SQL access.
- A new background-agent service in the first increment.
- Automated claims that an intervention caused an academic improvement.
- Student or guardian access to the Copilot.

## User Experience

### Triage

Each grouped student signal exposes **Analyze with Copilot**. The action generates a structured intervention card containing:

- student identity and signal summary;
- academic and attendance evidence;
- risk source, score, explanation, and data timestamp;
- missing or stale data warnings;
- recommended priority and intervention type;
- the recommendation rationale and confidence;
- any existing open case that may receive the signals;
- one primary next action and reasonable alternatives.

The card supports four outcomes:

1. create an editable case draft;
2. link the signals to an existing case;
3. modify the recommendation;
4. dismiss the signals with a mandatory reason.

Creating or linking remains a human-confirmed action. If the underlying signals or case state change before confirmation, the preview becomes stale and must be regenerated.

### Student Profile

The profile exposes a compact **Intervention brief** rather than another chat transcript. It summarizes the student's current academic state, active risks, unresolved signals, intervention history, and data freshness. It provides contextual actions to analyze changes, open an existing case, or begin a triage recommendation.

### Case Detail

Copilot acts as a case partner and can:

- summarize the timeline and current state;
- identify missing, overdue, or unowned work;
- propose the next task, owner role, and deadline;
- draft a meeting agenda;
- draft a French student email from an approved template;
- suggest a valid next case-state transition;
- prepare a follow-up assessment comparing the current evidence with the case-opening baseline.

The interface presents these outputs as editable task, communication, transition, and follow-up cards. It never hides a consequential operation inside prose.

### Sidebar and suggestions

The sidebar remains available for open-ended exploration. Page-aware suggestion pills replace a generic welcome-only experience. Examples include **Analyze this triage group**, **Summarize this case**, **What should happen next?**, and **Draft a follow-up email**. Suggestions depend on the current route, selected entity, role, and available operations.

## Architecture

The backend remains the source of truth and owns authorization, academic access, case-state rules, validation, persistence, idempotency, and audit history.

```text
Current page context
        -> Copilot runtime
        -> role-protected backend read tools
        -> structured recommendation or draft
        -> embedded preview / human-in-the-loop card
        -> explicit user confirmation
        -> existing backend command endpoint
        -> immutable case timeline event
```

### Context layer

Triage, Student Profile, and Case Detail publish small, typed, page-specific context objects. Context contains identifiers and the information already authorized and visible to the caller. Large histories and sensitive detail are fetched through backend tools only when needed.

The context layer must include:

- current route and selected entity;
- caller role and permitted action labels, not authorization claims to be trusted by the server;
- signal, case, task, and timeline summaries relevant to the page;
- risk provenance and timestamps;
- explicit loading, unavailable, stale, and insufficient-data states.

### Decision layer

The runtime receives a role-aware intervention prompt and typed backend read tools. Initial backend tools are:

- `get_triage_context(student_id)`;
- `get_case_context(case_id)`;
- `find_existing_cases(student_id)`;
- `get_follow_up_context(case_id)`.

The runtime uses those facts to produce two schema-validated outputs: `intervention_recommendation` and `follow_up_assessment`. The backend does not generate recommendations and the model does not invent permissible domain values.

`intervention_recommendation` is a structured proposal rather than free-form prose: priority, intervention type, rationale, evidence references, confidence band, missing information, proposed owner role, proposed tasks, and alternatives. Backend responses supply the facts and allowed domain values; the LLM only synthesizes the proposal.

`follow_up_assessment` compares the case-opening baseline returned by the backend with current evidence. It may report improved, unchanged, worsened, or insufficient data. It must not claim causality.

### Draft-action layer

The runtime can prepare, but not commit:

- `draft_case`;
- `draft_tasks`;
- `draft_case_transition`;
- `draft_meeting_agenda`;
- `draft_case_communication`.

Drafts use typed schemas, have an expiry time, and carry the source entity versions or timestamps needed for stale-preview detection. Where a server-side draft is persisted, ownership is bound to the authenticated user.

### Confirmation layer

CopilotKit v2 human-in-the-loop and tool-rendering APIs display structured cards. Confirmation calls the existing intervention endpoints rather than giving the LLM direct database write tools. Backend responses remain authoritative and are reflected in TanStack Query caches and the case timeline.

The integration should migrate legacy `useCopilotReadable` and `useCopilotAction` usage to the v2 context, frontend-tool, render-tool, suggestions, and human-in-the-loop APIs. Tool names must be unique and registered at a route-stable level when a confirmation can survive navigation.

## Data and Privacy Boundaries

- The verified Copilot session identity must remain bound to every runtime and tool request.
- Backend role and academic-access checks apply independently of any prompt or frontend context.
- Teachers receive only their permitted module evidence, assigned tasks, and allowed case material.
- The smallest useful student payload is sent to the external model.
- The interface discloses that Copilot-generated content is advisory and may be externally processed under the configured NIM deployment.
- No raw SQL, unrestricted query tool, secrets, internal tokens, or private notes outside the caller's scope enter model context.
- Confirmed timeline events record the user as actor and identify Copilot as the draft source.

## Failure Handling

| Failure | Required behavior |
|---|---|
| Copilot runtime or LLM unavailable | Core triage and case workflows remain usable; show a concise unavailable state. |
| Read tool fails | Identify the missing evidence and allow retry; do not fabricate a recommendation. |
| Write confirmation times out | Show an unknown/pending result and refetch before retrying. |
| Existing open case found | Prefer linking and explain why; require an explicit override to create another case. |
| Draft expired or source changed | Reject confirmation and regenerate from current data. |
| Backend rejects a transition or command | Display the backend reason and valid alternatives; do not retry automatically. |
| Partial multi-step plan failure | Preserve successful writes, report the failed step, and never repeat completed writes automatically. |
| Risk or academic data is missing/stale | Return insufficient evidence with the missing fields and timestamps. |
| Model emits invalid structured output | Reject it at schema validation and offer regeneration. |

## Testing

### Backend

- Contract tests for every read context and draft schema.
- Role and academic-scope tests for Admin, Responsable, and Enseignant.
- Duplicate-case, stale-draft, invalid-transition, expiry, and idempotency tests.
- Audit tests proving the authenticated user is the actor and the Copilot source is retained.

### Copilot runtime

- Tool-schema validation and serialization tests.
- Prompt/evaluation cases for missing evidence, duplicate cases, invalid priorities, and role-restricted requests.
- Failure tests for backend errors, malformed tool output, expired authentication, and model unavailability.
- Assertions that the runtime exposes draft operations but no direct commit operation.

### Frontend

- Page-context tests for Triage, Student Profile, and Case Detail.
- Structured-card rendering for loading, proposal, insufficient evidence, stale, confirmed, rejected, and failed states.
- Human-in-the-loop tests proving approve, modify, cancel, and unmount paths resolve or abort cleanly.
- Cache invalidation tests after confirmed actions.

### End-to-end acceptance

1. A Responsable analyzes a grouped signal, sees evidence, finds no existing case, edits the proposal, and confirms case creation.
2. A duplicate case is detected and the user links the signals instead.
3. A teacher receives module-scoped context and cannot access private notes or confirm a forbidden transition.
4. A case summary produces a task draft and French email; both require separate confirmations.
5. An LLM outage leaves manual triage, case, task, and communication flows operational.
6. A stale preview is rejected after another user changes the case.

## Success Measures

- Median time from signal creation to triage decision.
- Recommendation acceptance, modification, and rejection rates.
- Duplicate cases prevented through link recommendations.
- Percentage of active cases with an owned next task.
- Overdue tasks surfaced and subsequently resolved.
- Follow-up assessments completed on time.
- Authorization or unintended-write incidents, with a required target of zero.

Metrics evaluate workflow assistance, not model correctness in isolation. User modifications are useful learning signals and must not be treated automatically as failures.

## Delivery Sequence

### Increment 1 — Foundation and triage loop

- Standardize new work on CopilotKit v2 and migrate the affected shared integration points.
- Add route-aware context and static/dynamic suggestions.
- Implement triage context, duplicate-case lookup, structured intervention recommendation, and the embedded intervention card.
- Support confirmed case creation or signal linking through existing endpoints.

This increment is the release gate: it must deliver one complete evidence-to-action loop before further expansion.

### Increment 2 — Case partner

- Add case summary, next-task proposal, meeting agenda, and communication drafts.
- Add separate confirmation cards for task creation, case transition, and email sending.

### Increment 3 — Follow-up and proactive attention

- Store or derive the case-opening baseline needed for comparison.
- Add non-causal follow-up assessment.
- Surface proactive attention cards for overdue work and materially changed evidence.

## Release Acceptance

- A user can complete triage-to-case action without typing a free-form chat prompt.
- Every recommendation displays evidence, freshness, uncertainty, and missing information.
- No consequential operation occurs without an explicit confirmation.
- Existing backend rules and role boundaries reject invalid or forbidden operations.
- Duplicate-case detection is part of every new-case proposal.
- Confirmed actions appear in the audit timeline with the user actor and Copilot source.
- Manual workflows remain functional when Copilot is unavailable.
- The six end-to-end acceptance scenarios pass.
