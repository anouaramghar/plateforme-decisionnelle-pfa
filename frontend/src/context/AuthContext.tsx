import { createContext, useContext, useState, useCallback, useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import {
  api,
  setAuthToken,
  setRefreshToken,
  getRefreshToken,
  setOnUnauthorized,
  setOnTokenRefreshed,
  bumpSession,
} from '../services/api'

// The Copilot session cookie lives 15 min server-side; renew well before that
// so the sidebar doesn't silently start returning 401 mid-session.
const COPILOT_RENEW_MS = 10 * 60_000

interface AuthUser {
  email: string
  role: string
  nom: string
}

interface AuthContextValue {
  token: string | null
  user: AuthUser | null
  copilotActive: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  // Token + refresh token + user live in React state only — no storage
  // persistence (XSS hardening). On a hard reload the user re-authenticates;
  // within a session the refresh token is what keeps them logged in past the
  // 24h JWT expiry.
  const [token, setToken] = useState<string | null>(null)
  const [, setRefresh] = useState<string | null>(null)
  const [user, setUser] = useState<AuthUser | null>(null)
  const [copilotActive, setCopilotActive] = useState<boolean>(false)
  const queryClient = useQueryClient()

  // Wire the axios layer to React state.
  useEffect(() => {
    setAuthToken(token)
  }, [token])

  const logout = useCallback(() => {
    // Invalidate any in-flight token refresh so it can't revive this session.
    bumpSession()
    const rt = getRefreshToken()
    if (rt) {
      // Best-effort server-side revocation — don't block the UI on its result.
      api.post('/auth/logout', { refreshToken: rt }).catch(() => {})
    }
    api.delete('/copilot/session', { withCredentials: true }).catch(() => {})
    setToken(null)
    setRefresh(null)
    setUser(null)
    setCopilotActive(false)
    setAuthToken(null)
    setRefreshToken(null)
    queryClient.clear()
  }, [queryClient])

  // Keep the Copilot session cookie alive while Copilot is active. Without this
  // the 15-min cookie expires and the sidebar 401s while the UI still shows it
  // as available.
  useEffect(() => {
    if (!token || !copilotActive) return
    const id = setInterval(() => {
      api.post('/copilot/session', {}, { withCredentials: true })
        .catch(() => setCopilotActive(false))
    }, COPILOT_RENEW_MS)
    return () => clearInterval(id)
  }, [token, copilotActive])

  // Centralize 401 handling: if any request comes back unauthorized AND the
  // refresh attempt also fails, wipe state and let the router redirect.
  useEffect(() => {
    setOnUnauthorized(() => {
      setToken(null)
      setRefresh(null)
      setUser(null)
      setCopilotActive(false)
      queryClient.clear()
      api.delete('/copilot/session', { withCredentials: true }).catch(() => {})
    })
    setOnTokenRefreshed((newToken, newRefresh) => {
      setToken(newToken)
      setRefresh(newRefresh)
    })
    return () => {
      setOnUnauthorized(null)
      setOnTokenRefreshed(null)
    }
  }, [queryClient])

  const login = useCallback(async (email: string, password: string) => {
    // New session: invalidate any in-flight refresh from a prior session.
    bumpSession()
    // Cancel any inflight queries and wipe the cache so the previous user's
    // data cannot bleed through into the new session.
    await queryClient.cancelQueries()
    queryClient.clear()
    const res = await api.post('/auth/login', { email, motDePasse: password })
    const { token: jwt, refreshToken: refresh, email: userEmail, role, nomComplet } = res.data
    setAuthToken(jwt)
    setRefreshToken(refresh)
    setToken(jwt)
    setRefresh(refresh)
    setUser({ email: userEmail, role, nom: nomComplet })

    try {
      await api.post('/copilot/session', {}, { withCredentials: true })
      setCopilotActive(true)
    } catch (err) {
      console.warn('Copilot session creation failed:', err)
      setCopilotActive(false)
    }
  }, [queryClient])

  return (
    <AuthContext.Provider value={{ token, user, copilotActive, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
