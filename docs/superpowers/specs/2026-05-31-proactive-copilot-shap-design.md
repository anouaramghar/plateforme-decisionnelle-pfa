# Design Spec — Proactive Copilot + ML Explainability (SHAP)

**Date:** 2026-05-31  
**Author:** AMGHAR Anouar  
**Status:** Approved — ready for implementation planning

---

## Goal

Two compounding improvements to the ENIAD Copilot:

1. **Proactive Copilot** — the Copilot detects anomalies overnight and initiates the conversation when the user opens the panel, instead of waiting to be asked.
2. **ML Explainability (SHAP)** — risk scores are decomposed into per-feature SHAP contributions, visualised as an inline horizontal bar chart inside the chat bubble.

Together they answer the two biggest gaps in the current platform: the Copilot is reactive-only, and risk scores are opaque black boxes.

---

## Architecture

No new Docker services. Everything fits inside the existing `agent-service` and `backend` containers.

```
┌─────────────────────────────────────────────────────────┐
│  agent-service (FastAPI)                                │
│                                                         │
│  ┌─────────────────┐    ┌──────────────────────────┐   │
│  │  APScheduler    │───▶│  HeadlessAgent           │   │
│  │  (nightly 02:00 │    │  runs agent loop without │   │
│  │  + on-demand)   │    │  a user message          │   │
│  └─────────────────┘    └──────────┬───────────────┘   │
│                                    │ writes findings    │
│                                    ▼                    │
│                         ┌──────────────────────────┐   │
│                         │  ProactiveMessages table  │   │
│                         │  (PFA_DB)                 │   │
│                         └──────────┬───────────────┘   │
└────────────────────────────────────┼───────────────────┘
                                     │ GET /api/copilot/inbox
                              ┌──────▼────────┐
                              │   Backend     │
                              │  (new routes) │
                              └──────┬────────┘
                                     │
                              ┌──────▼────────┐
                              │   Frontend    │
                              │  badge + chat │
                              └───────────────┘
```

```
┌─────────────────────────────────────────────────────────┐
│  ML Explainability                                      │
│                                                         │
│  predict() → shap.TreeExplainer → values stored in DB  │
│  explain_risk tool → reads SHAP from DB                 │
│  Frontend → detects chart payload → ApexCharts inline   │
└─────────────────────────────────────────────────────────┘
```

---

## Feature 1 — Proactive Copilot

### Anomaly Triggers

The scheduler queries OLTP/DW for the following signals each run:

| Signal | Condition | Priority |
|---|---|---|
| Risk threshold crossed | `ScoreRisque` went from `< 0.65` to `≥ 0.65` since last digest | High |
| Absence spike | Student gained `> 8h` absences in the last 7 days | Medium |
| Grade drop | `NoteFinal` dropped `> 2 pts` vs previous semester | Medium |
| High-risk cohort | First time a filière has `> 30%` students at risk | High |

### Delivery Flow

```
02:00 UTC nightly
  → APScheduler fires HeadlessAgent
  → queries OLTP/DW for anomalies
  → for each finding: agent loop generates French prose message
  → rows written to ProactiveMessages (status = 'pending')

User opens app:
  → Copilot button shows red badge (unread count)
  → User opens panel
  → Pending messages load at top of chat, visually distinct
    (different background, "ENIAD Copilot · il y a 3h" timestamp)
  → User can reply and continue the conversation normally
  → Messages marked 'read' on open
```

### Database

New table in `PFA_DB`, added via `database/init.sql` (idempotent):

```sql
IF OBJECT_ID('dbo.ProactiveMessages', 'U') IS NULL
CREATE TABLE ProactiveMessages (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    Role         NVARCHAR(20)   NOT NULL,       -- 'Admin' | 'Responsable'
    TriggerType  NVARCHAR(40)   NOT NULL,       -- 'RiskCrossed' | 'AbsenceSpike' | 'GradeDrop' | 'HighRiskCohort'
    EntityType   NVARCHAR(20)   NULL,           -- 'Etudiant' | 'Filiere'
    EntityId     INT            NULL,
    Message      NVARCHAR(2000) NOT NULL,
    Status       NVARCHAR(10)   NOT NULL DEFAULT 'pending',  -- 'pending' | 'read'
    CreeLe       DATETIME2      NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_ProactiveMessages_Status ON ProactiveMessages(Status, Role, CreeLe DESC);
```

### New agent-service components

- `scheduler.py` — APScheduler setup, `run_digest()` job registered at startup
- `headless_agent.py` — runs the existing agent loop with a system-generated prompt instead of a user message; reuses `AgentRunner` from `agent.py`
- `anomaly_detector.py` — pure query logic; returns typed `Finding` objects; no LLM involved

### New API endpoints (backend)

```
GET  /api/copilot/inbox              → list ProactiveMessages for caller's role, status='pending'
POST /api/copilot/inbox/{id}/read    → mark message read (sets status='read')
POST /api/copilot/digest/run         → Admin-only, triggers HeadlessAgent immediately (demo button)
```

