# Pre-Merge Correctness Repair Design

## Goal

Make `feature/marouane-decisionnelle` safe to merge into `main` by repairing note entry, soft-delete consistency, dashboard history, CSV imports, role-aware navigation, alert scan reporting, and the repository verification gates.

## Decisions

- Note entry is an upsert keyed by `(EtudiantId, ModuleId, Annee, Semestre)`. A matching note is updated; a new combination is created.
- Withdrawn students remain in storage for audit and historical recovery, but are excluded from operational lists, alerts, predictions, dashboards, and active reports.
- Student CSV imports are all-or-nothing. Any invalid row rejects the complete file and returns row-specific errors.
- The repair stays within the current controller/service architecture. It will not introduce a repository-layer rewrite.

## Backend Design

### Note upsert

Add an authorized upsert endpoint to `NotesController`. It validates that the student is active and the module exists, then looks up the natural note key. It updates the matching record or creates a new one, saves once, refreshes the related low-grade alert, and returns whether the record was created or updated.

The existing create and update endpoints remain for compatibility. New frontend note forms use the upsert endpoint.

### Active-student policy

Use explicit active-student predicates in operational queries rather than broad EF global filters. This avoids silently changing audit and historical queries. Apply the predicate to:

- student detail and note access;
- alert generation and active-alert dashboard counts;
- prediction cohorts;
- operational report datasets;
- absence trends and other dashboard projections.

Repeated deletion is idempotent and preserves the original withdrawal timestamp. Direct update and operational detail endpoints reject withdrawn students.

### Dashboard history

Calculate active population at an arbitrary cutoff as students created before the cutoff whose withdrawal is null or occurs on/after the cutoff. Use this rule for the 30-day delta and each sparkline point. Current totals continue to count only students with no withdrawal timestamp.

### Atomic student import

Add an Admin/Responsable bulk-import endpoint accepting normalized student rows. Validate the entire batch before mutation:

- required fields and academic-year format;
- duplicate matricules within the file;
- matricules already stored;
- valid filiere codes;
- supported levels.

If validation fails, return a structured list of row errors and write nothing. Otherwise add all students and call one `SaveChangesAsync`, relying on the database transaction around that save for atomicity.

### Alert scan result

Count newly inserted alert IDs, not the difference between open-alert totals. Resolutions and escalations no longer produce negative “created” counts. Exclude withdrawn students and their notes from the scan.

## Frontend Design

### Note forms

Fetch the module catalogue independently from existing student notes. Use the same note-upsert request from the Enseignant page, student profile, and student drawer. Show server errors and keep form data after failure.

### CSV import

Parse uploaded files with the installed `xlsx` library so BOM headers, quoting, commas, and semicolon-delimited CSV are handled by a real parser. Normalize recognized column names, send the complete batch once, and display either the imported count or every row validation error.

### Authorization and navigation

Add a reusable role guard for role-specific routes. `/enseignant` accepts Enseignant and Admin; `/responsable` accepts Responsable and Admin. The student enrollment path permits Admin and Responsable. Responsable users can open the student-management tab without gaining access to user management, data-warehouse actions, or deletion.

Hide or disable actions that the current role cannot execute, while retaining backend authorization as the security boundary.

## Verification Infrastructure

- Pin `react-apexcharts` to a release compatible with ApexCharts 3.x and regenerate the lockfile.
- Remove or replace the stale `CopilotControllerTests` references to API types that no longer exist; retain current `CopilotToolControllerTests` coverage.
- Add backend regression tests for note upsert, inactive-student exclusion, dashboard as-of counts, atomic imports, and alert scan counts.
- Add focused frontend tests for CSV normalization and role decisions, then run TypeScript build and ESLint.

## Error Handling

All new mutation endpoints return stable `{ message, errors? }` payloads. Validation errors use HTTP 400, duplicate conflicts use HTTP 409 where applicable, missing resources use HTTP 404, and authorization remains HTTP 403. Frontend forms render these messages instead of swallowing rejected promises.

## Merge Gate

The branch is ready only when:

- backend build and complete backend test suite pass;
- frontend clean install, tests, build, and lint pass;
- Docker Compose configuration validates;
- `git diff --check` is clean;
- regression tests demonstrate each repaired failure mode.
