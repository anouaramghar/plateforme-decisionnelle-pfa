# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project

Plateforme décisionnelle ENIAD 2025/2026 — a BI & predictive-analytics platform for student performance. Built by a trio: AIT ALI / AMGHAR (frontend + alerts + exports) / ENGAR, supervised by Prof. LHIADI.

## Service Map

```
Internet → nginx:80 (pfa_public)  [+ 443 when ENABLE_TLS=true]
                ├─ /api/copilotkit → copilot-runtime:4000 (pfa_public)
                ├─ /api/*          → backend:8080          (pfa_public + pfa_internal_db + pfa_internal_ml)
                └─ /*              → frontend:8080         (pfa_public)

copilot-runtime:4000 → backend:8080   (tool callbacks via /api/copilot/tool/*)
backend:8080         → ml-service:8000 (pfa_internal_ml)
backend:8080         → db:1433         (pfa_internal_db)
ml-service:8000      → db:1433 (PFA_DW read-only via pfa_app_readonly)
ml-service:8000      → mlflow:5000     (pfa_internal_ml — model lineage)
db:1433              → host:1433       (pfa_db_host — published 127.0.0.1 for SSMS)
mlflow:5000          → host:5001       (pfa_mlflow_host — published 127.0.0.1 for the demo UI)
```

Five isolated Docker networks: `pfa_public`, `pfa_internal_db`, `pfa_internal_ml`, `pfa_db_host`, `pfa_mlflow_host`. The ML service, MLflow, and database are unreachable from the public internet by design (`internal: true` on the two internal planes); `pfa_db_host` and `pfa_mlflow_host` are non-internal bridges whose sole members are `db` / `mlflow`, so their `127.0.0.1`-published host ports work without exposing them to other services. README.md is the canonical service map — keep this in sync when you change compose.

## Commands

### Full stack
```bash
cp .env.example .env   # fill in real secrets first (see .env.example comments)
docker compose up --build
docker compose down -v  # also wipe DB volumes — needed after schema changes
```

### Backend (ASP.NET Core 8)
```bash
cd backend/PlateformePFA.API
dotnet restore && dotnet build
dotnet run                          # http://localhost:5135  (Swagger at /swagger)
# Schema evolution: there is NO Migrations/ folder and `dotnet ef migrations`
# is NOT used. Additive schema changes are applied at startup by
# Data/RuntimeMigrations.cs (idempotent ExecuteSqlRaw CREATE TABLE / ALTER TABLE
# / CREATE INDEX blocks). To add a column/table, append a guarded block there.
dotnet test                         # from solution root when tests exist
```

### ML service (FastAPI)
```bash
cd ml_service
python -m venv venv && source venv/bin/activate  # Windows: venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --reload --port 8000   # http://localhost:8000/docs
```

### Frontend (Vite + React)
```bash
cd frontend
npm ci
npm run dev     # http://localhost:5173
npm run build   # output → dist/
```

### Database (inside container)
```bash
docker exec -it <db-container> bash
SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -d PFA_DB
```

## Backend Architecture

**Stack:** ASP.NET Core 8 · EF Core (SQL Server) · BCrypt.Net-Next · JwtBearer · Swashbuckle

**Startup order in `Program.cs`:**
1. Fail-fast validation: `JWT_SECRET` must be ≥ 32 chars and not a placeholder.
2. `UseForwardedHeaders` — required because nginx strips `X-Forwarded-Proto/For`.
3. CORS (`FrontendPolicy`): origins `localhost:3000`, `localhost:80`, `localhost`.
4. Auth/Authorization middleware.
5. Swagger (Development only).
6. `/health` endpoint (used by Docker healthcheck and nginx `depends_on`).

**Middleware order matters:** `app.UseCors()` must come before `app.UseAuthentication()` so CORS preflight OPTIONS requests get a 200, not a 401.

**EF Core:** All foreign keys default to `DeleteBehavior.Restrict` (set globally in `AppDbContext.OnModelCreating`) to avoid multiple-cascade-path SQL Server errors.

**Controllers:** All require `[Authorize]`. Role-based access uses `[Authorize(Roles = "Admin,Responsable")]` where needed.

