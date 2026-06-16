# CopilotKit Full Implementation Design

**Date:** 2026-06-16  
**Branch:** copilotkit  
**Status:** Approved — ready for implementation planning

---

## Goal

Fully implement CopilotKit alongside the existing custom copilot (NVIDIA NIM agent-service) so both can be tested side by side before a final decision is made on which to keep.

---

## Architecture

```
Browser
  ├─ <CopilotKit runtimeUrl="http://localhost:4000/api/copilotkit" headers={JWT}>
  │     ├─ <CopilotSidebar>          ← CopilotKit test UI (Ctrl+Shift+J)
  │     └─ useCopilotReadable/Action ← hooks in Dashboard, Students, Predictions
  │
  └─ <CopilotPanel>                  ← UNCHANGED: custom panel (Ctrl+J)

copilot-runtime (Node.js :4000)      ← extended with 5 server-side tools
  ├─ get_student, list_at_risk, query_dw, explain_risk (server-side Actions)
  └─ BuiltInAgent (Claude Sonnet 4.6)

ASP.NET backend (:8080)              ← unchanged, existing tool endpoints reused
agent-service (Python :8001)         ← unchanged, custom panel still talks to it
```

**Auth flow:** JWT travels from `<CopilotKit headers={{ Authorization: 'Bearer ...' }}>` → copilot-runtime → forwarded to every backend API call. The runtime acts as an authenticated proxy. No new credentials or backend endpoints needed.

---

## Section 1: Server-side Tools in `copilot-runtime`

4 tools defined as `Action` objects in `copilot-runtime/src/index.ts`:

| Tool | Endpoint | Method |
|---|---|---|
| `get_student` | `/api/copilot/tools/student/{matricule}` | GET |
| `list_at_risk` | `/api/copilot/tools/at-risk?threshold&filiere&niveau` | GET |
| `query_dw` | `/api/copilot/tools/query-dw` | POST |
| `explain_risk` | `/api/copilot/tools/explain-risk` | POST |

`draft_alert` is intentionally excluded — it requires `renderAndWaitForResponse` which only works as a client-side `useCopilotAction` hook.

**Error handling:** All tools return a structured error string on backend failure (401, 500, etc.) so the LLM can report it gracefully.

**Backend URL:** Read from `BACKEND_URL` env var (default `http://localhost:5135`).

---

## Section 2: Client-side Hooks (per page)

### Dashboard page
- `useCopilotReadable("dashboard-stats")` — total students, at-risk count, average score, alert count
- `useCopilotAction: navigate_to_student(matricule)` — navigates to the student detail page

### Students page
- `useCopilotReadable("students-list")` — current visible student list (matricule, name, filière, niveau, risk score)
- `useCopilotAction: filter_students({ filiere?, niveau?, minRisk? })` — sets page filters programmatically
- `useCopilotAction: open_student(matricule)` — navigates to student detail view

### Predictions page
- `useCopilotReadable("predictions-list")` — visible predictions (matricule, score, trend)
- `useCopilotAction: highlight_student(matricule)` — scrolls to and highlights a specific row

### `draft_alert` (Students + Predictions pages)
- `useCopilotAction: draft_alert` with `renderAndWaitForResponse: true`
- Renders the existing `ConfirmCard` component inside the CopilotKit chat
- Tool pauses until user clicks Confirm or Cancel, returns result to LLM
- On confirm: `POST /api/alertes` directly from the browser (same as manual alert flow)
- Parameters: `matricule`, `severity` (high/medium/low), `message`

---

## Section 3: UI — CopilotSidebar alongside CopilotPanel

| | Custom Panel | CopilotKit Sidebar |
|---|---|---|
| Shortcut | `Ctrl+J` | `Ctrl+Shift+J` |
| Component | `<CopilotPanel>` (unchanged) | `<CopilotSidebar>` from `@copilotkit/react-ui` |
| Header label | "ENIAD Copilot" | "ENIAD Copilot · CopilotKit" |
| Backend | agent-service (NVIDIA NIM) | copilot-runtime (Claude Sonnet 4.6) |
| Entry point | Existing topbar button | New topbar button with "CK" badge |

`@copilotkit/react-ui` styles imported once in `main.tsx`. No routing changes, no new pages.

---

## What is NOT changing

- `CopilotPanel.tsx` — untouched
- `agent-service/` (Python) — untouched
- ASP.NET backend controllers — untouched
- Existing `/api/copilot/tools/*` endpoints — reused as-is
- All existing tests (83 pytest + 31 dotnet) — unchanged

---

## Files to create / modify

| File | Action |
|---|---|
| `frontend/package.json` | Add `@copilotkit/react-ui` dependency (needed for `CopilotSidebar`) |
| `copilot-runtime/src/index.ts` | Rewrite — add 4 server-side Actions, auth forwarding |
| `copilot-runtime/src/tools.ts` | Create — tool definitions extracted to own file |
| `frontend/src/main.tsx` | Modify — pass JWT to `<CopilotKit headers={...}>`, import CK styles |
| `frontend/src/layout/AppShell.tsx` | Modify — add `<CopilotSidebar>` + second topbar button |
| `frontend/src/layout/Topbar.tsx` | Modify — add CK toggle button |
| `frontend/src/pages/Dashboard.tsx` | Modify — add `useCopilotReadable` + `navigate_to_student` action |
| `frontend/src/pages/Students.tsx` | Modify — add readable + `filter_students`, `open_student`, `draft_alert` actions |
| `frontend/src/pages/Predictions.tsx` | Modify — add readable + `highlight_student`, `draft_alert` actions |
| `.env.example` | Modify — add `BACKEND_URL`, `VITE_COPILOTKIT_URL` vars |

---

## Testing Plan

1. Start `copilot-runtime` (`npm run dev` in `copilot-runtime/`)
2. Start frontend (`npm run dev` in `frontend/`)
3. Start backend + agent-service via `docker compose up`
4. **Server-side tool tests:**
   - Ask "donne-moi le profil de E10001" → `get_student` fires
   - Ask "liste les étudiants à risque > 0.7" → `list_at_risk` fires
   - Ask "combien d'étudiants dans le DW ?" → `query_dw` fires
   - Ask "explique le risque de E10001" → `explain_risk` fires
5. **Client-side hook tests:**
   - On Dashboard: ask "combien d'étudiants sont à risque ?" → LLM reads live stats
   - On Students: ask "filtre sur la filière GI" → table filters
   - Ask "crée une alerte pour E10002" → `draft_alert` renders confirm card, pause, confirm → alert created
6. **Side-by-side comparison:**
   - Open custom panel (Ctrl+J) and CopilotKit sidebar (Ctrl+Shift+J) simultaneously
   - Send the same query to both, compare quality and UX
