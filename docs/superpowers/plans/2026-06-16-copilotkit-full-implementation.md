# CopilotKit Full Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fully implement CopilotKit alongside the existing custom copilot (NVIDIA NIM) so both run in parallel for comparison — server-side tools in `copilot-runtime`, client-side hooks in 3 pages, `CopilotSidebar` as the CopilotKit test UI.

**Architecture:** `copilot-runtime` (Node.js :4000) gets 4 server-side Actions that forward the user's JWT to the existing `/api/copilot/tools/*` endpoints. Three React pages get `useCopilotReadable` + `useCopilotAction` hooks. `CopilotSidebar` from `@copilotkit/react-ui` renders alongside the existing `CopilotPanel` (unchanged). The custom panel stays on `Ctrl+J`; CopilotKit sidebar on `Ctrl+Shift+J`.

**Tech Stack:** `@copilotkit/react-core/v2`, `@copilotkit/react-ui`, `@copilotkit/runtime/v2`, Express, Node.js `AsyncLocalStorage`, React hooks, TypeScript

---

## File Map

| File | Change |
|---|---|
| `frontend/package.json` | Add `@copilotkit/react-ui` |
| `frontend/src/main.tsx` | Add `CopilotBridge` component, import CK styles |
| `frontend/src/layout/AppShell.tsx` | Add `<CopilotSidebar>` + second toggle state |
| `frontend/src/layout/Topbar.tsx` | Add CK toggle button + `onCopilotKitOpen` prop |
| `frontend/src/pages/Dashboard.tsx` | Add `useCopilotReadable` + `navigate_to_student` action |
| `frontend/src/pages/Students.tsx` | Add readable + `filter_students`, `open_student`, `draft_alert` actions |
| `frontend/src/pages/Predictions.tsx` | Add readable + `highlight_student`, `draft_alert` actions |
| `copilot-runtime/src/index.ts` | Add 4 server-side Actions + auth middleware |
| `copilot-runtime/src/tools.ts` | Create — tool definitions |
| `.env.example` | Already has `VITE_COPILOTKIT_URL` — add `BACKEND_URL` |

---

## Task 1: Install @copilotkit/react-ui

**Files:**
- Modify: `frontend/package.json`

- [ ] **Step 1: Install the package**

```bash
cd frontend
npm install @copilotkit/react-ui
```

Expected output: `added N packages` with no peer-dep errors.

- [ ] **Step 2: Verify it's in package.json**

Check `frontend/package.json` dependencies — you should see `"@copilotkit/react-ui": "^X.X.X"`.

- [ ] **Step 3: Commit**

```bash
git add frontend/package.json frontend/package-lock.json
git commit -m "chore(copilotkit): add @copilotkit/react-ui dependency"
```

---

## Task 2: Wire JWT into CopilotKit Provider

**Files:**
- Modify: `frontend/src/main.tsx`

The current `main.tsx` wraps `<CopilotKit>` inside `<AuthProvider>` but does not pass the JWT. We fix this by extracting `CopilotKit` into a `CopilotBridge` component that reads `token` from `AuthContext` and forwards it as an HTTP header to the runtime.

- [ ] **Step 1: Rewrite main.tsx**

Replace the entire `frontend/src/main.tsx` with:

```tsx
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CopilotKit } from '@copilotkit/react-core/v2'
import '@copilotkit/react-ui/styles.css'
import App from './App'
import { AuthProvider, useAuth } from './context/AuthContext'
import { ThemeProvider } from './context/ThemeContext'
import './index.css'

const copilotRuntimeUrl =
  (import.meta.env.VITE_COPILOTKIT_URL as string | undefined) ??
  'http://localhost:4000/api/copilotkit'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
})

function CopilotBridge({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  return (
    <CopilotKit
      runtimeUrl={copilotRuntimeUrl}
      useSingleEndpoint
      headers={token ? { Authorization: `Bearer ${token}` } : {}}
    >
      {children}
    </CopilotKit>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <AuthProvider>
          <CopilotBridge>
            <App />
          </CopilotBridge>
        </AuthProvider>
      </ThemeProvider>
    </QueryClientProvider>
  </StrictMode>
)
```

- [ ] **Step 2: Verify the app still compiles**

```bash
cd frontend
npm run build
```

