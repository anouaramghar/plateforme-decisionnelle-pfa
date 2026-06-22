# ENIAD Student Success Platform — Release 1 Plan

## Scope decision

The original roadmap spanned three releases at ~18–25 weeks for a three-person
team. That is enterprise-product scope on top of a student project. We
deliberately cut to **Release 1 only**: the case/triage model is the actual
product differentiator and the right thing to demo. Releases 2 and 3 are
recorded as **Future Work** (see the end) — a deliberate scoping call we can
defend, not half-built features.

## Summary

Turn the current analytics-focused PFA into an operational staff tool that
converts passive alerts into owned, auditable intervention cases.

Staff-only, three roles:

- **Admin:** users, configuration, imports, institution-wide access.
- **Responsable:** filière oversight, alert triage, intervention ownership.
- **Enseignant:** assigned modules, case contributions and tasks.

Students do not access the platform. They receive email notifications from
controlled templates.

Implementation begins only after the existing correctness-remediation plan is
completed.

---

## Release 1 — Student Intervention Operations

**Target:** Turn alerts into managed actions instead of passive notifications.

### Intervention signals and triage

- Keep alerts and predictions as evidence signals, not intervention cases.
- Create a triage queue grouped by student.
- Combine related unresolved signals raised within seven days.
- Allow Responsable or Admin to:
  - dismiss a signal with a mandatory reason;
  - link it to an existing case;
  - open a new intervention case.
- Automatically assign new cases to the student’s filière Responsable; assign Admin when no Responsable exists.
- Use priorities: `Critical`, `High`, `Medium`, `Low`.
- Use signal states: `New`, `Reviewed`, `Linked`, `Dismissed`.

### Case management

Each case contains:

- Student, filière, academic period and opening reason
- Owner and contributors
- Priority, due date and escalation state
- Linked alerts, predictions, grades and absences
- Internal notes
- Tasks with owner, deadline and completion evidence
- Meetings with date, participants and result
- Outbound emails and delivery attempts
- Outcome, resolution summary and follow-up date
- Immutable activity timeline

Case states:

```text
Open → InProgress → WaitingStudent → Monitoring → Resolved → Closed
                         ↘ Escalated ↗
```

Rules:

- `Open → InProgress` requires an owner.
- `Resolved` requires an outcome and resolution summary.
- `Closed` requires the monitoring period to finish.
- Reopening a case records a reason and creates a timeline event.
- Critical overdue cases escalate automatically to Admin.
- Teachers cannot close cases or view private Responsable/Admin notes.

### Student 360° workspace

Replace disconnected student views with one staff workspace containing:

- Identity, enrollment and academic status
- Current grades, attendance and unresolved data issues
- Academic history by year and semester
- Risk trend with data freshness and explanations
- Signals and intervention cases
- Tasks, meetings and communications
- Chronological activity timeline
- Staff-uploaded supporting documents

Teacher access is limited to their assigned modules, their contributions and
their assigned tasks.

### Email communication

- Send SMTP via an already-installed library (e.g. MailKit). No provider-abstraction layer until a second provider exists.
- Configurable French templates for:
  - meeting invitation;
  - absence warning;
  - academic warning;
  - intervention follow-up;
  - case-resolution notice.
- Require staff confirmation before sending.
- Record `Queued`, `Sent` or `Failed`; do not claim delivery/read status unless the provider supports it.
- Failed messages are visible and retried with a manual **Retry** button — no background retry worker for now.
- Store the rendered subject/body as sent so later template edits do not rewrite history.

### Role dashboards

- **Admin:** unassigned/escalated cases, institution-wide SLA, email failures.
- **Responsable:** triage queue, owned cases, overdue tasks and filière risk overview.
- **Enseignant:** assigned academic work, requested case contributions and students requiring attention.

### Release 1 acceptance

- Every alert can be dismissed, linked or converted into a case.
- Every case has one accountable owner and full audit history.
- Role/privacy checks prevent teachers from accessing private case material.
- Failed emails remain visible and retryable without duplicating successful sends.
- Dashboards link directly to actionable work.
- All ENIAD filières are supported at launch.

Estimated effort for a three-person team: **4–6 weeks** after remediation.

---

## Public Interfaces and Core Types (Release 1)

Additive endpoint groups:

```text
/api/intervention-signals
/api/intervention-cases
/api/intervention-cases/{id}/tasks
/api/intervention-cases/{id}/meetings
/api/intervention-cases/{id}/communications
/api/students/{id}/timeline
```

Core additions:

- `InterventionSignal`
- `InterventionCase`
- `CaseTask`
- `CaseMeeting`
- `CaseNote`
- `CaseCommunication`
- `CaseTimelineEvent`
- `CaseOutcome`

Existing student, note, absence, alert and prediction endpoints remain
operational. Legacy endpoints are deprecated only after equivalent Release 1
functionality is live.

---

## Test and Acceptance Plan (Release 1)

### Product scenarios

- Multiple alerts for one student enter one triage context instead of creating duplicate cases.
- Responsable opens a case, assigns tasks, sends an email, monitors the result and closes it with an outcome.
- Teacher contributes module evidence but cannot see private notes or close the case.
- Critical overdue cases escalate to Admin.
- Email failure is visible and retryable without duplicating successful sends.
- Prediction outage or insufficient data never becomes a low-risk score.

### Rollout gate

Before launching across all ENIAD programs:

- Role/privacy acceptance tests pass.
- Email templates and sender identity are approved.
- Backup, restore and audit-export procedures are tested.
- Representatives of all three roles complete scripted acceptance scenarios.

---

## Assumptions and Defaults

- ENIAD is the only institution; multi-tenancy and branding are excluded.
- Only Admin, Responsable and Enseignant access the platform.
- Students and guardians have no accounts.
- Email is the only outbound student channel.
- Responsable owns intervention cases for their filière; teachers have limited contribution access.
- Alerts enter a triage queue; they do not automatically become cases.

---

## Future Work (explicitly deferred — not in this PFA)

Recorded so the full problem is visible and the cut is a deliberate decision.
Each item is deferred because it is speculative for a one-semester project or
rebuilds a SIS we do not need yet.

### Release 2 — Academic Operations and Rule Engine

- Effective-dated academic structure (year/semester/offering/teaching assignment/assessment scheme).
- Timetable with scheduled sessions; absence-hours derived from session duration.
- Gradebook from versioned assessment schemes.
- **Configurable, program-versioned academic rule engine** (passing thresholds, compensation, credit acquisition, retake eligibility, progression). *Deferred:* ENIAD's actual rules are hardcodeable today; no second rule set exists yet.
- Template-based, preview-before-commit Excel imports and a data-quality center.

### Release 3 — Formal Decisions and Responsible Intelligence

- Jury workflow with frozen immutable input snapshots and official PDF/XLSX minutes. *Keep the honesty discipline (frozen inputs, never overwrite finalized history) if this is ever built.*
- Intervention effectiveness analytics (signal-to-triage time, before/after attendance and grades, outcome rates).
- Responsible-ML monitoring (drift, false negatives, out-of-time evaluation). *Deferred:* current model AUC is inflated by synthetic data sharing one per-student profile (see CLAUDE.md); drift monitoring needs real longitudinal data a one-semester PFA won't produce. Keep "insufficient data, not a fake score."
- Copilot acting inside workflows (draft-only, role-scoped, confirmation-gated, audited).
