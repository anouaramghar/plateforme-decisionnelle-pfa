# Plateforme Décisionnelle ENIAD 2025/2026

BI & predictive-analytics platform for student performance monitoring at ENIAD Berkane.

**Team:** AIT ALI Marouane · AMGHAR Anouar · ENGAR Aymen  
**Supervisor:** Prof. Redouane LHIADI

---

## Stack

| Layer | Technology |
|---|---|
| Frontend | React 19 · TypeScript · Vite · TanStack Query v5 · ApexCharts · Tailwind |
| Backend | ASP.NET Core 8 · EF Core · SQL Server · JWT Bearer |
| ML Service | FastAPI 0.111 · scikit-learn · XGBoost · joblib |
| Agent Service | FastAPI · NVIDIA NIM (OpenAI-compatible) · httpx · pydantic 2 |
| Infrastructure | Docker Compose · Nginx · SQL Server 2022 |

## Services

```
Internet → nginx:80 (pfa_public)
               ├─ /api/copilotkit → copilot-runtime:4000
               ├─ /api/*          → backend:8080
               └─ /*              → frontend:8080

copilot-runtime:4000 → backend:8080   (tool callbacks via /api/copilot/tool/*)
backend:8080         → ml-service:8000 (pfa_internal_ml)
backend:8080         → db:1433         (pfa_internal_db)
```

Three isolated Docker networks: `pfa_public`, `pfa_internal_db`, `pfa_internal_ml`. ML service and database are unreachable from the public internet (`internal: true`).

---

## Quick Start

```bash
cp .env.example .env   # fill in JWT_SECRET, SA_PASSWORD, APP_DB_PASSWORD, ML_INTERNAL_TOKEN, NVIDIA_NIM_API_KEY
docker compose up --build
```

First admin account: `admin@eniad.dz` / `Admin@ENIAD2025` (seeded by `DataSeeder`).

```bash
docker compose down -v  # wipe DB volumes — needed after schema changes
```

## Development

### Backend (ASP.NET Core 8)
```bash
cd backend/PlateformePFA.API
dotnet restore && dotnet build
dotnet run                    # http://localhost:5135  (Swagger at /swagger)
dotnet ef migrations add <Name>
dotnet ef database update
```

Tests:
```bash
cd backend/PlateformePFA.Tests && dotnet test --nologo
```