**Auth flow:**
- `POST /api/auth/login` → validates bcrypt hash → returns JWT (24h) + refresh token (7 days).
- Refresh tokens stored in the `RefreshTokens` SQL table (DB-backed, single-use rotation) — survive restarts. See `Models/RefreshToken.cs`; the table is created by `database/init.sql`. Never use an in-memory `Dictionary<string, ...>` in production.
- JWT claims: `NameIdentifier` (UserId), `Email`, `Role`, `NomComplet`.

**ML proxy pattern** (in `PredictionsController`):
```csharp
var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
// POST to mlApiUrl/predict; extracts response["probabilite"] as ScoreRisque.
// On exception: logs warning, saves Prediction with ScoreRisque = 0 (graceful degradation).
```

**First admin:** Created at first boot by `DataSeeder` (`Data/DataSeeder.cs`) from the `ADMIN_SEED_EMAIL` / `ADMIN_SEED_PASSWORD` env vars (see `.env.example`). There is **no** `/api/auth/register` endpoint — `DataSeeder` is the only first-admin path. Defaults documented in `.env.example` use `admin@eniad.ma`; change the password from the UI/API after first login. The seeder refuses to run if the password is `< 12` chars or still a `CHANGE_ME` placeholder.

**Refresh tokens:** Stored in `RefreshTokens` SQL table (NOT in-memory). `AuthService` must query this table — never use a `Dictionary<string, ...>` in production as tokens die on restart. The `RefreshToken` model is at `Models/RefreshToken.cs`; the table is created by `database/init.sql`.

## ETL — OLTP → Data Warehouse

ETL logic lives in two places:
- **`database/etl.sql`** — canonical SQL with four idempotent `MERGE` statements (DimEtudiant, DimModule, DimTemps, FaitNotes). Uses cross-database references (`PFA_DB.dbo.*`).
- **`backend/PlateformePFA.API/Scripts/etl.sql`** — copy deployed with the binary (via `<CopyToOutputDirectory>Always</CopyToOutputDirectory>` in the `.csproj`).
- **`POST /api/admin/sync-dw`** — Admin-only endpoint in `AdminController.cs` that executes the script batch-by-batch. Call after bulk grade imports.

`database/etl.sql` is the **source of truth** — always edit it there and re-copy to `Scripts/etl.sql`.

## Database

**Two databases on the same SQL Server instance:**

| Database | Purpose | Schema style |
|---|---|---|
| `PFA_DB` | OLTP — daily operations | Normalized (6NF-ish) |
| `PFA_DW` | Data Warehouse — BI queries | Star schema |

**DW star schema:** `FaitNotes` fact table referencing `DimEtudiant`, `DimModule`, `DimTemps`.

**Init scripts** (`database/init.sql`, `database/init_dw.sql`, `database/copilot_readonly.sql`):
- Idempotent — every `CREATE TABLE` is wrapped in `IF OBJECT_ID(...) IS NULL`. Run on **every** boot (not just cold first boot) so pre-existing volumes roll forward.
- `init.sql` creates a least-privilege `pfa_app` SQL login (`db_datareader` + `db_datawriter` + `db_ddladmin` — the latter is required because `RuntimeMigrations.cs` applies additive schema changes at startup; see the note in `init.sql` for how to drop it once RuntimeMigrations is retired). The backend connects as `pfa_app`, not `sa`.
- `copilot_readonly.sql` creates `pfa_app_readonly` (db_datareader on PFA_DW only, no PFA_DB access) used by the Copilot `query_dw` tool AND by the ML service's DW training-data reads.
- Passwords are injected via sqlcmd variables (`-v APP_PASSWORD=`, `-v READONLY_PASSWORD=`) from `entrypoint.sh`.
- **Admin seed:** the first Admin is created by `DataSeeder` at backend startup from `ADMIN_SEED_EMAIL` / `ADMIN_SEED_PASSWORD` (see `.env.example`). There is **no** `/api/auth/register` endpoint.

**`entrypoint.sh`**: starts `sqlservr` in background, polls for readiness (450 attempts × 2s ≈ 15 min, to ride out "script upgrade mode" on existing volumes), waits for user databases to be ONLINE, exits 1 if it never comes up, then runs all three init scripts with `-b` (fail on SQL error). A `dbo.SchemaState` row is written only after every script succeeds; the Compose DB healthcheck requires it.

## ML Service

**Stack:** FastAPI 0.111.0 · Uvicorn · scikit-learn · XGBoost · pandas · joblib · MLflow (lineage)