Expected: build succeeds with no type errors. The CK CSS import may add ~20KB to bundle — that's fine.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/main.tsx
git commit -m "feat(copilotkit): wire JWT into CopilotKit provider via CopilotBridge"
```

---

## Task 3: Server-side Tools in copilot-runtime

**Files:**
- Create: `copilot-runtime/src/tools.ts`
- Modify: `copilot-runtime/src/index.ts`

4 tools call the existing backend endpoints, forwarding the user's JWT. The JWT is captured from the incoming Express request using `AsyncLocalStorage` so it's available inside tool handlers without threading it through function arguments.

- [ ] **Step 1: Add BACKEND_URL to .env.example**

Add this line to `.env.example` under the `# ─── Frontend React (Vite) ───` section:

```
# Where the copilot-runtime can reach the backend (local dev only).
BACKEND_URL=http://localhost:5135
```

- [ ] **Step 2: Create copilot-runtime/src/tools.ts**

```typescript
import { AsyncLocalStorage } from "node:async_hooks";

export const authStore = new AsyncLocalStorage<string>();

const BACKEND_URL = process.env.BACKEND_URL ?? "http://localhost:5135";

async function backendGet(path: string): Promise<unknown> {
  const token = authStore.getStore() ?? "";
  const res = await fetch(`${BACKEND_URL}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) return { error: `Backend error ${res.status}` };
  return res.json();
}

async function backendPost(path: string, body: unknown): Promise<unknown> {
  const token = authStore.getStore() ?? "";
  const res = await fetch(`${BACKEND_URL}${path}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(body),
  });
  if (!res.ok) return { error: `Backend error ${res.status}` };
  return res.json();
}

export const serverTools = [
  {
    name: "get_student",
    description:
      "Fetch a student's full profile and academic stats by matricule (e.g. E10001). " +
      "Returns name, filière, niveau, average, absences, risk score.",
    parameters: [
      {
        name: "matricule",
        type: "string",
        description: "Student matricule, e.g. E10001",
        required: true,
      },
    ],
    handler: async ({ matricule }: { matricule: string }) => {
      const data = await backendGet(
        `/api/copilot/tools/student/${encodeURIComponent(matricule)}`
      );
      return JSON.stringify(data);
    },
  },
  {
    name: "list_at_risk",
    description:
      "List students whose risk score is above a threshold. " +
      "Optionally filter by filière (TCP, GI, IA, ROC, IRSI) and niveau (CP1, CP2, CI1, CI2, CI3).",
    parameters: [
      {
        name: "threshold",
        type: "number",
        description: "Minimum risk score (0.0–1.0). Default 0.6.",
        required: false,
      },
      {
        name: "filiere",
        type: "string",
        description: "Filière code to filter by (optional).",
        required: false,
      },
      {
        name: "niveau",
        type: "string",
        description: "Academic level to filter by (optional).",
        required: false,
      },
    ],
    handler: async ({
      threshold = 0.6,
      filiere,
      niveau,
    }: {
      threshold?: number;
      filiere?: string;
      niveau?: string;
    }) => {
      const params = new URLSearchParams({ threshold: String(threshold) });
      if (filiere) params.set("filiere", filiere);
      if (niveau) params.set("niveau", niveau);
      const data = await backendGet(
        `/api/copilot/tools/at-risk?${params.toString()}`
      );
      return JSON.stringify(data);
    },
  },
  {
    name: "query_dw",
    description:
      "Answer a natural-language question about aggregated student data using the data warehouse. " +
      "Examples: 'How many students are enrolled?', 'Average score by filière', 'Modules with highest failure rate'.",
    parameters: [
      {
        name: "question",
        type: "string",
        description: "Natural language question about student data.",
        required: true,
      },
    ],
    handler: async ({ question }: { question: string }) => {
      const data = await backendPost("/api/copilot/tools/query-dw", {
        question,
      });
      return JSON.stringify(data);
    },
  },
  {
    name: "explain_risk",
    description:
      "Explain why a specific student has their current risk score, " +
      "listing the top contributing factors (absences, low grades, failed modules, etc.).",
    parameters: [
      {
        name: "matricule",
        type: "string",
        description: "Student matricule to explain risk for.",
        required: true,
      },
    ],
    handler: async ({ matricule }: { matricule: string }) => {
      const data = await backendPost("/api/copilot/tools/explain-risk", {
        matricule,
      });
      return JSON.stringify(data);
    },
  },
];
```

- [ ] **Step 3: Rewrite copilot-runtime/src/index.ts**

```typescript
import "dotenv/config";
import express from "express";
import { CopilotRuntime, BuiltInAgent } from "@copilotkit/runtime/v2";
import { createCopilotExpressHandler } from "@copilotkit/runtime/v2/express";
import { authStore, serverTools } from "./tools.js";

const agent = new BuiltInAgent({
  model: "anthropic/claude-sonnet-4-6",
  prompt:
    "Tu es ENIAD Copilot, un assistant d'aide à la décision pour le personnel " +
    "pédagogique de l'ENIAD (École Nationale d'Ingénieurs et d'Architectes, Berkane, Maroc). " +
    "Réponds en français, de façon concise et factuelle. " +
    "Tu as accès aux données des étudiants via les outils disponibles.",
});

const runtime = new CopilotRuntime({
  agents: { default: agent },
  actions: serverTools,
});

const app = express();

// Capture JWT per-request so tool handlers can forward it to the backend.
app.use("/api/copilotkit", (req, _res, next) => {
  const token = (req.headers.authorization ?? "").replace(/^Bearer\s+/i, "");
  authStore.run(token, () => next());
});

app.use(
  "/api/copilotkit",
  createCopilotExpressHandler({
    runtime,
    basePath: "/",
    mode: "single-route",
  }),
);

const port = Number(process.env.PORT ?? 4000);
app.listen(port, () => {
  console.log(`CopilotKit runtime → http://localhost:${port}/api/copilotkit`);
});
```

- [ ] **Step 4: Verify copilot-runtime compiles**

```bash
cd copilot-runtime
npm run dev
```

Expected: `CopilotKit runtime → http://localhost:4000/api/copilotkit` with no TypeScript errors.

