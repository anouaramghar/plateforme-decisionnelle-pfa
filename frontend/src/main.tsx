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