`/digest/run` hits a new route on `agent-service`: `POST /agent/digest/run`, authenticated with `X-Internal-Token`.

---

## Feature 2 — ML Explainability (SHAP + Inline Chart)

### SHAP Integration

SHAP `TreeExplainer` is instantiated **once at startup** alongside the XGBoost model (in `lifespan`). Per-prediction SHAP values add ~1ms.

```python
# ml_service — during predict()
explainer = app.state.shap_explainer       # loaded at startup
shap_vals = explainer.shap_values(X_row)   # shape: [n_features]
# features order: [moyenne_generale, taux_absence, nb_modules]
```

### Storage

Three new nullable float columns on the `Predictions` table:

```sql
ALTER TABLE Predictions ADD ShapMoyenne FLOAT NULL;
ALTER TABLE Predictions ADD ShapAbsence FLOAT NULL;
ALTER TABLE Predictions ADD ShapModules FLOAT NULL;
```

Added via `RuntimeMigrations.cs` (idempotent `IF NOT EXISTS` pattern already in use).

### Enhanced `explain_risk` Tool Response

The tool callback (`CopilotToolController`) reads SHAP columns from the latest `Prediction` for the student and returns a structured payload:

```json
{
  "text": "Le risque d'AMGHAR Mehdi (0.82) est principalement dû à un taux d'absence élevé (+0.31). Sa moyenne générale exerce un effet protecteur (-0.12).",
  "chart": {
    "type": "shap_bar",
    "student": "AMGHAR Mehdi",
    "score": 0.82,
    "factors": [
      { "label": "Taux d'absence",      "value":  0.31, "direction": "up" },
      { "label": "Modules non validés", "value":  0.18, "direction": "up" },
      { "label": "Moyenne générale",    "value": -0.12, "direction": "down" }
    ]
  }
}
```

Fallback: if SHAP columns are NULL (old predictions, or SHAP failed), the existing heuristic 4-factor breakdown is returned without a `chart` key.

### Frontend Rendering

`ToolChip` (resolved state) detects `chart.type === "shap_bar"` in the tool result and renders an inline ApexCharts horizontal bar:

```
┌─────────────────────────────────────┐
│ explain_risk ✓  AMGHAR Mehdi        │
│                                     │
│ Taux d'absence    ████████░░  +0.31 │  ← var(--bad)
│ Modules non val.  █████░░░░░  +0.18 │  ← var(--warn)
│ Moyenne générale  ██░░░░░░░░  -0.12 │  ← var(--ok)
│                                     │
│ Score de risque global: 0.82        │
└─────────────────────────────────────┘
```

Bars are coloured using the existing design system tokens: `var(--bad)` for positive contributions (risk-increasing), `var(--ok)` for negative (protective).

---

## Error Handling

### Scheduler

| Failure | Behavior |
|---|---|
| DW unreachable at 02:00 | Retry once after 10 min; log warning; no crash |
| No anomalies found | Silent — no message written, no badge |
| NIM (LLM) unreachable | Write fallback template message without LLM prose |
| `agent-service` crash | Docker restarts container; APScheduler resumes on next tick |

### SHAP

| Failure | Behavior |
|---|---|
| SHAP computation fails | Score saved, SHAP columns left NULL; no impact on prediction |
| SHAP columns NULL | `explain_risk` falls back to heuristic breakdown; no chart emitted |
| SHAP explainer load fails at startup | Warning logged; prediction endpoint still works |

---

## Testing Plan

| Layer | What |
|---|---|
| `agent-service` pytest | `anomaly_detector` with mocked DB rows; `HeadlessAgent` with mocked tool executor; scheduler trigger fires `run_digest` |
| `backend` xUnit | `GET /inbox` returns correct rows filtered by role; `POST /inbox/{id}/read` flips status; `POST /digest/run` returns 403 for non-Admin |
| `ml_service` pytest | SHAP values present in predict response; fallback when explainer unavailable |
| Frontend (manual) | Badge shows unread count; proactive bubble visually distinct; SHAP chart renders when payload present; chart absent when SHAP NULL |

---

## Demo Flow (Soutenance)

1. Sign in as Admin → Copilot button shows **🔴 2**
2. Open Copilot panel → two proactive messages waiting ("Mehdi a franchi le seuil de risque élevé cette nuit…")
3. Reply to one → conversation continues normally
4. Ask `explique le risque de E10042` → tool chip resolves → SHAP bar chart renders inline
5. Click **Lancer le diagnostic** button → `POST /digest/run` → new finding appears within seconds

---

## Out of Scope

- Email / push notifications (separate feature, ~3 days)
- Proactive messages for Enseignant role (can be added later by extending `Role` filter)
- Historical SHAP trends (requires storing SHAP per prediction over time — already enabled by the schema, but visualisation is future work)
- Real-time SSE push when panel is closed (Approach 2 — deferred)
