# Student Outreach Intervention Design

**Date:** 2026-06-24
**Status:** Approved design
**Primary outcome:** Ensure that staff contact high-risk students, hold a meeting, and record its outcome.

## Context

The platform already provides student profiles, risk predictions, alerts, grouped triage, intervention cases, communications, and audit history. However, its visible product story still centers on displaying information. A user can learn that a student is struggling without being guided through one concrete institutional response.

The first action-oriented release will focus on a deliberately small intervention: the Responsable contacts the student by email, schedules a meeting, and records the meeting outcome. Broader intervention plans, teacher task assignment, calendar integrations, and student self-service scheduling remain possible later, but are outside this release.

The product promise is:

> The platform does not stop at identifying risk; it helps staff contact the student and verify that a real meeting happened.

## Product Decision

Build a dedicated student-outreach workflow on the existing intervention-case infrastructure. The workflow has four user-facing stages:

1. **Needs contact** — the Responsable reviews the evidence and starts an intervention.
2. **Email prepared** — the platform generates an editable French email draft.
3. **Meeting scheduled** — the Responsable selects a date, time, and location, approves the email, and sends it.
4. **Meeting held** — the Responsable records attendance and a short outcome; only then is the intervention complete.

AI assists with drafting but never sends a message or changes case state autonomously.

## Alternatives Considered

### Extend the Alerts page

Adding a contact action beside each risk alert would be quick, but it would crowd an already analytical page and make follow-through less visible.

### Dedicated intervention workflow — selected

A dedicated work queue makes ownership, progress, overdue work, and completion explicit. It also reuses the platform's existing triage, case, communication, and audit foundations.

### AI email assistant only

An email generator would demonstrate AI, but would not prove that contact was sent, a meeting happened, or an outcome was recorded.

## Goals

- Convert a high-risk signal into a traceable outreach action.
- Reduce the time between risk detection and student contact.
- Give the Responsable one clear queue of interventions requiring attention.
- Generate a supportive, personalized French email that staff can edit.
- Record the scheduled meeting and its outcome.
- Prevent an intervention from appearing complete before the meeting record exists.
- Preserve backend authorization and a complete audit trail.
- Keep the manual workflow usable when AI is unavailable.

## Non-goals

- Automatically sending email when risk is detected.
- Allowing the student to select a meeting slot.
- Google Calendar or Microsoft Outlook integration.
- Full intervention plans, tutoring programs, or teacher task assignment.
- Student or guardian access to the platform.
- Autonomous case creation, case transition, or meeting completion.
- Proving that the meeting caused an academic improvement.

## Users and Responsibilities

### Responsable pédagogique

The Responsable owns the outreach intervention from creation to completion. They review evidence, approve or edit the email, choose the meeting details, send the message, and record the outcome.

### Enseignant

Teachers can report academic concerns and view intervention progress within their authorized academic scope. They cannot send outreach email, schedule the meeting on behalf of the Responsable, or complete the intervention in this release.

### Admin

Administrators manage users and email templates and can monitor aggregate results. They do not normally execute student interventions.

## User Experience

### Triage

Grouped alerts for a student expose a primary **Start intervention** action. Before creation, the backend checks for another non-terminal intervention for the same student. If one exists, the interface directs the Responsable to it rather than creating a duplicate.

The creation preview shows the student, the relevant academic and attendance evidence, the risk source and timestamp, the reason for outreach, the proposed priority, and the assigned Responsable.

### Intervention queue

The existing Cases experience becomes a clearly named **Interventions** work queue. Its primary organization uses four user-facing stages:

- Needs contact;
- Email prepared;
- Meeting scheduled;
- Meeting held.

Each item displays the student, priority, owner, current stage, last activity, and the next relevant deadline. Filters cover owner, filière, priority, and stage.

### Intervention detail

One detail view contains:

- the student and risk-evidence summary;
- evidence provenance and freshness;
- an editable email draft;
- recipient, subject, meeting date, time, and location;
- email delivery state and retry action;
- a chronological intervention timeline;
- the meeting attendance and outcome form.

AI is embedded in the **Generate email draft** action. A separate chat interaction is not required.

## Email Draft

The generated message is in French and uses a supportive, non-accusatory tone. It may use the student's name, general areas of concern, the purpose of the meeting, and the selected meeting details. It must not expose internal risk scores, model labels, private staff notes, or unnecessarily sensitive academic detail.

Before sending, the Responsable must review:

- recipient address;
- subject;
- complete body;
- meeting date and time;
- meeting location.

The final rendered subject and body are stored exactly as sent so later template or prompt changes cannot rewrite history.

## Architecture and Data Model

The existing backend remains authoritative for identity, academic scope, case-state transitions, persistence, email delivery, and audit history.

### Existing records

- `InterventionCase` remains the central owned intervention.
- `CaseCommunication` remains the outbound email record.
- `CaseTimelineEvent` records important user and system events.
- Existing alert-to-case links retain the evidence that opened the intervention.

### Required extensions

- `CaseCommunication.Status` supports `Draft`, `Queued`, `Sent`, and `Failed`.
- A draft communication stores its editable subject and body before delivery.
- The intervention case stores `MeetingScheduledFor` and `MeetingLocation`.
- Meeting completion records `MeetingAttendance`, `MeetingHeldAt`, `Outcome`, and `ResolutionSummary`. Attendance values are `Held`, `Absent`, or `Cancelled`; `MeetingHeldAt` is set only for `Held`.