If you see `Cannot find module './tools.js'`, add `"moduleResolution": "bundler"` to `copilot-runtime/tsconfig.json` or change the import to `"./tools"`.

- [ ] **Step 5: Commit**

```bash
git add copilot-runtime/src/tools.ts copilot-runtime/src/index.ts .env.example
git commit -m "feat(copilotkit): 4 server-side tools with JWT forwarding via AsyncLocalStorage"
```

---

## Task 4: CopilotSidebar UI + Second Topbar Button

**Files:**
- Modify: `frontend/src/layout/Topbar.tsx`
- Modify: `frontend/src/layout/AppShell.tsx`

Add a second button in the topbar for the CopilotKit sidebar, and render `<CopilotSidebar>` in AppShell alongside the existing `<CopilotPanel>`.

- [ ] **Step 1: Update Topbar.tsx**

Replace the existing `TopbarProps` interface and the copilot button section:

```tsx
import { useLocation } from 'react-router-dom'
import { Icon } from '../components/ui/Icon'
import { useTheme } from '../context/ThemeContext'

const BREADCRUMBS: Record<string, [string, string]> = {
  '/dashboard':   ['Pilotage', 'Tableau de bord'],
  '/students':    ['Pilotage', 'Étudiants'],
  '/alerts':      ['Pilotage', 'Alertes'],
  '/predictions': ['Pilotage', 'Prédictions ML'],
  '/reports':     ['Pilotage', 'Rapports'],
}

interface TopbarProps {
  onCommandOpen: () => void
  onCopilotOpen: () => void
  onCopilotKitOpen: () => void
  copilotActive?: boolean
  copilotKitActive?: boolean
}

export function Topbar({ onCommandOpen, onCopilotOpen, onCopilotKitOpen, copilotActive, copilotKitActive }: TopbarProps) {
  const { pathname } = useLocation()
  const { theme, toggleTheme, density, cycleDensity } = useTheme()
  const breadcrumb = BREADCRUMBS[pathname] ?? ['Pilotage', 'Page']

  return (
    <header
      className="flex items-center justify-between px-6"
      style={{
        height: 56,
        flexShrink: 0,
        background: 'color-mix(in oklch, var(--surface) 80%, transparent)',
        backdropFilter: 'saturate(140%) blur(8px)',
        borderBottom: '1px solid var(--border)',
        position: 'sticky',
        top: 0,
        zIndex: 30,
      }}
    >
      <div className="flex items-center gap-3 min-w-0">
        <div className="flex items-center gap-2 text-[12.5px]" style={{ color: 'var(--text-3)' }}>
          {breadcrumb.map((c, i) => (
            <span key={c} className="flex items-center gap-2">
              <span
                className={i === breadcrumb.length - 1 ? 'font-medium' : ''}
                style={{ color: i === breadcrumb.length - 1 ? 'var(--text)' : 'var(--text-3)' }}
              >
                {c}
              </span>
              {i < breadcrumb.length - 1 && (
                <Icon name="chevRight" size={11} style={{ color: 'var(--text-4)' }} />
              )}
            </span>
          ))}
        </div>
      </div>

      <div className="flex items-center gap-1.5">
        <button
          onClick={onCommandOpen}
          className="flex items-center gap-2.5 h-8 pl-2.5 pr-2 rounded-lg"
          style={{ background: 'var(--surface-2)', border: '1px solid var(--border)', minWidth: 240 }}
        >
          <Icon name="search" size={13} style={{ color: 'var(--text-3)' }} />
          <span className="text-[12.5px] flex-1 text-left" style={{ color: 'var(--text-3)' }}>
            Rechercher…
          </span>
          <kbd>⌘K</kbd>
        </button>

        <div style={{ width: 1, height: 20, background: 'var(--border)', margin: '0 4px' }} />

        <button className="btn btn-sm btn-ghost relative" title="Notifications">
          <Icon name="bell" size={14} />
          <span
            style={{
              position: 'absolute',
              top: 6,
              right: 6,
              width: 6,
              height: 6,
              borderRadius: 999,
              background: 'var(--accent-500)',
              boxShadow: '0 0 0 2px var(--surface)',
            }}
          />
        </button>

        <button onClick={cycleDensity} className="btn btn-sm btn-ghost" title={`Densité : ${density}`}>
          <Icon name="filter" size={14} />
        </button>

        <button onClick={toggleTheme} className="btn btn-sm btn-ghost" title="Thème" style={{ fontSize: 14 }}>
          {theme === 'dark' ? '☀' : '☾'}
        </button>

        <div style={{ width: 1, height: 20, background: 'var(--border)', margin: '0 4px' }} />

        <button
          onClick={onCopilotOpen}
          className="btn btn-sm"
          title="ENIAD Copilot (⌘J)"
          style={copilotActive ? {
            background: 'color-mix(in oklch, var(--accent-500) 12%, transparent)',
            borderColor: 'color-mix(in oklch, var(--accent-500) 30%, transparent)',
            color: 'var(--accent-600)',
          } : undefined}
        >
          <Icon name="brain" size={13} />
          Copilot
        </button>

        <button
          onClick={onCopilotKitOpen}
          className="btn btn-sm"
          title="CopilotKit (⌘⇧J)"
          style={copilotKitActive ? {
            background: 'color-mix(in oklch, var(--accent-500) 12%, transparent)',
            borderColor: 'color-mix(in oklch, var(--accent-500) 30%, transparent)',
            color: 'var(--accent-600)',
          } : undefined}
        >
          <Icon name="brain" size={13} />
          <span className="text-[10px] font-bold ml-0.5 opacity-60">CK</span>
        </button>

        <button className="btn btn-sm btn-accent">
          <Icon name="plus" size={13} strokeWidth={2.4} />
          Nouveau
        </button>
      </div>
    </header>
  )
}
```