Four routers (implemented): `routers/predict.py`, `routers/cluster.py`, `routers/forecast.py`, `routers/metrics.py`. Registered in `main.py` with `app.include_router(...)`.

Use FastAPI's `lifespan` context manager to load joblib models **once at startup** into `app.state.risk_model` / `cluster_model` / `forecast_model` — never load inside route handlers (would deserialize MB of data on every request).

**MLflow integration:** every training run (startup auto-train + `/predict/retrain`) logs params, metrics, tags, and the `.joblib` artifact to the `mlflow` service when `MLFLOW_TRACKING_URI` is set. Logging is best-effort via `ml_service/mlflow_client.py` — if MLflow is down, training proceeds with joblib-on-disk only. The MLflow UI is at `http://localhost:5001` on the host (not proxied by nginx).

Expected prediction endpoint (called by backend):
```
POST /predict
Body: { "moyenne_generale": float, "taux_absence": float, "nb_modules": int }
Returns: { "probabilite": float }   # 0.0 – 1.0
```

Validate the shared secret `ML_INTERNAL_TOKEN` from the `X-Internal-Token` header in a FastAPI dependency before processing any prediction request.

## Frontend

**Owner:** AMGHAR Anouar (alerts feature + exports feature).

**Stack (locked in — `package.json` is fully bootstrapped):**
- Vite + React + TypeScript
- TanStack Query v5 for data fetching and caching
- `react-router-dom` v6 for routing
- `tailwindcss` for styling
- `react-hook-form` + `zod` + `@hookform/resolvers` for forms
- **`apexcharts` + `react-apexcharts`** for BI charts (not recharts)
- Exports: `jsPDF` + `jspdf-autotable` (PDF), `xlsx` (Excel), native `Blob` (CSV)
- `clsx` for conditional classnames

**API base URL:** use `VITE_API_URL` env var (default `/api` — nginx proxies it). Do **not** use `REACT_APP_` prefix; that's Create React App, not Vite.

**Token storage:** store JWT in an `httpOnly` cookie set by the backend — not in `localStorage` (XSS risk). If using `Authorization` header instead, keep the token in React state / memory only.

**SPA routing:** the frontend container (`nginxinc/nginx-unprivileged`, port 8080) uses `frontend/nginx.conf` which has `try_files $uri $uri/ /index.html` — all unknown paths serve `index.html` so client-side routing works.

**Alerts feature (polling approach):**
```ts
// TanStack Query re-fetches every 30s — no SSE/WebSocket needed for a PFA.
useQuery({ queryKey: ['alertes'], queryFn: fetchAlertes, refetchInterval: 30_000 })
```

## Environment Variables

See [.env.example](.env.example) for the full annotated list. Key rules:
- `JWT_SECRET` must be ≥ 32 chars of entropy — backend refuses to start otherwise.
- `SA_PASSWORD` and `APP_DB_PASSWORD` are separate: `sa` is for DB admin only; the backend uses `pfa_app`.
- `ML_INTERNAL_TOKEN` is the shared secret between backend and ML service.
- Never commit a real `.env` — it is git-ignored; `.env.example` is safe to commit.

## nginx

Built from [nginx/Dockerfile](nginx/Dockerfile) (a thin wrapper around `nginx:1.27-alpine`). At startup `05-tls-select.sh` picks `nginx.http.conf` or `nginx.tls.conf` based on `ENABLE_TLS` and writes it to `/etc/nginx/nginx.conf`; shared location blocks live in `locations.conf`. Proxies `/api/copilotkit` → `copilot-runtime:4000`, `/api/` → `backend:8080`, `/` → `frontend:8080`. Key settings:
- `proxy_read_timeout 120s` (generous for slow ML predictions)
- `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host` headers forwarded
- Rate limit: 20 r/s per IP on `/api/`, burst 40; stricter 10 r/m on `/api/auth/login`
- gzip enabled for JSON + JS + CSS
- Security headers: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `X-XSS-Protection`
- TLS (opt-in via `ENABLE_TLS=true`): port 80 → 301 redirect to 443, TLS 1.2/1.3, HSTS. Put certs at `nginx/certs/fullchain.pem` + `privkey.pem` (see `nginx/generate-dev-certs.sh` for a self-signed dev cert; use Let's Encrypt / your CA for production).
