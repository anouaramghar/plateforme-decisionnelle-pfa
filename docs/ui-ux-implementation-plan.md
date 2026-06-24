# ENIAD Platform UI/UX Remediation Plan

Date: 2026-06-24
Target: `frontend/`
Audience: implementation agent working in the existing repository

## Objective

Implement the complete UI/UX audit remediation across the Admin, Responsable, and Enseignant experiences without redesigning the product's established visual identity.

The finished platform must:

- remain usable when Copilot is unavailable;
- work at 390 px, 768 px, 1024 px, and desktop widths;
- expose meaningful accessible names and keyboard behavior;
- use consistent French copy and navigation metadata;
- protect destructive or computationally expensive operations with clear confirmation and feedback;
- preserve all existing role permissions and API contracts.

## Important repository constraints

- The worktree already contains unrelated user changes. Do not reset, discard, or overwrite them.
- Do not change backend schemas or API contracts unless a frontend requirement is impossible without doing so.
- Keep the JWT and refresh token in memory as currently designed.
- Preserve the existing Tailwind, CSS-variable, ApexCharts, TanStack Query, and CopilotKit stack.
- Do not replace the current design system or introduce a second component library.
- Use French for user-facing platform copy. Technical model identifiers may remain unchanged.
- Treat existing tests as contracts. Update a test only when the intended UX has deliberately changed.

## Out of scope

- New product features unrelated to the audit.
- Backend authorization changes.
- A new brand identity or wholesale page redesign.
- Replacing CopilotKit, ApexCharts, TanStack Query, or React Router.
- Changing the desktop information architecture beyond the corrections listed below.

## Required implementation approach

For every phase:

1. Add or update focused tests first.
2. Implement the smallest coherent change.
3. Run the focused tests.
4. Run `npm run lint`, `npm run test`, and `npm run build` before considering the phase complete.
5. Verify the affected routes in a real browser at the required viewport sizes.

Do not combine all work into one large patch. Keep phases independently reviewable.

---

## Phase 0 — Baseline and audit harness

### Purpose

Establish a reproducible baseline before modifying behavior.

### Actions

- Inspect `git status --short` and preserve all existing modifications.
- Run from `frontend/`:

  ```powershell
  npm run lint
  npm run test
  npm run build
  ```

- Run the existing Playwright smoke tests when the live stack and credentials are available.
- Add a small viewport matrix to the Playwright configuration or a dedicated UI audit spec:
  - mobile: 390 × 844;
  - tablet: 768 × 1024;
  - small desktop: 1024 × 768;
  - desktop: 1280 × 720.
- Record existing failures separately from regressions introduced by this plan.

### Acceptance criteria

- Baseline command results are documented in the final handoff.
- No pre-existing user change is lost.
- Browser tests can target all four viewport sizes.

---

## Phase 1 — Make Copilot non-blocking

### Primary files

- `frontend/src/layout/AppShell.tsx`
- `frontend/src/main.tsx`
- `frontend/src/index.css`
- `frontend/src/layout/AppShell.test.tsx` or a new focused test file
- `frontend/e2e/smoke.spec.ts`

### Problems to solve

- `CopilotSidebar` currently uses `defaultOpen={true}`.
- At mobile width, the Copilot surface can cover the entire application.
- Copilot runtime failures can surface developer-oriented errors over login or application pages.
- The development inspector/announcement UI must never appear in a production build.
- Closing the mobile Copilot surface must be obvious and reliable.

### Implementation

1. Change the authenticated Copilot sidebar to be closed by default.
2. Drive its open state from one controlled state source rather than mixing implicit CopilotKit state and local state.
3. At widths below 640 px:
   - render Copilot as a full-screen modal/drawer;
   - provide a visible 44 × 44 px close button in the header;
   - prevent background scrolling;
   - restore focus to the Copilot launcher after closing;
   - close on `Escape`.
