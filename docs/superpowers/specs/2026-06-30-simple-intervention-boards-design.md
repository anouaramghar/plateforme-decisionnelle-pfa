# Simple Intervention Boards Design

## Goal

Make interventions easier to use by splitting the experience by role:

- Enseignant gets a lightweight follow-up board focused on student observations.
- Admin/Responsable gets an operational intervention board focused on the next action.

The current intervention model already has cases, states, notes, communications, meetings, timeline, and escalation. This design simplifies the UI first and reuses the backend unless implementation proves a small API gap.

## Non-Goals

- No Trello-style drag and drop in the first version.
- No new generic workflow engine.
- No new notification system.
- No redesign of the prediction or triage engines.

## Enseignant Experience: Suivi Etudiants

Create or adapt a teacher-facing page named "Suivi etudiants".

The teacher sees three columns:

1. A voir
   Students with risk signals related to the teacher's modules, or open intervention cases where teacher context is useful.

2. En suivi
   Students where the teacher has already added an observation or asked for admin intervention.

3. Traite
   Students where the concern is resolved, closed, or no longer needs teacher action.

Each card shows only the useful teaching context:

- student name
- module when available
- risk reason
- last grade or absence signal
- last teacher/admin action

Allowed teacher actions:

- Ajouter observation
- Demander intervention
- Marquer traite

Hidden from teachers:

- email composer
- meeting scheduling
- escalation controls
- generic communication sending
- private admin notes
- advanced state transitions

If the current backend cannot express "teacher follow-up" separately from intervention cases, the first implementation should use existing case notes and case state as much as possible. Add a minimal API only if the UI cannot reliably save the three teacher actions.

## Admin Experience: Intervention Board

Keep Admin/Responsable in the intervention workflow, but make the board next-action driven.

The admin board uses four columns:

1. Nouveau
   Case is open and unassigned.
   Primary action: Assigner.

2. A contacter
   Case is assigned, but student outreach has not been sent.
   Primary action: Envoyer invitation.

3. Rendez-vous
   Meeting is scheduled or waiting for student result.
   Primary action: Saisir resultat.

4. Resolu
   Case is resolved or closed.
   Primary action: Consulter, with reopen available only where allowed.

Each admin card shows:

- student name
- risk or priority
- owner
- next action
- days open

The detail page keeps one primary next-action panel at the top:

- unassigned case: assign owner
- not contacted: prepare/send invitation
- meeting planned: record result
- resolved: show outcome summary

Secondary information moves below the fold or into collapsed sections:

- notes
- timeline
- emails
- tasks
- advanced transitions

## Data Flow

Teacher flow:

1. Teacher opens Suivi etudiants.
2. Frontend loads the teacher-relevant students or intervention cases.
3. Teacher adds an observation, requests intervention, or marks treated.
4. Backend records the action with the authenticated user and updates the displayed column.

Admin flow:

1. Admin opens Interventions.
2. Frontend loads existing intervention cases.
3. Frontend derives the board column from current case fields and communication/meeting state.
4. Admin takes the single primary next action shown for that case.
5. Backend remains authoritative for permissions, transitions, audit, and validation.

## Column Mapping

For Admin/Responsable:

- Nouveau: `etat = Open` and `ownerId = null`
- A contacter: assigned case without sent outreach
- Rendez-vous: `etat = WaitingStudent` or a scheduled meeting without recorded result
- Resolu: `etat = Resolved` or `etat = Closed`

For Enseignant:

- A voir: risky student or open case with no teacher observation
- En suivi: teacher observation exists, intervention requested, or active case is in progress
- Traite: resolved, closed, or explicitly marked treated by the teacher

If the teacher mapping needs new persistence, add the smallest possible field or endpoint to record teacher follow-up status. Do not create a separate workflow system.

## Error Handling

- If board data fails to load, show the existing compact error state.
- If an action fails, keep the card in place and show a short inline error.
- If permissions reject an action, show a friendly denial and refresh the card.
- If a duplicate active case exists when a teacher requests intervention, navigate to or show the existing case instead of creating another.

## Testing

Add focused tests only around changed behavior:

- teacher board renders the three columns and hides admin-only controls
- teacher actions call the expected API and update/refetch the board
- admin board places cases in the four next-action columns
- admin detail shows exactly one primary next action for representative states

Use existing frontend test patterns. Backend tests are only needed if a new endpoint or field is added.

## Acceptance Criteria

- Teachers can understand what to do without seeing email, meeting, or escalation controls.
- Admins see a simple operational board with one obvious next action per card.
- Existing intervention audit/security rules remain intact.
- First implementation has no drag and drop and no new workflow abstraction.