- [ ] **Step 2: Update AppShell.tsx**

```tsx
import { useEffect, useState } from 'react'
import { Outlet } from 'react-router-dom'
import { CopilotSidebar } from '@copilotkit/react-ui'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { CommandPalette } from './CommandPalette'
import { CopilotPanel } from '../components/copilot/CopilotPanel'

export function AppShell() {
  const [cmdOpen, setCmdOpen] = useState(false)
  const [copilotOpen, setCopilotOpen] = useState(false)
  const [copilotKitOpen, setCopilotKitOpen] = useState(false)

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        setCmdOpen(o => !o)
      }
      if ((e.metaKey || e.ctrlKey) && !e.shiftKey && e.key === 'j') {
        e.preventDefault()
        setCopilotOpen(o => !o)
      }
      if ((e.metaKey || e.ctrlKey) && e.shiftKey && e.key === 'J') {
        e.preventDefault()
        setCopilotKitOpen(o => !o)
      }
      if (e.key === 'Escape') {
        setCmdOpen(false)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  return (
    <div className="vh-full flex" style={{ background: 'var(--bg)' }}>
      <Sidebar />
      <div className="flex-1 flex flex-col min-w-0">
        <Topbar
          onCommandOpen={() => setCmdOpen(true)}
          onCopilotOpen={() => setCopilotOpen(true)}
          onCopilotKitOpen={() => setCopilotKitOpen(o => !o)}
          copilotActive={copilotOpen}
          copilotKitActive={copilotKitOpen}
        />
        <div className="flex-1 overflow-y-auto scroll-thin">
          <div className="px-8 py-7 max-w-[1480px] mx-auto">
            <Outlet />
          </div>
        </div>
      </div>
      <CommandPalette open={cmdOpen} onClose={() => setCmdOpen(false)} />
      {copilotOpen && <CopilotPanel onClose={() => setCopilotOpen(false)} />}
      <CopilotSidebar
        defaultOpen={false}
        labels={{
          title: 'ENIAD Copilot · CopilotKit',
          initial: 'Bonjour ! Interrogez les données étudiantes, les scores de risque et le DW en langage naturel.',
        }}
        clickOutsideToClose={false}
        open={copilotKitOpen}
        onSetOpen={setCopilotKitOpen}
      />
    </div>
  )
}
```