4. At desktop widths:
   - use a right-side drawer with an explicit width and maximum viewport width;
   - ensure it does not silently hide primary page actions.
5. When the Copilot session or runtime is unavailable:
   - keep the main platform fully usable;
   - show one friendly, dismissible French status message only after the user opens Copilot;
   - do not expose stack traces, runtime status codes, or developer terminology.
6. Determine the supported CopilotKit option for disabling its development inspector. Gate any inspector/debug functionality behind `import.meta.env.DEV`; confirm it is absent from the production build.
7. Do not automatically open Copilot after login or route changes.

### Tests

- App shell renders with Copilot closed by default.
- Opening and closing Copilot works with mouse and keyboard.
- `Escape` closes the Copilot drawer.
- Focus returns to the launcher.
- Runtime failure does not replace or block route content.
- At 390 px, the user can close Copilot and use the page underneath.
- Production build does not render the web inspector or announcement bubble.

### Acceptance criteria

- No Copilot UI is visible until explicitly opened.
- Copilot failure never blocks login, routing, or page interaction.
- No development inspector is present in `npm run build` output when served.

---

## Phase 2 — Remove the post-login routing race

### Primary files

- `frontend/src/context/AuthContext.tsx`
- `frontend/src/pages/Login.tsx`
- `frontend/src/App.tsx`
- `frontend/src/context/AuthContext.test.tsx` or a new auth integration test

### Problems to solve

- `login()` schedules React state updates and immediately returns the role.
- `Login` immediately navigates to the role home while `ProtectedRoute` may still observe a null token.
- The result can be a route change with the login screen still rendered or a second-submit requirement.

### Implementation

1. Add an explicit authentication status such as `checking | anonymous | authenticated` to `AuthContext`.
2. Ensure successful login commits the token and user before protected navigation is evaluated.
3. Prefer routing from a state-aware effect or authenticated redirect rather than relying on a race between `setToken()` and `navigate()`.
4. Keep the login button disabled and show a spinner while authentication is pending.
5. Prevent duplicate submissions.
6. Keep the entered email after invalid credentials; clear or leave the password according to the current security convention.
7. Preserve role destinations:
   - Admin → `/dashboard`;
   - Responsable → `/responsable`;
   - Enseignant → `/enseignant`.

### Tests

- One successful click reaches the correct role home.
- Protected content renders without a transient login screen.
- Double-clicking submit sends only one request.
- Failed login remains on `/login` with an accessible error message.
- Copilot session failure after a valid platform login does not invalidate platform authentication.

### Acceptance criteria

- A successful login never requires a second submission.
- Authentication and Copilot availability are independent states.

---

## Phase 3 — Correct breadcrumbs and route metadata

### Primary files

- `frontend/src/layout/Topbar.tsx`
- optionally a new `frontend/src/navigation/routeMeta.ts`
- route metadata tests

### Problems to solve

The fallback `Pilotage › Page` appears on valid routes such as interventions, case details, student profiles, and role workspaces.

### Implementation

1. Move route labels into one shared route-metadata map.
2. Support static and dynamic patterns:
   - `/dashboard` → Pilotage / Tableau de bord;
   - `/students` → Pilotage / Étudiants;
   - `/students/:id` → Étudiants / Fiche étudiant;
   - `/alerts` → Pilotage / Alertes;
   - `/cases` → Pilotage / Interventions;
   - `/cases/:id` → Interventions / Détail du cas;
   - `/predictions` → Pilotage / Prédictions ML;
   - `/reports` → Pilotage / Rapports;
   - `/admin` → Système / Administration;
   - `/settings` → Système / Paramètres;
   - `/enseignant` → Enseignement / Mon espace;
   - `/responsable` → Pilotage / Mon espace.
3. Render breadcrumbs as semantic navigation with `aria-label="Fil d’Ariane"`.
4. Make the parent crumb a link when it has a meaningful destination.

### Tests

- Test every route above, including representative dynamic IDs.
- Confirm no supported route displays `Page`.

