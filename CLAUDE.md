# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Plateforme décisionnelle ENIAD 2025/2026 — a BI & predictive-analytics platform for student performance. Built by a trio: AIT ALI / AMGHAR (frontend + alerts + exports) / ENGAR, supervised by Prof. LHIADI.

## Service Map

```
Internet → nginx:80 (pfa_public)
                ├─ /api/*  → backend:8080  (pfa_public + pfa_internal_db + pfa_internal_ml)
                └─ /*      → frontend:8080 (pfa_public)

backend:8080 → ml-service:8000  (pfa_internal_ml, internal=true)
backend:8080 → db:1433          (pfa_internal_db, internal=true)
```

Three isolated Docker networks: `pfa_public`, `pfa_internal_db`, `pfa_internal_ml`. The ML service and database are unreachable from the public internet by design (`internal: true`).

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
dotnet ef migrations add <Name>
dotnet ef database update
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
- Refresh tokens stored in an in-memory `Dictionary<string, (UserId, Expiry)>` in `AuthService` — **lost on restart**, fine for dev/demo.
- JWT claims: `NameIdentifier` (UserId), `Email`, `Role`, `NomComplet`.

**ML proxy pattern** (in `PredictionsController`):
```csharp
var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
// POST to mlApiUrl/predict; extracts response["probabilite"] as ScoreRisque.
// On exception: logs warning, saves Prediction with ScoreRisque = 0 (graceful degradation).
```

**First admin:** Created by `DataSeeder` at startup: `admin@eniad.dz` / `Admin@ENIAD2025`.

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

**Init scripts** (`database/init.sql`, `database/init_dw.sql`):
- Idempotent — every `CREATE TABLE` is wrapped in `IF OBJECT_ID(...) IS NULL`.
- `init.sql` creates a least-privilege `pfa_app` SQL login (`db_datareader` + `db_datawriter`) — the backend connects as `pfa_app`, not `sa`.
- Password is injected via sqlcmd variable `-v APP_PASSWORD=` from `entrypoint.sh`.
- **No admin seed** — create the first user through the API `/api/auth/register` endpoint.

**`entrypoint.sh`**: starts `sqlservr` in background, polls for readiness (30 attempts × 2s), exits 1 if it never comes up, then runs both scripts with `-b` (fail on SQL error).

## ML Service

**Stack:** FastAPI 0.111.0 · Uvicorn · scikit-learn · XGBoost · pandas · joblib

Three routers to implement: `routers/predict.py`, `routers/cluster.py`, `routers/forecast.py`. Register them in `main.py` with `app.include_router(...)`.

Use FastAPI's `lifespan` context manager to load joblib models **once at startup** into `app.state.models` — never load inside route handlers (would deserialize MB of data on every request).

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

[nginx/nginx.conf](nginx/nginx.conf) proxies `/api/` → `backend:8080` and `/` → `frontend:8080`. Key settings already configured:
- `proxy_read_timeout 120s` (generous for slow ML predictions)
- `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host` headers forwarded
- Rate limit: 20 r/s per IP on `/api/`, burst 40
- gzip enabled for JSON + JS + CSS
- Security headers: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`