This release supports one scheduled outreach meeting per intervention. Rescheduling updates the meeting fields and adds an immutable timeline event containing the previous and new schedule. Supporting multiple meetings is deferred.

### State mapping

The existing backend workflow remains stable. The interface maps its relevant states to clearer outreach labels:

| Backend state | User-facing stage | Entry requirement |
|---|---|---|
| `Open` | Needs contact | Intervention created with an owner |
| `InProgress` | Email prepared | Editable communication draft exists |
| `WaitingStudent` | Meeting scheduled | Email sent successfully and future meeting details saved |
| `Resolved` | Meeting held | Attendance is `Held`, and the held time and outcome summary are recorded |

Other existing states, such as `Escalated`, `Monitoring`, and `Closed`, remain available to the general case workflow but are not primary columns in the first outreach queue. They appear through explicit status badges and filters when present.

## Data Flow

```text
Risk alert or grouped triage signal
    -> duplicate intervention check
    -> Responsable creates owned intervention
    -> current authorized evidence is loaded
    -> AI or template creates editable French email draft
    -> Responsable edits draft and selects meeting details
    -> backend validates and attempts email delivery
    -> successful delivery advances to Meeting scheduled
    -> Responsable records meeting attendance and outcome
    -> backend resolves intervention and records timeline event
```

Each command is validated independently. A later stage cannot be reached merely by changing a frontend label.

## Validation and Failure Handling

| Situation | Required behavior |
|---|---|
| Duplicate active intervention | Direct the user to the existing intervention; do not create another by default. |
| Missing student email | Keep the intervention in Needs contact and show the missing field. |
| AI unavailable | Offer a deterministic template-based draft and preserve the manual editor. |
| Stale or missing evidence | Show the affected evidence and timestamp; do not invent details. |
| Invalid meeting time | Reject past dates and explain the correction required. |
| Email delivery fails | Preserve the edited message, mark delivery Failed, and allow an explicit retry. |
| Email result is unknown | Refetch delivery state before offering retry to avoid duplicate messages. |
| Unauthorized action | Return the backend authorization error; never rely on hidden UI alone. |
| Meeting not attended | Record `Absent` or `Cancelled`, keep the intervention in Meeting scheduled, and require rescheduling before completion. |
| Missing outcome | Reject completion until attendance and a non-empty summary are supplied. |

The workflow advances to Meeting scheduled only after confirmed email delivery. Every confirmed change records the authenticated user, timestamp, prior value where relevant, and resulting state in the audit timeline.

## Testing

### Backend

- Duplicate active-intervention detection.
- Role and academic-scope checks for Responsable, Enseignant, and Admin.
- Communication draft creation, editing, validation, sending, failure, and retry.
- Protection against accidental duplicate email sends after an unknown result.
- Meeting scheduling, rescheduling, and past-date rejection.
- Meeting completion guards requiring `Held`, a held timestamp, and an outcome; absent or cancelled meetings remain active.
- State-transition and audit-timeline assertions.

### Frontend

- Triage-to-intervention creation and duplicate redirection.
- Four-stage queue rendering and filtering.
- Draft loading, editing, validation, and review states.
- Email sent, failed, unknown, and retry states.
- Meeting scheduling and outcome form validation.
- Teacher read-only behavior for restricted actions.
- Manual template fallback when AI is unavailable.

### End-to-end acceptance

1. A Responsable converts an authorized high-risk signal into an owned intervention.
2. The platform generates a personalized French email draft from current evidence.
3. The Responsable edits the draft, selects a future meeting time and location, and confirms sending.
4. Successful delivery moves the intervention into Meeting scheduled.
5. A failed delivery preserves the draft and can be retried once explicitly.
6. After the meeting, the Responsable records `Held` attendance and a short outcome; an absent or cancelled meeting stays active for rescheduling.
7. Only a held timestamp and valid outcome move the intervention to Meeting held.
8. A teacher can view authorized progress but cannot send or resolve the intervention.
9. All confirmed actions appear in the intervention timeline.
10. The complete manual workflow remains usable while AI is unavailable.

## Success Measures

- Percentage of eligible high-risk students with outreach started.
- Median time from risk detection to email sent.
- Email delivery success and failure rates.
- Percentage of scheduled meetings marked held, absent, or cancelled.
- Percentage of held meetings with a recorded outcome.
- Number of duplicate interventions prevented.
- Authorization or unintended-send incidents, with a required target of zero.

These metrics measure whether the platform moves staff from evidence to accountable action. They do not claim that outreach caused a later academic result.

## Delivery Sequence

### Increment 1 — Intervention queue and meeting data

- Add the outreach fields and state presentation.
- Refocus Cases as the Interventions queue.
- Add duplicate prevention and the meeting schedule/outcome forms.

### Increment 2 — Draft, review, and send

- Add persisted communication drafts.
- Add the deterministic French template fallback.
- Add AI-assisted draft generation, review, send, delivery state, and retry.

### Increment 3 — Measurement and polish

- Add outreach funnel metrics.
- Add overdue and missing-outcome indicators.
- Complete role, audit, failure, and end-to-end coverage.

## Release Acceptance

- The primary demo completes the full alert-to-held-meeting loop without using a generic chatbot.
- No email is sent without explicit Responsable confirmation.
- No intervention is shown as complete without attendance and an outcome summary.
- Delivery failures and missing student data are visible and recoverable.
- Duplicate active interventions are prevented by default.
- Backend authorization and academic scope remain authoritative.
- Confirmed actions are attributable in the timeline and audit history.
- The workflow remains usable without AI.