### Acceptance criteria

- Every reachable page has correct, localized route context.

---

## Phase 4 — Responsive application shell and top bar

### Primary files

- `frontend/src/layout/AppShell.tsx`
- `frontend/src/layout/Topbar.tsx`
- `frontend/src/layout/Sidebar.tsx`
- `frontend/src/index.css`

### Implementation

1. Define intentional breakpoints for mobile, tablet, small desktop, and desktop instead of relying only on the `<640 px` global grid override.
2. Mobile top bar:
   - retain menu, page title, alert indicator, and one overflow/action menu;
   - replace the full search field with a search icon that opens the command palette;
   - move `Nouveau`, theme, and Copilot into an overflow menu when space is insufficient;
   - hide desktop keyboard hints.
3. Tablet top bar:
   - allow compact search;
   - prevent actions from overlapping or clipping.
4. Mobile navigation drawer:
   - trap focus while open;
   - close on `Escape`, backdrop click, and route change;
   - restore focus to the menu button;
   - expose `aria-expanded` and `aria-controls`.
5. Use `100dvh` with a safe fallback instead of relying only on `100vh`.
6. Ensure main content has no unintended page-level horizontal overflow.

### Tests

- Visual/browser verification at all four viewport sizes.
- Keyboard test for opening and closing navigation.
- Assert all primary top-bar actions remain reachable at 390 px.

### Acceptance criteria

- No clipped top-bar actions or overlapping drawers at any target viewport.
- Navigation and primary actions remain reachable without horizontal page scrolling.

---

## Phase 5 — Accessibility pass across shared controls

### Primary files

- `frontend/src/components/ui/*`
- `frontend/src/layout/*`
- all table-heavy pages

### Implementation

1. Introduce or standardize a shared icon-button pattern requiring an accessible label.
2. Add accessible names to:
   - student row actions;
   - teacher workspace row actions;
   - prediction-history row actions;
   - pagination previous/next buttons;
   - notification button;
   - table selection checkboxes;
   - the Settings sidebar switch.
3. Associate every checkbox, switch, select, and input with a visible label or `aria-label`.
4. For select-all checkboxes, support `indeterminate` state when only some rows are selected.
5. Ensure dialogs:
   - have a title referenced by `aria-labelledby`;
   - trap focus;
   - close on `Escape` where safe;
   - return focus to the trigger.
6. Ensure status is not communicated by color alone; keep text/icon indicators.
7. Add `aria-live="polite"` to non-critical success messages and `role="alert"` to submission failures.
8. Verify visible focus styling in both light and dark themes.
9. Maintain 44 px touch targets for important mobile actions.

### Tests

- Add `jest-dom` assertions for accessible names and roles.
- Use `getByRole` in tests instead of structural selectors.
- Run an automated accessibility scan if an existing compatible tool is available; do not add a large dependency solely for this plan without approval.
- Complete a manual keyboard pass: login, navigation, table row action, modal, filters, settings.

### Acceptance criteria

- No interactive control in the tested routes is exposed with an empty accessible name.
- All primary workflows can be completed without a mouse.

---

## Phase 6 — Students and student profile

### Primary files

- `frontend/src/pages/Students.tsx`
- `frontend/src/pages/StudentProfile.tsx`
- `frontend/src/components/charts/index.tsx`
- related tests

### Students page

1. Keep the desktop table but place it inside an explicitly labeled horizontal-scroll region at narrow widths.
2. At mobile width, prefer a compact student-card presentation if it can be implemented without duplicating business logic; otherwise make the table scroll affordance obvious.
3. When one or more rows are selected, show a bulk-action bar or remove selection if no bulk action exists.
4. Label the select-all checkbox and each row checkbox with the student's name.
5. Give the row action a clear label such as `Voir la fiche de Sara Benali`.
6. Show active filters as removable chips and provide `Réinitialiser les filtres`.
7. Preserve filter/search state when returning from a profile.

### Student profile