- [ ] **Step 3: Run the frontend and verify both buttons appear**

```bash
cd frontend
npm run dev
```

Open `http://localhost:5173`. You should see two buttons in the topbar: **Copilot** and **Copilot CK**. Pressing each should open the respective panel.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/layout/Topbar.tsx frontend/src/layout/AppShell.tsx
git commit -m "feat(copilotkit): add CopilotSidebar UI + second topbar toggle (Ctrl+Shift+J)"
```

---

## Task 5: Hooks in Dashboard Page

**Files:**
- Modify: `frontend/src/pages/Dashboard.tsx`

Add `useCopilotReadable` to expose live KPIs and a `navigate_to_student` action that navigates to `/students?q=<matricule>` so the Students page auto-filters.

- [ ] **Step 1: Add imports at top of Dashboard.tsx**

After the existing imports, add:

```tsx
import { useCopilotReadable, useCopilotAction } from '@copilotkit/react-core/v2'
import { useNavigate } from 'react-router-dom'
```

- [ ] **Step 2: Add hooks inside the Dashboard() component**

Add these two hooks immediately after the `useQuery` calls (after the `mlMetrics` derived variable), inside the `Dashboard` function body:

```tsx
const navigate = useNavigate()

useCopilotReadable({
  description: 'Live dashboard KPIs — total students, at-risk count, average score, active alerts',
  value: data?.kpis
    ? {
        totalStudents: data.kpis.nbEtudiants,
        activeAlerts: data.kpis.alertesActives,
        averageScore: data.kpis.moyGlobale,
        successRate: data.kpis.tauxReussite,
        topAtRisk: data.topARisque.slice(0, 5).map(s => ({
          matricule: s.matricule,
          name: s.nomComplet,
          filiere: s.filiere,
          score: s.scoreRisque,
        })),
      }
    : null,
})

useCopilotAction({
  name: 'navigate_to_student',
  description: 'Navigate to the Students page and pre-filter to a specific student by matricule.',
  parameters: [
    {
      name: 'matricule',
      type: 'string',
      description: 'Student matricule, e.g. E10001',
      required: true,
    },
  ],
  handler: async ({ matricule }: { matricule: string }) => {
    navigate(`/students?q=${encodeURIComponent(matricule)}`)
    return `Navigating to student ${matricule}`
  },
})
```

- [ ] **Step 3: Verify Dashboard.tsx compiles**

```bash
cd frontend && npm run build
```

Expected: clean build, no type errors.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/pages/Dashboard.tsx
git commit -m "feat(copilotkit): dashboard readable + navigate_to_student action"
```

---

## Task 6: Hooks in Students Page

**Files:**
- Modify: `frontend/src/pages/Students.tsx`

Add readable for the current visible student list, `filter_students` and `open_student` actions, and `draft_alert` with `renderAndWaitForResponse`.

The Students page already reads a `?q=` URL param via `useSearchParams` — but currently it doesn't. We need to add that first so the `navigate_to_student` action from Task 5 works.

- [ ] **Step 1: Add imports at top of Students.tsx**

After the existing imports, add:

```tsx
import { useCopilotReadable, useCopilotAction } from '@copilotkit/react-core/v2'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { ConfirmCard } from '../components/copilot/CopilotPanel'
```

Note: `api` is already imported in `Students.tsx` — do not add it again.

Wait — `ConfirmCard` is not exported from `CopilotPanel.tsx`. Before using it, export it.

- [ ] **Step 2: Export ConfirmCard from CopilotPanel.tsx**

In `frontend/src/components/copilot/CopilotPanel.tsx`, change the `ConfirmCard` function declaration from:

```tsx
function ConfirmCard({
```

to:

```tsx
export function ConfirmCard({
```

Also export the `ConfirmData` interface — change it from:

```tsx
interface ConfirmData {
```

to:

