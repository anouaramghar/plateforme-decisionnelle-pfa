# Plateforme Décisionnelle ENIAD 2025/2026

BI & predictive-analytics platform for student performance monitoring at ENIAD Berkane.

**Team:** AIT ALI Marouane · AMGHAR Anouar · ENGAR Aymen  
**Supervisor:** Prof. Redouane LHIADI

---

## Stack

| Layer | Technology |
|---|---|
| Frontend | React 18 · TypeScript · Vite · TanStack Query v5 · ApexCharts · Tailwind |
| Backend | ASP.NET Core 8 · EF Core · SQL Server · JWT Bearer |
| ML Service | FastAPI 0.111 · scikit-learn · XGBoost · joblib |
| Agent Service | Node.js · Express · CopilotKit Runtime · NVIDIA NIM (OpenAI-compatible) |
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
ml-service:8000      → db:1433 (PFA_DW read-only via pfa_app_readonly, pfa_internal_db)
db:1433              → host:1433       (pfa_db_host — published 127.0.0.1 port for SSMS)
```

Four isolated Docker networks: `pfa_public`, `pfa_internal_db`, `pfa_internal_ml`, and `pfa_db_host`. The ML service and database are unreachable from the public internet (`internal: true`); `pfa_db_host` is a non-internal bridge whose sole member is `db`, so its `127.0.0.1:1433` published port is reachable from the host for SSMS without exposing the DB to frontend/nginx.

---

## Quick Start

```bash
cp .env.example .env   # fill in JWT_SECRET, SA_PASSWORD, APP_DB_PASSWORD,
                       # READONLY_DB_PASSWORD, ML_INTERNAL_TOKEN,
                       # AGENT_INTERNAL_TOKEN, NVIDIA_NIM_API_KEY
docker compose up --build
```

First admin account is created at backend startup by `DataSeeder`, driven by the `ADMIN_SEED_EMAIL` / `ADMIN_SEED_PASSWORD` env vars (see `.env.example` — set them before first boot; the seeder refuses a placeholder/short password). There is **no** `/api/auth/register` endpoint. Example credentials are placeholders only — do not use them in production.

```bash
docker compose down -v  # wipe DB volumes — needed after schema changes
```

## Development

### Backend (ASP.NET Core 8)
```bash
cd backend/PlateformePFA.API
dotnet restore && dotnet build
dotnet run                    # http://localhost:5135  (Swagger at /swagger)
```

Schema evolution is handled by `RuntimeMigrations.cs` (idempotent `CREATE TABLE` / `ALTER TABLE` blocks run on every startup), **not** EF Core migrations — there is no `Migrations/` folder, so `dotnet ef migrations add` / `dotnet ef database update` are not used.

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

### ENIAD Copilot
- AI assistant backed by NVIDIA NIM (LLaMA 3.3 70B)
- CopilotKit runtime with authenticated streaming at `/api/copilotkit`
- Tool-calling agent loop with role filtering and backend-gated callbacks
- Current tools: `get_student`, `list_at_risk`, `query_dw`, `draft_alert`, `explain_risk`
- Frontend side panel mounted after login with a short-lived Copilot session cookie

---

## Architecture

### Database
Two databases on the same SQL Server instance:

| Database | Purpose |
|---|---|
| `PFA_DB` | OLTP — daily operations (normalized) |
| `PFA_DW` | Data Warehouse — BI queries (star schema: FaitNotes + DimEtudiant + DimModule + DimTemps) |

ETL: `POST /api/admin/sync-dw` executes the API-bundled `backend/PlateformePFA.API/Scripts/etl.sql`. `database/etl.sql` is the manual SQL copy and must match it.

### Security
- JWT Bearer (24h) + DB-backed refresh tokens (7 days, single-use rotation)
- Roles: Admin, Responsable, Enseignant
- ML/agent services gated by `X-Internal-Token` shared secret
- Agent tool callbacks require both user JWT + internal token (dual gate)
- `pfa_app_readonly` SQL login for Copilot read-only DW queries

### Environment Variables
See `.env.example` for the full annotated list. Required at minimum:
- `JWT_SECRET` (≥ 32 chars)
- `SA_PASSWORD`, `APP_DB_PASSWORD`, `READONLY_DB_PASSWORD`
- `ML_INTERNAL_TOKEN`, `AGENT_INTERNAL_TOKEN`
- `ADMIN_SEED_EMAIL`, `ADMIN_SEED_PASSWORD` (first-boot admin)
- `NVIDIA_NIM_API_KEY` (for Copilot)

### Operations

- **Copilot sessions:** the chat stream is gated by a short-lived (15 min) `pfa_copilot_session` HTTP-only cookie (`SameSite=Strict`, path `/api/copilotkit`). The frontend mints it via `POST /api/copilot/session` after login and clears it via `DELETE /api/copilot/session` on logout. The runtime rejects any inference request without a verified cookie (401); only `GET /health` is anonymous.
- **Liveness vs readiness:** `/health` is liveness only. `/ready` reports `Database.CanConnectAsync()` **plus** the `dbo.SchemaState` core marker — the backend Docker healthcheck and nginx startup gate target `/ready`, so traffic is only admitted after schema provisioning completes.
- **DB initialization:** `database/entrypoint.sh` runs `init.sql`, `init_dw.sql`, and `copilot_readonly.sql` (all idempotent, `-b`) on **every** boot — existing or partial volumes are re-applied, not skipped. The `dbo.SchemaState` row is written only after every script succeeds; the Compose DB healthcheck requires it.
- **Model provenance:** `/api/predictions/metrics` carries `data_source` (`dw` | `uci` | `synthetic` | `unknown`), `split_strategy`, `trained_at`, and evaluated periods. The Predictions page warns whenever the active model source is not `dw`.
- **Retraining prerequisites:** honest training needs real `PFA_DW` data (run the ETL first). Risk labels are the *next-period* observed outcome and forecasts use *prior-period* features only; evaluation holds out the latest period (or `GroupShuffleSplit` by student when only one period exists). Without DW data the service falls back to clearly-labelled synthetic training.
- **ETL conflict response:** `POST /api/admin/sync-dw` takes an exclusive app-lock and runs as one transaction. A concurrent call returns **HTTP 409** rather than producing duplicate `FaitNotes` grain rows.
- **Container architecture:** the ML image derives the Microsoft ODBC repo arch from `dpkg --print-architecture`, so it builds on both `amd64` and `arm64` hosts. If a target lacks an `msodbcsql18` package, pin `platform: linux/amd64` for `ml-service` in Compose.

---

## Project Status (2026-06-29)

| Component | Status |
|---|---|
| Infrastructure | ✅ Complete |
| Database OLTP + DW | ✅ Complete |
| Backend API | ✅ Complete |
| ML Service | ✅ Complete |
| Frontend application | ✅ Complete |
| Copilot runtime + tools | ✅ Integrated: `get_student`, `list_at_risk`, `query_dw`, `draft_alert`, `explain_risk` |
| Copilot frontend panel | ✅ Integrated |
| Automated tests | ✅ Backend, ML, and frontend unit suites passing |
| Playwright end-to-end tests | ⏳ Available, pending full flow coverage |
