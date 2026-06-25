import { StrictMode, useMemo } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CopilotKit } from '@copilotkit/react-core/v2'
// CopilotKit v2 styles are loaded as a static asset in index.html (Tailwind-v4
// build, incompatible with this project's Tailwind-v3 PostCSS pipeline).
import App from './App'
import { AuthProvider, useAuth } from './context/AuthContext'
import { ThemeProvider } from './context/ThemeContext'
import { FiliereProvider } from './context/FiliereContext'
import { ErrorBoundary } from './components/ErrorBoundary'
import './index.css'

// Always same-origin: the runtime authenticates via the httpOnly
// `pfa_copilot_session` cookie, which the browser only sends on same-origin
// requests. In dev, Vite proxies /api/copilotkit → the runtime (see
// vite.config.ts); in prod, nginx does. A cross-origin localhost:4000 URL would
// drop the cookie and 401 every request.
const copilotRuntimeUrl =
  (import.meta.env.VITE_COPILOTKIT_URL as string | undefined) ?? '/api/copilotkit'

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
  // Memoize so CopilotKit doesn't see a new headers object on every AuthContext
  // re-render (e.g. when copilotActive toggles). Without this, CopilotKit would
  // reconnect to the runtime on every auth state change → zigzag animations.
  const headers = useMemo(
    () => (token ? { Authorization: `Bearer ${token}` } : {}) as Record<string, string>,
    [token],
  )
  return (
    <CopilotKit
      // Remount when auth becomes available so the runtime connection is
      // (re)established WITH the Authorization header. The token lives in memory
      // only, so at first app mount it's null; without this key CopilotKit would
      // connect once unauthenticated and never re-send the header → 401.
      key={token ? 'auth' : 'anon'}
      runtimeUrl={copilotRuntimeUrl}
      useSingleEndpoint
      headers={headers}
      showDevConsole={import.meta.env.DEV}
    >
      {children}
    </CopilotKit>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider>
          <AuthProvider>
            <FiliereProvider>
              <CopilotBridge>
                <App />
              </CopilotBridge>
            </FiliereProvider>
          </AuthProvider>
        </ThemeProvider>
      </QueryClientProvider>
    </ErrorBoundary>
  </StrictMode>
)