1. Keep the current academic summary and risk hierarchy.
2. Replace deterministic wording such as “probabilité de décrochage” with calibrated language such as “estimation du risque par le modèle”.
3. Add a compact explanation:
   - score is an aid to prioritization, not a final decision;
   - last model update/time;
   - most important increasing and reducing factors.
4. Translate “SHAP” into user language in the default heading; keep the technical term in help text.
5. Ensure teacher-only editing actions are displayed only when authorized.
6. Make notes tables responsive and keep module/code context visible.

### Acceptance criteria

- Selection has an obvious purpose.
- Returning from a student profile preserves the list context.
- ML language is cautious and understandable to non-technical staff.

---

## Phase 7 — Alerts and triage

### Primary files

- `frontend/src/pages/Alertes.tsx`
- `frontend/src/pages/Alerts.tsx`
- `frontend/src/pages/Triage.tsx` if still referenced
- related tests

### Implementation

1. Keep `Triage` and `Journal` as the two top-level modes.
2. Replace raw backend enums in the UI:
   - `NoteFaible` → `Note faible`;
   - `AbsenceExcessive` → `Absences excessives`;
   - `RisqueEchec` → `Risque élevé (ML)`.
3. Rename `Tout marquer lu` to `Tout résoudre` if it performs resolution. If the product truly needs read/unread state, implement it separately rather than conflating it with resolution.
4. Before bulk resolution, show the number of affected alerts and require confirmation.
5. Keep rejection/dismissal behind a dialog requiring a reason.
6. After resolve/reject:
   - update the list immediately;
   - announce success;
   - provide a short undo action only if the backend supports a reversible operation.
7. Clarify auto-refresh with a subtle last-updated timestamp; do not let polling visibly reorder a row while the user is interacting with it.
8. At mobile width, stack alert metadata and keep the primary action visible.

### Tests

- Localization mapping for every supported alert type.
- Bulk action affects only the visible filtered set.
- Confirmation contains the correct count.
- Filters, tabs, and success feedback are keyboard accessible.

### Acceptance criteria

- Terminology accurately matches the backend action being taken.
- No raw alert enum is displayed to users.

---

## Phase 8 — Intervention board and case detail

### Primary files

- `frontend/src/pages/Cases.tsx`
- `frontend/src/pages/CaseDetail.tsx`
- `frontend/src/components/interventions/OutreachComposer.tsx`
- `frontend/src/components/interventions/MeetingOutcomeForm.tsx`
- related tests

### Intervention board

1. Replace the cramped fixed four-column layout:
   - desktop ≥1280 px: four columns;
   - tablet/small desktop: two columns;
   - mobile: one column or horizontally scrollable kanban columns with a visible scroll affordance.
2. Give each case card a minimum usable width.
3. Never truncate the student name, priority, state, and due date simultaneously.
4. Use line clamping only for the motif; expose the full motif in the case detail.
5. Keep filters wrapped cleanly and add one visible reset action when filters are active.

### Case detail

1. Ensure the date/time input follows the user's locale or provide a visible French format hint.
2. Explain why `Préparer un brouillon`, `Ajouter`, or `Envoyer` is disabled.
3. Use the established orange accent for primary actions; reserve black/neutral buttons for secondary actions.
4. Keep outreach preparation as the primary task and visually separate advanced case management.
5. Make tasks, notes, communications, and history collapsible on mobile, with counts in their headers.
6. Confirm email recipient, subject, meeting time, and location before sending.
7. Preserve entered draft data when a request fails.
8. Use consistent French workflow labels throughout the board and detail view.

### Tests

- Case cards remain readable at 768 and 1024 px.
- Disabled outreach action has visible explanatory text.
- Failed draft/send preserves user input.
- Confirmation dialog contains all outbound communication details.

### Acceptance criteria

- No case card content is clipped at any target viewport.
- Users always understand what information is missing before an action becomes available.

---

