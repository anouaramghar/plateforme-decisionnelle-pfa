# ENIAD Student Success Platform Product Plan

## Summary

Transform the current analytics-focused PFA into an operational staff platform that detects student difficulties, coordinates interventions, manages academic activity, and records formal decisions.

The product remains ENIAD-first and staff-only, using three roles:

- **Admin:** users, academic configuration, imports, data quality, institution-wide access.
- **Responsable:** filière oversight, alert triage, intervention ownership, jury preparation.
- **Enseignant:** assigned modules, attendance, grades, case contributions and tasks.

Students do not access the platform. They receive email notifications generated from controlled templates.

Implementation begins only after the existing correctness-remediation plan is completed.

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

Teacher access is limited to their assigned modules, their contributions and their assigned tasks.

### Email communication

- Use SMTP through a provider-neutral email service.
- Support configurable French templates for:
  - meeting invitation;
  - absence warning;
  - academic warning;
  - intervention follow-up;
  - case-resolution notice.
- Require staff confirmation before sending.
- Record `Queued`, `Sent` or `Failed`; do not claim delivery/read status unless the provider supports it.
- Retry failed messages manually or through a bounded background retry.
- Store the rendered subject/body as sent so later template edits do not rewrite history.

### Role dashboards

- **Admin:** unassigned/escalated cases, institution-wide SLA, email failures and data-quality blockers.
- **Responsable:** triage queue, owned cases, overdue tasks and filière risk overview.
- **Enseignant:** assigned academic work, requested case contributions and students requiring attention.

### Release 1 acceptance

- Every alert can be dismissed, linked or converted into a case.
- Every case has one accountable owner and full audit history.
- Role/privacy checks prevent teachers from accessing private case material.
- Failed emails remain visible and retryable.
- Dashboards link directly to actionable work.
- All ENIAD filières are supported at launch.

Estimated effort for a three-person team: **5–7 weeks** after remediation.

---

## Release 2 — Academic Operations and Rule Engine

**Target:** Make the data originate from realistic academic workflows.

### Academic structure

Add effective-dated configuration for:

```text
AcademicYear
└── Semester
    ├── Filière
    ├── Level
    ├── Cohort / Group
    ├── ModuleOffering
    ├── TeachingAssignment
    └── AssessmentScheme
```

- A module definition is reusable.
- A module offering binds it to a year, semester, filière and level.
- Replace the single teacher-module relationship with teaching assignments supporting multiple modules and groups.
- Prevent changes to finalized academic periods except through an audited reopening action.

### Timetable and attendance

- Define scheduled teaching sessions by offering, group, teacher, room and duration.
- Teacher opens a session attendance sheet.
- Statuses: `Present`, `Absent`, `Late`, `Excused`.
- Absence hours derive from session duration.
- Corrections require a reason and remain audited.
- Justification workflow: `Pending`, `Accepted`, `Rejected`.
- Recalculate absence signals after every correction or justification decision.
- Detect missing attendance sheets and duplicate/conflicting sessions.

### Assessment and gradebook

- Configure assessment components such as exam, project, practical work and continuous assessment.
- Define weights, score ranges, deadlines and whether a component is mandatory.
- Teachers enter or import scores only for their assignments.
- Calculate final grades from the active assessment scheme.
- Preserve existing final-grade APIs temporarily for compatibility.
- Lock grades after validation; reopening requires Responsable/Admin authorization and a reason.
- Track missing, late and incomplete grade submissions.

### Configurable academic rules

Rule sets are versioned by program and academic year and cover:

- Module passing threshold
- Minimum examination score
- Component weights
- Semester compensation
- Credit acquisition
- Retake eligibility
- Attendance-based exclusion
- Progression requirements
- Permitted jury overrides

Every calculated decision must include an explanation, for example:

> Module not validated: final grade 11.2, but examination grade 6.5 is below the required minimum of 7.

### Excel imports and data-quality center

Provide template-based, preview-before-commit imports for:

- Students and enrollments
- Module offerings
- Teacher assignments
- Timetables
- Assessment definitions
- Grades

Import behavior:

- Validate the entire workbook before mutation.
- Show row-level errors and correction guidance.
- Commit atomically.
- Preserve import history, uploader, source file hash and summary.
- Reject duplicate imports unless explicitly confirmed.

Data-quality checks include:

- Students without enrollment/group
- Modules without teachers
- Sessions without attendance
- Missing or overdue grades
- Invalid academic periods
- Conflicting assignments
- Stale predictions
- Failed email jobs
- Records excluded from calculations

### Release 2 acceptance

- Staff can configure a complete academic year through UI and Excel.
- Attendance originates from scheduled sessions.
- Grades originate from versioned assessment schemes.
- Academic results contain human-readable rule explanations.
- Imports are previewable, atomic and auditable.
- No finalized period changes silently.

Estimated effort: **7–10 weeks**.

---

## Release 3 — Formal Decisions and Responsible Intelligence

**Target:** Support institutional decisions and measure whether interventions work.

### Jury workflow

- Create jury sessions for an academic period and population.
- Freeze an immutable input snapshot when preparation begins.
- Present:
  - grades and credits;
  - failed modules;
  - attendance exclusions;
  - compensation eligibility;
  - unresolved data issues;
  - active interventions;
  - proposed rule-based decision.
- Allow permitted overrides with mandatory justification.
- Workflow: `Draft → Prepared → InReview → Approved → Finalized`.
- Finalization locks decisions and generates official PDF/XLSX minutes.
- Corrections require a new revision; finalized history is never overwritten.

