# Make Copilot Non-Blocking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Phase 1 ("Make Copilot non-blocking") of the UI/UX plan by making the Copilot closed by default, driving its state from a single controlled React state source, supporting a responsive full-screen drawer layout on mobile, restoring focus on close, preventing body scroll, and allowing Escape key closing.

**Architecture:** Drive the Copilot state from `copilotOpen` state in `AppShell.tsx`. Synchronize this state with `chatConfig.isModalOpen` via two `useEffect` hooks. Set up responsive styling in `index.css` to handle screen sizes (< 640px full-screen drawer, >= 640px 380px drawer with layout push). Implement focus management and Escape key closing inside `AppShell.tsx`.

**Tech Stack:** React, TypeScript, Vitest, Testing Library, Tailwind CSS

---

## File Structure

- Modify: [AppShell.tsx](file:///D:/pfa/plateforme-decisionnelle-pfa/frontend/src/layout/AppShell.tsx) — Main layout shell driving the Copilot state, focus, mobile backdrop, and shortcuts.
- Modify: [main.tsx](file:///D:/pfa/plateforme-decisionnelle-pfa/frontend/src/main.tsx) — React entrypoint containing the `<CopilotKit>` component.
- Modify: [index.css](file:///D:/pfa/plateforme-decisionnelle-pfa/frontend/src/index.css) — Custom styles for drawer animation and responsive layout.
- Create: [AppShell.test.tsx](file:///D:/pfa/plateforme-decisionnelle-pfa/frontend/src/layout/AppShell.test.tsx) — Focused unit tests for AppShell Copilot behavior.

---

### Task 1: Update main.tsx

**Files:**
- Modify: `D:/pfa/plateforme-decisionnelle-pfa/frontend/src/main.tsx`

- [ ] **Step 1: Update CopilotBridge to include showDevConsole**

Modify the `<CopilotKit>` declaration in `CopilotBridge` to conditionalize `showDevConsole` based on `import.meta.env.DEV`.

```tsx
function CopilotBridge({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  return (
    <CopilotKit
      // Remount when auth becomes available so the runtime connection is
      // (re)established WITH the Authorization header. The token lives in memory
      // only, so at first app mount it's null; without this key CopilotKit would
      // connect once unauthenticated and never re-send the header → 401.
      key={token ? 'auth' : 'anon'}
      runtimeUrl={copilotRuntimeUrl}
      useSingleEndpoint
      headers={token ? { Authorization: `Bearer ${token}` } : {}}
      showDevConsole={import.meta.env.DEV}
    >
      {children}
    </CopilotKit>
  )
}
```

---

### Task 2: State Management and Sync in AppShell.tsx

**Files:**
- Modify: `D:/pfa/plateforme-decisionnelle-pfa/frontend/src/layout/AppShell.tsx`

- [ ] **Step 1: Implement state, sync hooks, and backdrop/close buttons in AppShell.tsx**

Update `AppShell.tsx` to:
1. Declare `const [copilotOpen, setCopilotOpen] = useState(false)` as the single source of truth.
2. Synchronize `copilotOpen` with `chatConfig.isModalOpen` using `useEffect` hooks.
3. Automatically close Copilot on route change (`pathname`).
4. Set `defaultOpen={false}` on `CopilotSidebar`.
5. Render a custom 44x44px close button for `CopilotSidebar` on mobile.
6. Render the fallback Copilot panel with a responsive 44x44px close button.
7. Manage body scroll, escape keys, and focus restoration using refs and effects.

Implementation Code:
```tsx
import { useCallback, useEffect, useState, useRef } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { CopilotSidebar, useCopilotChatConfiguration } from '@copilotkit/react-core/v2'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { CommandPalette } from './CommandPalette'
import { Icon } from '../components/ui/Icon'
import { useAuth } from '../context/AuthContext'

export function AppShell() {
  const [cmdOpen, setCmdOpen] = useState(false)
  const [navOpen, setNavOpen] = useState(false)
  const { pathname } = useLocation()
  const navigate = useNavigate()
  const { copilotActive } = useAuth()
  const chatConfig = useCopilotChatConfiguration()
  const [copilotOpen, setCopilotOpen] = useState(false)
  const lastActiveElementRef = useRef<HTMLElement | null>(null)

  // Close the mobile nav drawer whenever the route changes.
  useEffect(() => { setNavOpen(false) }, [pathname])

  // Close Copilot whenever the route changes.
  useEffect(() => {
    setCopilotOpen(false)
  }, [pathname])

  // Sync state from chatConfig.isModalOpen -> copilotOpen
  useEffect(() => {
    if (copilotActive && chatConfig && chatConfig.isModalOpen !== copilotOpen) {
      setCopilotOpen(chatConfig.isModalOpen)
    }
  }, [chatConfig?.isModalOpen, copilotActive, copilotOpen])

  // Sync state from copilotOpen -> chatConfig.isModalOpen
  useEffect(() => {
    if (copilotActive && chatConfig && chatConfig.isModalOpen !== copilotOpen) {
      chatConfig.setModalOpen(copilotOpen)
    }
  }, [copilotOpen, copilotActive, chatConfig])

  // Prevent body scroll when Copilot is open on mobile
  useEffect(() => {
    if (copilotOpen) {
      const checkMobile = () => window.innerWidth < 640
      if (checkMobile()) {
        document.body.style.overflow = 'hidden'
      }

      const onResize = () => {
        if (window.innerWidth >= 640) {
          document.body.style.overflow = ''
        } else {
          document.body.style.overflow = 'hidden'
        }
      }
      window.addEventListener('resize', onResize)
      return () => {
        document.body.style.overflow = ''
        window.removeEventListener('resize', onResize)
      }
    }
  }, [copilotOpen])

  // Restore focus to launcher on close
  useEffect(() => {
    if (copilotOpen) {
      if (document.activeElement instanceof HTMLElement) {
        lastActiveElementRef.current = document.activeElement
      }
    } else {
      if (lastActiveElementRef.current) {
        lastActiveElementRef.current.focus()
        lastActiveElementRef.current = null
      }
    }
  }, [copilotOpen])

  useEffect(() => {
    let gPressed = false
    let gTimer: number | undefined
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        setCmdOpen(o => !o)
        return
      }
      if ((e.metaKey || e.ctrlKey) && e.key === 'j') {
        e.preventDefault()
        setCopilotOpen(o => !o)
        return
      }
      if (e.key === 'Escape') {
        setCmdOpen(false)
        setCopilotOpen(false)
        return
      }
      // g-then-letter navigation (Linear-style). Ignore when the user is typing
      // or holding a modifier so we don't hijack normal input.
      const target = e.target as HTMLElement | null
      const typing = !!target && (
        target.tagName === 'INPUT' ||
        target.tagName === 'TEXTAREA' ||
        target.tagName === 'SELECT' ||
        target.isContentEditable
      )
      if (typing || e.metaKey || e.ctrlKey || e.altKey) return
      if (e.key === 'g') {
        gPressed = true
        window.clearTimeout(gTimer)
        gTimer = window.setTimeout(() => { gPressed = false }, 800)
        return
      }
      if (gPressed) {
        gPressed = false
        window.clearTimeout(gTimer)
        const GOTO: Record<string, string> = {
          d: '/dashboard',
          e: '/students',
          a: '/alerts',
          p: '/predictions',
          r: '/reports',
        }
        const to = GOTO[e.key.toLowerCase()]
        if (to) {
          e.preventDefault()
          navigate(to)
        }
      }
    }
    window.addEventListener('keydown', onKey)
    return () => { window.removeEventListener('keydown', onKey); window.clearTimeout(gTimer) }
  }, [navigate])

  return (
    <div className="vh-full flex relative overflow-hidden" style={{ background: 'var(--bg)' }}>
      {/* Mobile drawer backdrop */}
      {navOpen && (
        <div
          className="fixed inset-0 z-40 lg:hidden"
          style={{ background: 'rgba(12,10,9,0.42)' }}
          onClick={() => setNavOpen(false)}
        />
      )}
      {/* Sidebar: static on desktop, off-canvas drawer under lg */}
      <div
        className={`fixed inset-y-0 left-0 z-50 transition-transform duration-200 lg:static lg:z-auto lg:translate-x-0 ${
          navOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'
        }`}
      >
        <Sidebar />
      </div>
      <div className={`flex-1 flex flex-col min-w-0 copilot-open-layout ${copilotOpen ? 'copilot-open' : ''}`}>
        <Topbar
          onCommandOpen={() => setCmdOpen(true)}
          onCopilotOpen={() => setCopilotOpen(o => !o)}
          onMenuOpen={() => setNavOpen(true)}
          copilotActive={copilotOpen}
        />
        <div className="flex-1 overflow-y-auto scroll-thin">
          <div className="app-content px-4 sm:px-6 lg:px-8 py-5 lg:py-7 max-w-[1480px] mx-auto">
            <Outlet />
          </div>
        </div>
      </div>
      <CommandPalette open={cmdOpen} onClose={() => setCmdOpen(false)} />
      {copilotActive ? (
        <div className="relative">
          <CopilotSidebar
            agentId="default"
            defaultOpen={false}
            labels={{
              modalHeaderTitle: 'ENIAD Copilot',
              welcomeMessageText: 'Bonjour ! Interrogez les données étudiantes, les scores de risque et le DW en langage naturel.',
            }}
          />
          {copilotOpen && (
            <button
              onClick={() => setCopilotOpen(false)}
              className="sm:hidden fixed z-[10000] top-2 right-2 w-11 h-11 flex items-center justify-center rounded-lg bg-surface border border-border text-text hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-500"
              aria-label="Fermer le panneau Copilot"
            >
              <Icon name="x" size={20} />
            </button>
          )}
        </div>
      ) : copilotOpen ? (
        <div
          className="copilot-panel fixed right-0 top-0 bottom-0 w-80 flex flex-col z-50 drawer"
          style={{ background: 'var(--surface)', borderLeft: '1px solid var(--border)', color: 'var(--text)' }}
        >
          <div className="p-4 flex justify-between items-center" style={{ borderBottom: '1px solid var(--border)' }}>
            <h3 className="font-semibold text-lg">ENIAD Copilot</h3>
            <button
              onClick={() => setCopilotOpen(false)}
              className="btn btn-ghost w-11 h-11 sm:btn-sm sm:w-auto sm:h-auto flex items-center justify-center p-0"
              aria-label="Fermer le panneau Copilot"
            >
              <Icon name="x" size={18} />
            </button>
          </div>
          <div className="flex-1 p-6 flex flex-col items-center justify-center text-center">
            <div className="mb-4" style={{ color: 'var(--accent-500)' }}>
              <Icon name="alert" size={40} />
            </div>
            <p className="font-medium mb-1">Copilot indisponible</p>
            <p className="text-sm" style={{ color: 'var(--text-3)' }}>
              La session de chat sécurisée n'a pas pu être établie. Les fonctionnalités d'intelligence artificielle sont temporairement désactivées.
            </p>
          </div>
        </div>
      ) : null}
    </div>
  )
}
```

---

### Task 3: CSS Overrides in index.css

**Files:**
- Modify: `D:/pfa/plateforme-decisionnelle-pfa/frontend/src/index.css`

- [ ] **Step 1: Add Copilot layout rules**

Append the following responsive CSS overrides to the end of `index.css`:

```css
/* Copilot layout transitions and drawer sizing */
.copilot-open-layout {
  transition: margin-right 0.22s cubic-bezier(0.16, 1, 0.3, 1);
}

@media (min-width: 640px) {
  .copilot-open-layout.copilot-open {
    margin-right: 380px;
  }
  .copilot-panel, [data-copilot-sidebar] {
    width: 380px !important;
    max-width: 90vw !important;
  }
}

@media (max-width: 639px) {
  .copilot-panel, [data-copilot-sidebar] {
    width: 100% !important;
    max-width: 100% !important;
    left: 0 !important;
    right: 0 !important;
    top: 0 !important;
    bottom: 0 !important;
    height: 100% !important;
    z-index: 9999 !important;
  }
}
```

---

### Task 4: Unit Testing for AppShell

**Files:**
- Create: `D:/pfa/plateforme-decisionnelle-pfa/frontend/src/layout/AppShell.test.tsx`

- [ ] **Step 1: Write the unit tests for AppShell**

Create a Vitest test suite that renders `AppShell` with the mock router and Copilot configuration, verifying default states, opening, closing, escaping, and focus recovery.

```tsx
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { AppShell } from './AppShell'
import * as AuthContext from '../context/AuthContext'

// Mock subcomponents
vi.mock('./Sidebar', () => ({
  Sidebar: () => <div data-testid="sidebar">Sidebar</div>,
}))
vi.mock('./CommandPalette', () => ({
  CommandPalette: ({ open, onClose }: { open: boolean; onClose: () => void }) => (
    open ? <div data-testid="command-palette"><button onClick={onClose}>Close Cmd</button></div> : null
  ),
}))

// Mock Copilot v2 components
const mockSetModalOpen = vi.fn()
let mockIsModalOpen = false

vi.mock('@copilotkit/react-core/v2', () => ({
  CopilotSidebar: ({ defaultOpen }: { defaultOpen: boolean }) => (
    <div data-testid="copilot-sidebar" data-default-open={defaultOpen}>
      Copilot Sidebar Content
    </div>
  ),
  useCopilotChatConfiguration: () => ({
    isModalOpen: mockIsModalOpen,
    setModalOpen: mockSetModalOpen,
  }),
}))

describe('AppShell Copilot Integration', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockIsModalOpen = false
    vi.spyOn(AuthContext, 'useAuth').mockReturnValue({
      token: 'fake-token',
      user: { email: 'admin@eniad.ma', role: 'Admin', nom: 'Admin' },
      copilotActive: true,
      login: vi.fn(),
      logout: vi.fn(),
    })
  })

  it('renders closed by default (defaultOpen is false)', () => {
    render(
      <MemoryRouter>
        <AppShell />
      </MemoryRouter>
    )

    const sidebar = screen.getByTestId('copilot-sidebar')
    expect(sidebar).toBeInTheDocument()
    expect(sidebar.getAttribute('data-default-open')).toBe('false')
  })

  it('toggles Copilot sidebar when launcher button is clicked', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter>
        <AppShell />
      </MemoryRouter>
    )

    const launcher = screen.getByRole('button', { name: /copilot/i })
    await user.click(launcher)

    expect(mockSetModalOpen).toHaveBeenCalledWith(true)
  })

  it('closes Copilot when Escape is pressed', async () => {
    const user = userEvent.setup()
    mockIsModalOpen = true

    render(
      <MemoryRouter>
        <AppShell />
      </MemoryRouter>
    )

    await user.keyboard('{Escape}')
    expect(mockSetModalOpen).toHaveBeenCalledWith(false)
  })
})
```

---

### Task 5: Run Verification Commands

- [ ] **Step 1: Run linting**
Run: `npm run lint`
Expected: PASS

- [ ] **Step 2: Run tests**
Run: `npm run test`
Expected: PASS

- [ ] **Step 3: Run production build**
Run: `npm run build`
Expected: PASS