## Phase 9 — Dashboard, Responsable, and Enseignant workspaces

### Primary files

- `frontend/src/pages/Dashboard.tsx`
- `frontend/src/pages/Responsable.tsx`
- `frontend/src/pages/Enseignant.tsx`
- shared KPI/chart components

### Dashboard

1. Retain the current executive hierarchy.
2. Add help text or tooltips for deltas, periods, AUC, F1, precision, and recall.
3. Require confirmation before retraining and explain likely duration/impact.
4. Display success/failure feedback without replacing existing data.
5. At tablet width, use two KPI columns and stack charts predictably.

### Responsable

1. Keep the intervention funnel as the primary differentiator from the general dashboard.
2. Translate `Sent / (Sent + Failed)` and all other technical English copy.
3. Add short definitions for delivery rate, meeting rate, and median delay.
4. Avoid duplicating full dashboard content; link to deeper analytics where appropriate.
5. Ensure color-coded alert and risk states retain textual labels.

### Enseignant

1. Keep the focused module/student layout.
2. Replace unlabeled external/action icons with labeled actions.
3. Improve the initial empty panel with a direct instruction such as `Sélectionnez un étudiant dans la liste`.
4. Consider selecting the first student only if doing so cannot accidentally imply an edit target; otherwise retain the explicit empty state.
5. Keep note and absence editing clearly separated with visible current values.
6. Validate numeric grade ranges inline and show the calculated final grade before saving when applicable.
7. Preserve unsaved inputs if an API request fails.

### Tests

- Retraining confirmation and result feedback.
- Responsive KPI grids at all target widths.
- Teacher note values reject invalid ranges.
- Failed save retains entered note/absence data.

### Acceptance criteria

- Role-specific home pages remain focused on each role's decisions and tasks.
- No unexplained technical metric or English operational copy remains.

---

## Phase 10 — Predictions, reports, administration, and settings

### Predictions

Primary file: `frontend/src/pages/Predictions.tsx`

1. Remove one of the two competing batch-prediction launch actions.
2. Keep one clear cohort selector and one primary launch button.
3. Add definitions/tooltips for risk thresholds and model metrics.
4. Add confirmation before retraining, including data source and expected impact.
5. Clearly label synthetic/demo model metrics when returned by the API.
6. Make prediction-history row actions accessible.
7. Format run timestamps using localized, human-readable dates.

### Reports

Primary file: `frontend/src/pages/Reports.tsx`

1. Preserve the template-first layout.
2. Keep the selected template, parameters, and generation action visible together at tablet/mobile widths.
3. Add a generation progress state and prevent duplicate submissions.
4. Announce completion and expose a clear download action.
5. Give recent-report rows accessible labels and robust placeholders for missing metadata.
6. Avoid displaying `NaN`, blank titles, or raw enum values when data is incomplete.

### Administration

Primary file: `frontend/src/pages/Admin.tsx`

1. Show user name, email, role, active status, and available actions consistently.
2. Add accessible row-action labels.
3. Confirm data warehouse synchronization and alert generation.
4. Explain that DW synchronization may take time and prevent concurrent submissions.
5. Present success details and actionable errors.
6. On narrow screens, convert tabs and wide tables into usable scrollable or stacked layouts.

### Settings

Primary file: `frontend/src/pages/Settings.tsx`

1. Implement theme, accent, and density choices as semantic radio groups or segmented controls with `aria-checked`.
2. Give the sidebar switch an accessible name and description.
3. Confirm that every preference persists using the existing theme context behavior.
4. Keep logout visually separated from appearance settings and require confirmation only if unsaved local form state exists elsewhere.
5. Ensure dark-theme contrast remains acceptable for muted text, borders, warning pills, and selected controls.

### Acceptance criteria

- Each operation has one unambiguous primary action.
- Long-running and destructive operations provide confirmation, progress, and completion feedback.
- Settings controls expose their selected state to assistive technology.

---

## Phase 11 — Copy, localization, and consistency sweep