### Intervention effectiveness

Measure:

- Time from signal to triage
- Time from case opening to first action
- Overdue tasks and SLA compliance
- Meetings completed
- Email success/failure
- Attendance before and after intervention
- Grade/risk change after intervention
- Resolution and escalation rates
- Outcomes by intervention type, filière and cohort
- Recurring cases after closure

Use case outcomes:

- `Improved`
- `Stable`
- `Deteriorated`
- `Withdrawn`
- `AdministrativeResolution`
- `UnableToContact`
- `NoLongerApplicable`

### Responsible ML

- Predictions prioritize attention; they never produce official academic decisions.
- Show risk trend, contributing factors, confidence/data sufficiency, model version and training-data provenance.
- Display “insufficient data” instead of a numeric score when evidence is inadequate.
- Collect staff feedback: `Accurate`, `Inaccurate`, `AlreadyHandled`, `NotEnoughContext`.
- Evaluate models out-of-time and by student, program and level.
- Monitor false negatives, false positives, drift and stale models.
- Keep deterministic academic rules separate from predictive models.

### Copilot inside workflows

Copilot remains an assistant, not the primary UI.

Allowed assistance:

- Summarize a student’s evidence
- Explain risk factors
- Draft internal case summaries
- Draft student emails
- Suggest intervention tasks
- Summarize cohort or jury information
- Query authorized institutional data

Controls:

- Every mutation requires explicit staff confirmation.
- Copilot inherits the user’s role and data scope.
- Drafts are visibly distinguished from saved records.
- Sources and freshness accompany factual summaries.
- Sensitive private notes are excluded unless the caller is authorized.
- All tool calls and confirmations are audited.

### Release 3 acceptance

- Jury decisions are reproducible from frozen inputs and effective rule sets.
- Overrides are attributable and justified.
- Management can measure intervention outcomes rather than only alert volume.
- ML pages communicate uncertainty and provenance.
- Copilot cannot bypass normal authorization or confirmation workflows.

Estimated effort: **6–8 weeks**.

---

## Public Interfaces and Core Types

Additive endpoint groups:

```text
/api/intervention-signals
/api/intervention-cases
/api/intervention-cases/{id}/tasks
/api/intervention-cases/{id}/meetings
/api/intervention-cases/{id}/communications
/api/students/{id}/timeline

/api/academic-years
/api/semesters
/api/module-offerings
/api/teaching-assignments
/api/sessions
/api/attendance-sheets
/api/assessment-schemes
/api/gradebooks
/api/academic-rule-sets
/api/import-jobs
/api/data-quality

/api/juries
/api/juries/{id}/students
/api/juries/{id}/finalize
/api/intervention-metrics
/api/model-monitoring
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
- `AcademicYear`
- `Semester`
- `Enrollment`
- `CohortGroup`
- `ModuleOffering`
- `TeachingAssignment`
- `TeachingSession`
- `AttendanceRecord`
- `AttendanceJustification`
- `AssessmentScheme`
- `AssessmentComponent`
- `AssessmentScore`
- `AcademicRuleSet`
- `ImportJob`
- `DataQualityIssue`
- `JurySession`
- `JuryDecision`
- `JuryRevision`

Existing student, note, absence, alert and prediction endpoints remain operational until their frontend consumers migrate. Legacy endpoints are deprecated only after equivalent release functionality is live.

---

## Test and Acceptance Plan

### Product scenarios

- Multiple alerts for one student enter one triage context instead of creating duplicate cases.
- Responsable opens a case, assigns tasks, sends an email, monitors the result and closes it with an outcome.
- Teacher contributes module evidence but cannot see private notes or close the case.
- Critical overdue cases escalate to Admin.
- Email failure is visible and retryable without duplicating successful sends.
- Session correction updates attendance totals and reevaluates the associated signal.
- Grade calculations follow the rule set effective for the student’s program and year.
- Excel imports reject the entire workbook when any blocking row is invalid.
- Jury finalization prevents silent edits and preserves revisions.
- Prediction outage or insufficient data never becomes a low-risk score.
- Copilot drafts actions but cannot execute them without confirmation.

### Institution-wide rollout gate

Before launching across all ENIAD programs:

- Every active student has one valid enrollment, filière, level and group.
- Every active module offering has an assigned teacher and assessment scheme.
- Academic calendars and rule sets are approved by Admin/Responsable representatives.
- Blocking data-quality issues are zero.
- Role/privacy acceptance tests pass.
- Email templates and sender identity are approved.
- Backup, restore and audit-export procedures are tested.
- Representatives of all three roles complete scripted acceptance scenarios.

---

## Assumptions and Defaults

- ENIAD is the only institution; multi-tenancy and branding are excluded.
- Only Admin, Responsable and Enseignant access the platform.
- Students and guardians have no accounts in these releases.
- Email is the only outbound student channel initially.
- Admin absorbs scolarité and institution-management responsibilities.
- Responsable owns intervention cases for their filière.
- Teachers have limited case contribution access.
- Alerts enter a triage queue; they do not automatically become cases.
- Initial data maintenance uses UI and validated Excel imports.
- Academic rules are configurable and effective-dated.
- All ENIAD programs launch together after institution-wide acceptance testing.
- Mobile applications, guardian access, SMS, WhatsApp and external SIS integration remain outside this roadmap.
- Total product expansion is approximately **18–25 weeks** for a three-person team after the existing remediation work.