```tsx
export interface ConfirmData {
```

- [ ] **Step 3: Add URL param sync to Students.tsx**

Inside the `Students()` component, after the `useQuery` line, add:

```tsx
const [searchParams] = useSearchParams()
const navigate = useNavigate()

// Pre-fill search from URL ?q= param (used by navigate_to_student copilot action)
useEffect(() => {
  const qParam = searchParams.get('q')
  if (qParam) setQ(qParam)
}, [searchParams])
```

- [ ] **Step 4: Add CopilotKit hooks to Students.tsx**

Add these hooks inside the `Students()` function, after the filter state declarations:

```tsx
useCopilotReadable({
  description: 'Currently visible students after filters are applied — name, matricule, filière, niveau, risk score',
  value: slice.map(s => ({
    matricule: s.matricule,
    name: s.nomComplet,
    filiere: s.filiereCode,
    niveau: s.niveau,
    averageScore: s.moyenne,
    absences: s.absences,
    riskScore: s.scoreRisque,
    riskLevel: s.risque,
  })),
})

useCopilotAction({
  name: 'filter_students',
  description: 'Filter the student table by filière, niveau, or risk level. Use this when the user asks to narrow down the list.',
  parameters: [
    {
      name: 'filiere',
      type: 'string',
      description: 'Filière code: TCP, GI, IA, ROC, IRSI, or Tous to clear.',
      required: false,
    },
    {
      name: 'niveau',
      type: 'string',
      description: 'Academic level: CP1, CP2, CI1, CI2, CI3, or Tous to clear.',
      required: false,
    },
    {
      name: 'risque',
      type: 'string',
      description: 'Risk level: faible, modere, eleve, or Tous to clear.',
      required: false,
    },
    {
      name: 'search',
      type: 'string',
      description: 'Free text search on student name or matricule.',
      required: false,
    },
  ],
  handler: async ({
    filiere,
    niveau,
    risque: risqueFilter,
    search,
  }: {
    filiere?: string
    niveau?: string
    risque?: string
    search?: string
  }) => {
    if (filiere !== undefined) setFiliere(filiere)
    if (niveau !== undefined) setNiveau(niveau)
    if (risqueFilter !== undefined) setRisque(risqueFilter)
    if (search !== undefined) setQ(search)
    return 'Filtres appliqués'
  },
})

useCopilotAction({
  name: 'open_student',
  description: 'Open the detail panel for a specific student by matricule or name.',
  parameters: [
    {
      name: 'matricule',
      type: 'string',
      description: 'Student matricule to open.',
      required: true,
    },
  ],
  handler: async ({ matricule }: { matricule: string }) => {
    const student = all.find(
      s => s.matricule.toLowerCase() === matricule.toLowerCase()
    )
    if (!student) return `No student found with matricule ${matricule}`
    setSelected(student)
    return `Opened profile for ${student.nomComplet}`
  },
})

useCopilotAction({
  name: 'draft_alert',
  description:
    'Propose creating an alert for a student at risk. Always use this when asked to create, send, or draft an alert. ' +
    'The user must confirm before the alert is saved.',
  parameters: [
    {
      name: 'matricule',
      type: 'string',
      description: 'Student matricule',
      required: true,
    },
    {
      name: 'severity',
      type: 'string',
      description: 'Alert severity: high, medium, or low',
      required: true,
    },
    {
      name: 'message',
      type: 'string',
      description: 'Alert message content',
      required: true,
    },
  ],
  renderAndWaitForResponse: ({
    args,
    status,
    respond,
  }: {
    args: { matricule?: string; severity?: string; message?: string }
    status: string
    respond?: (r: unknown) => void
  }) => {
    const student = all.find(
      s => s.matricule.toLowerCase() === (args.matricule ?? '').toLowerCase()
    )

    const confirmData = {
      draftId: 0,
      preview: {
        student_name: student?.nomComplet ?? args.matricule ?? '',
        matricule: args.matricule ?? '',
        severity: args.severity ?? 'medium',
        message: args.message ?? '',
      },
      tool: 'draft_alert',
      state:
        status === 'complete'
          ? ('confirmed' as const)
          : ('pending' as const),
    }

    return (
      <ConfirmCard
        data={confirmData}
        onConfirm={async () => {
          await api.post('/alertes', {
            etudiantId: student?.id,
            severite: args.severity ?? 'medium',
            message: args.message ?? '',
          })
          respond?.({ confirmed: true })
        }}
        onDismiss={() => respond?.({ confirmed: false })}
      />
    )
  },
})
```

