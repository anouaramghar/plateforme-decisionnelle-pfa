import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CopilotKit } from '@copilotkit/react-core/v2'
// CopilotKit v2 styles are loaded as a static asset in index.html (Tailwind-v4
// build, incompatible with this project's Tailwind-v3 PostCSS pipeline).
import App from './App'
import { AuthProvider, useAuth } from './context/AuthContext'
import { ThemeProvider } from './context/ThemeContext'
import { FiliereProvider } from './context/FiliereContext'
import './index.css'

const copilotRuntimeUrl =
  (import.meta.env.VITE_COPILOTKIT_URL as string | undefined) ??
  (import.meta.env.DEV ? 'http://localhost:4000/api/copilotkit' : '/api/copilotkit')

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
      // Remount when auth becomes available so the runtime connection is
      // (re)established WITH the Authorization header. The token lives in memory
      // only, so at first app mount it's null; without this key CopilotKit would
      // connect once unauthenticated and never re-send the header → 401.
      key={token ? 'auth' : 'anon'}
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
          <FiliereProvider>
            <CopilotBridge>
              <App />
            </CopilotBridge>
          </FiliereProvider>
        </AuthProvider>
      </ThemeProvider>
    </QueryClientProvider>
  </StrictMode>
)