### Files

- All `frontend/src/pages/*`
- shared layout and UI components

### Implementation

1. Replace the `.dz` support addresses in `Login.tsx` with the approved ENIAD address/domain. Confirm the exact address with the product owner if it is not defined in configuration.
2. Centralize repeated user-facing mappings for:
   - roles;
   - alert types;
   - risk levels;
   - intervention priorities and states;
   - report statuses;
   - model provenance labels.
3. Standardize capitalization, punctuation, date formatting, and terminology:
   - `Filière`, not mixed variants;
   - `Étudiant`, `Enseignant`, `Responsable` consistently;
   - French date/time formatting;
   - `Risque modéré`, not raw `modere`;
   - one term for resolution across alerts.
4. Review empty, loading, error, and success states for every page.
5. Ensure no technical error text or backend enum appears directly in the UI.

### Acceptance criteria

- User-facing copy is consistently French and domain-appropriate.
- Missing or malformed optional data produces a safe placeholder, never `NaN`, `undefined`, or a blank critical field.

---

## Phase 12 — Final verification

### Automated verification

From `frontend/`:

```powershell
npm run lint
npm run test
npm run build
npm run e2e
```

Run the full-stack checks required by the repository if any backend contract was changed.

### Manual route matrix

Verify each route in light and dark mode where applicable:

- `/login`
- `/dashboard`
- `/students`
- `/students/:id`
- `/alerts` — Triage and Journal
- `/cases`
- `/cases/:id`
- `/predictions`
- `/reports`
- `/admin` — all tabs
- `/settings`
- `/enseignant`
- `/responsable`

Verify each role:

- Admin
- Responsable
- Enseignant

Verify each viewport:

- 390 × 844
- 768 × 1024
- 1024 × 768
- 1280 × 720

### Workflow matrix

- Login once and arrive at the correct role home.
- Open and close mobile navigation.
- Open and close Copilot; repeat with Copilot unavailable.
- Search/filter students, open a profile, and return with context preserved.
- Use Alerts Triage and Journal.
- Open an intervention case and prepare an outreach draft up to the confirmation step.
- Enter a teacher note and absence with validation; do not transmit real data during UI-only verification.
- Launch prediction batch using test data.
- Generate and download a test report.
- Open all Administration tabs and verify confirmations without running destructive production operations.
- Change theme, accent, density, and sidebar preference.

### Final acceptance criteria

- No route requires a second login submission.
- Copilot cannot block the platform.
- No supported route displays the breadcrumb `Page`.
- No unnamed interactive controls remain in the checked pages.
- No page-level horizontal overflow occurs at 390 px.
- Intervention cards remain readable at 768 px and 1024 px.
- All user-facing copy is localized and safe for missing data.
- Lint, unit tests, build, and E2E checks pass, or pre-existing failures are explicitly documented with evidence.

## Suggested commit sequence

1. `fix(ui): keep copilot closed and non-blocking`
2. `fix(auth): remove protected-route login race`
3. `fix(nav): add complete route metadata and breadcrumbs`
4. `fix(responsive): harden shell and topbar breakpoints`
5. `fix(a11y): label shared controls and dialogs`
6. `fix(students): improve responsive list and risk explanation`
7. `fix(alerts): align triage terminology and bulk actions`
8. `fix(cases): make intervention workflows responsive and clear`
9. `fix(role-ui): refine dashboard and role workspaces`
10. `fix(ui): refine predictions reports admin and settings`
11. `fix(copy): normalize French labels and safe fallbacks`
12. `test(ui): complete responsive and role-based coverage`

## Required final handoff from the implementing agent

The implementing agent must report:

- files changed, grouped by phase;
- behavioral decisions or deviations from this plan;
- exact verification commands and their results;
- screenshots at 390 px and 1280 px for the major pages;
- any remaining accessibility or backend-dependent limitations;
- confirmation that pre-existing unrelated worktree changes were preserved.