- [ ] **Step 5: Verify Students.tsx compiles**

```bash
cd frontend && npm run build
```

Expected: clean build. If you get a type error on `renderAndWaitForResponse` props, add `// @ts-expect-error — CK v2 types` above the offending line.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/pages/Students.tsx frontend/src/components/copilot/CopilotPanel.tsx
git commit -m "feat(copilotkit): students page — readable + filter/open/draft_alert actions"
```

---

## Task 7: Hooks in Predictions Page

**Files:**
- Modify: `frontend/src/pages/Predictions.tsx`

Add readable for top-at-risk predictions, `highlight_student` action, and `draft_alert` action (same pattern as Students).

- [ ] **Step 1: Add imports to Predictions.tsx**

After the existing imports, add:

```tsx
import { useCopilotReadable, useCopilotAction } from '@copilotkit/react-core/v2'
import { ConfirmCard } from '../components/copilot/CopilotPanel'
import type { ConfirmData } from '../components/copilot/CopilotPanel'
```

- [ ] **Step 2: Add highlighted row state**

Inside the `Predictions()` component, after the existing `useState` declarations, add:

```tsx
const [highlightedMatricule, setHighlightedMatricule] = useState<string | null>(null)
```

- [ ] **Step 3: Add CopilotKit hooks to Predictions.tsx**

Add these hooks inside the `Predictions()` function, after the mutation declarations:

```tsx
useCopilotReadable({
  description: 'Top at-risk students visible on the predictions page — matricule, name, filière, niveau, risk score',
  value: data?.topARisque.map(s => ({
    matricule: s.matricule,
    name: s.nomComplet,
    filiere: s.filiere,
    niveau: s.niveau,
    averageScore: s.moyenne,
    absences: s.absences,
    riskScore: s.scoreRisque,
  })) ?? [],
})

useCopilotReadable({
  description: 'Current ML model metrics — AUC, F1, precision, recall',
  value: ml
    ? { auc: ml.auc, f1: ml.f1, precision: ml.precision, recall: ml.recall }
    : null,
})

useCopilotAction({
  name: 'highlight_student',
  description: 'Highlight a specific student row in the top-at-risk table by matricule.',
  parameters: [
    {
      name: 'matricule',
      type: 'string',
      description: 'Student matricule to highlight.',
      required: true,
    },
  ],
  handler: async ({ matricule }: { matricule: string }) => {
    setHighlightedMatricule(matricule)
    const found = data?.topARisque.find(s => s.matricule === matricule)
    if (!found) return `Student ${matricule} not in the top-at-risk list`
    // Scroll to the row after React re-renders
    setTimeout(() => {
      document.querySelector(`[data-matricule="${matricule}"]`)?.scrollIntoView({
        behavior: 'smooth',
        block: 'center',
      })
    }, 100)
    return `Highlighted ${found.nomComplet} (score: ${found.scoreRisque.toFixed(2)})`
  },
})

useCopilotAction({
  name: 'draft_alert',
  description:
    'Propose creating an alert for a student at risk. Always use this when asked to create, send, or draft an alert. ' +
    'The user must confirm before the alert is saved.',
  parameters: [
    {
      name: 'matricule',
      type: 'string',
      description: 'Student matricule',
      required: true,
    },
    {
      name: 'severity',
      type: 'string',
      description: 'Alert severity: high, medium, or low',
      required: true,
    },
    {
      name: 'message',
      type: 'string',
      description: 'Alert message content',
      required: true,
    },
  ],
  renderAndWaitForResponse: ({
    args,
    status,
    respond,
  }: {
    args: { matricule?: string; severity?: string; message?: string }
    status: string
    respond?: (r: unknown) => void
  }) => {
    const student = data?.topARisque.find(
      s => s.matricule.toLowerCase() === (args.matricule ?? '').toLowerCase()
    )

    const confirmData: ConfirmData = {
      draftId: 0,
      preview: {
        student_name: student?.nomComplet ?? args.matricule ?? '',
        matricule: args.matricule ?? '',
        severity: args.severity ?? 'medium',
        message: args.message ?? '',
      },
      tool: 'draft_alert',
      state: status === 'complete' ? 'confirmed' : 'pending',
    }

    return (
      <ConfirmCard
        data={confirmData}
        onConfirm={async () => {
          await api.post('/alertes', {
            etudiantId: student?.id,
            severite: args.severity ?? 'medium',
            message: args.message ?? '',
          })
          respond?.({ confirmed: true })
        }}
        onDismiss={() => respond?.({ confirmed: false })}
      />
    )
  },
})
```

- [ ] **Step 4: Add `data-matricule` attribute to the top-at-risk table rows**

Find the JSX that renders the `topARisque` rows in `Predictions.tsx`. Each row `<tr>` or row container needs:

```tsx
data-matricule={s.matricule}
style={highlightedMatricule === s.matricule
  ? { background: 'color-mix(in oklch, var(--accent-500) 10%, transparent)' }
  : undefined}