### ML Service (FastAPI)
```bash
cd ml_service
python -m venv venv && venv/Scripts/activate
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

### CopilotKit Runtime (Node + NVIDIA NIM)
```bash
cd copilot-runtime
npm install
# Requires AGENT_INTERNAL_TOKEN + NVIDIA_NIM_API_KEY + JWT_SECRET in env
npm run dev     # http://localhost:4000/api/copilotkit
```

### Frontend (Vite + React)
```bash
cd frontend
npm ci
npm run dev     # http://localhost:5173
npm run build
```

---

## Features

### BI Dashboard
- KPIs: student count, global average, pass rate, active alerts — with semester-over-semester deltas
- Charts: grades by filière (bar), grade distribution (histogram), absence trend (area), risk distribution
- At-risk student table with ML scores
- Real-time activity feed backed by audit log

### Students
- Paginated table with search + filters (filière, niveau, risk level)
- Student drawer: notes by module, ML risk analysis with factor breakdown

### Alerts
- Automatic alerts: low grade (< 10), excessive absences (> 20h), high ML risk score
- Polling every 30s, bulk resolve, XLSX export

### Predictions ML
- Batch prediction for any filière/niveau cohort
- Risk scatter plot (average vs absences)
- Model retrain from the UI
- Prediction run history

### Reports
- 6 report templates (performance, risk, notes, absences, alerts, ML summary)
- PDF / XLSX / CSV export, auth-aware download

### ENIAD Copilot (in development)
- AI assistant backed by NVIDIA NIM (LLaMA 3.3 70B)
- Tool-calling agent loop with role filtering
- Current tools: `get_student`, `list_at_risk`
- SSE streaming via `POST /api/copilot/chat`

---

## Architecture

### Database
Two databases on the same SQL Server instance:

| Database | Purpose |
|---|---|
| `PFA_DB` | OLTP — daily operations (normalized) |
| `PFA_DW` | Data Warehouse — BI queries (star schema: FaitNotes + DimEtudiant + DimModule + DimTemps) |

ETL: `POST /api/admin/sync-dw` executes `database/etl.sql` MERGE statements.

### Security
- JWT Bearer (24h) + DB-backed refresh tokens (7 days, single-use rotation)
- Roles: Admin, Responsable, Enseignant
- ML/agent services gated by `X-Internal-Token` shared secret
- Agent tool callbacks require both user JWT + internal token (dual gate)
- `pfa_app_readonly` SQL login for Copilot read-only DW queries

### Environment Variables
See `.env.example` for the full annotated list. Required at minimum:
- `JWT_SECRET` (≥ 32 chars)
- `SA_PASSWORD`, `APP_DB_PASSWORD`
- `ML_INTERNAL_TOKEN`, `AGENT_INTERNAL_TOKEN`
- `NVIDIA_NIM_API_KEY` (for Copilot)

### Operations

- **Copilot sessions:** the chat stream is gated by a short-lived (15 min) `pfa_copilot_session` HTTP-only cookie (`SameSite=Strict`, path `/api/copilotkit`). The frontend mints it via `POST /api/copilot/session` after login and clears it via `DELETE /api/copilot/session` on logout. The runtime rejects any inference request without a verified cookie (401); only `GET /health` is anonymous.
- **Liveness vs readiness:** `/health` is liveness only. `/ready` reports `Database.CanConnectAsync()` **plus** the `dbo.SchemaState` core marker — the backend Docker healthcheck and nginx startup gate target `/ready`, so traffic is only admitted after schema provisioning completes.
- **DB initialization:** `database/entrypoint.sh` runs `init.sql`, `init_dw.sql`, and `copilot_readonly.sql` (all idempotent, `-b`) on **every** boot — existing or partial volumes are re-applied, not skipped. The `dbo.SchemaState` row is written only after every script succeeds; the Compose DB healthcheck requires it.
- **Model provenance:** `/api/predictions/metrics` carries `data_source` (`dw` | `synthetic`), `split_strategy`, `trained_at`, and evaluated periods. When `data_source === 'synthetic'`, the Predictions page shows a non-dismissible demo-data warning — synthetic metrics are not production validation.
- **Retraining prerequisites:** honest training needs real `PFA_DW` data (run the ETL first). Risk labels are the *next-period* observed outcome and forecasts use *prior-period* features only; evaluation holds out the latest period (or `GroupShuffleSplit` by student when only one period exists). Without DW data the service falls back to clearly-labelled synthetic training.
- **ETL conflict response:** `POST /api/admin/sync-dw` takes an exclusive app-lock and runs as one transaction. A concurrent call returns **HTTP 409** rather than producing duplicate `FaitNotes` grain rows.
- **Container architecture:** the ML image derives the Microsoft ODBC repo arch from `dpkg --print-architecture`, so it builds on both `amd64` and `arm64` hosts. If a target lacks an `msodbcsql18` package, pin `platform: linux/amd64` for `ml-service` in Compose.

---

## Project Status (2026-05-30)

| Component | Status |
|---|---|
| Infrastructure | ✅ Complete |
| Database OLTP + DW | ✅ Complete |
| Backend API | ✅ Complete |
| ML Service | ✅ Complete |
| Frontend — all 5 pages | ✅ Complete |
| Copilot (CopilotKit runtime) | 🔄 5 tools: `get_student`, `list_at_risk`, `query_dw`, `draft_alert`, `explain_risk` |
| Copilot frontend panel | ❌ Not started |
| End-to-end tests | ⏳ Pending |