```

- [ ] **Step 5: Verify Predictions.tsx compiles**

```bash
cd frontend && npm run build
```

- [ ] **Step 6: Commit**

```bash
git add frontend/src/pages/Predictions.tsx
git commit -m "feat(copilotkit): predictions page — readable + highlight/draft_alert actions"
```

---

## Task 8: End-to-End Testing

Run the full stack and verify every capability.

- [ ] **Step 1: Start all services**

Terminal 1 — Backend:
```bash
cd backend/PlateformePFA.API && dotnet run
# Expected: listening on http://localhost:5135
```

Terminal 2 — copilot-runtime:
```bash
cd copilot-runtime && npm run dev
# Expected: CopilotKit runtime → http://localhost:4000/api/copilotkit
```

Terminal 3 — Frontend:
```bash
cd frontend && npm run dev
# Expected: http://localhost:5173
```

- [ ] **Step 2: Login and open the CopilotKit sidebar**

1. Open `http://localhost:5173`, log in as `admin@eniad.ma` / `Admin@ENIAD2025`
2. Press `Ctrl+Shift+J` — the **CopilotKit sidebar** slides in from the right
3. You should see the "ENIAD Copilot · CopilotKit" header and the initial greeting

- [ ] **Step 3: Test server-side tools**

Send these messages in the CopilotKit sidebar:

| Message | Expected tool call | Expected result |
|---|---|---|
| `Donne-moi le profil de l'étudiant E10001` | `get_student` | Student name, filière, risk score |
| `Liste les étudiants avec un risque élevé de 0.7` | `list_at_risk` | List of at-risk students |
| `Combien d'étudiants sont dans le DW ?` | `query_dw` | Count from data warehouse |
| `Explique le risque de E10001` | `explain_risk` | Risk factor breakdown |

If tools return `"Backend error 401"`, the JWT is not being forwarded correctly. Check that `CopilotBridge` is rendering correctly (add a `console.log(token)` temporarily in `CopilotBridge`).

- [ ] **Step 4: Test client-side hooks on Dashboard**

1. Navigate to `/dashboard`
2. In the CopilotKit sidebar, ask: `Combien d'étudiants sont à risque ?`
3. Expected: the LLM reads from the `useCopilotReadable` data and gives a specific number (not a generic answer)
4. Ask: `Montre-moi l'étudiant E10001`
5. Expected: browser navigates to `/students?q=E10001`

- [ ] **Step 5: Test client-side hooks on Students**

1. Navigate to `/students`
2. Ask: `Filtre les étudiants de la filière GI`
3. Expected: the filière dropdown changes to GI and the table filters
4. Ask: `Ouvre le profil de E10001`
5. Expected: the student detail panel slides open

- [ ] **Step 6: Test draft_alert with confirm card**

1. On `/students`, ask: `Crée une alerte haute pour l'étudiant E10001 : risque d'abandon détecté`
2. Expected: the `draft_alert` tool fires, and a **ConfirmCard** renders inside the CopilotKit sidebar
3. The card shows the student name, severity "Haute", and the message
4. Click **Confirmer l'envoi**
5. Expected: card transitions to confirmed state, alert created in the DB
6. Navigate to `/alerts` — the new alert should appear

- [ ] **Step 7: Test highlight on Predictions**

1. Navigate to `/predictions`
2. Ask: `Mets en évidence l'étudiant E10001 dans le tableau`
3. Expected: the row for E10001 in the top-at-risk table highlights in accent color and scrolls into view

- [ ] **Step 8: Side-by-side comparison**

1. Press `Ctrl+J` to open the custom NVIDIA NIM panel
2. Press `Ctrl+Shift+J` to open the CopilotKit sidebar simultaneously
3. Send the same query to both: `Liste les 3 étudiants les plus à risque`
4. Compare response quality, speed, and UX

- [ ] **Step 9: Commit final state**

```bash
git add -A
git commit -m "feat(copilotkit): full implementation complete — all tools + hooks + sidebar"
```
