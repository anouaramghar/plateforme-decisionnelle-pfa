import { createContext, useContext, useState, useCallback, useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import {
  api,
  setAuthToken,
  setRefreshToken,
  setOnUnauthorized,
  setOnTokenRefreshed,
} from '../services/api'

interface AuthUser {
  email: string
  role: string
  nom: string
}

interface AuthContextValue {
  token: string | null
  user: AuthUser | null
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
  const queryClient = useQueryClient()

  // Wire the axios layer to React state.
  useEffect(() => {
    setAuthToken(token)
  }, [token])

  const logout = useCallback(() => {
    setToken(null)
    setRefresh(null)
    setUser(null)
    setAuthToken(null)
    setRefreshToken(null)
    queryClient.clear()
  }, [queryClient])

  // Centralize 401 handling: if any request comes back unauthorized AND the
  // refresh attempt also fails, wipe state and let the router redirect.
  useEffect(() => {
    setOnUnauthorized(() => {
      setToken(null)
      setRefresh(null)
      setUser(null)
      queryClient.clear()
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
    const res = await api.post('/auth/login', { email, motDePasse: password })
    const { token: jwt, refreshToken: refresh, email: userEmail, role, nomComplet } = res.data
    setAuthToken(jwt)
    setRefreshToken(refresh)
    setToken(jwt)
    setRefresh(refresh)
    setUser({ email: userEmail, role, nom: nomComplet })
  }, [])

  return (
    <AuthContext.Provider value={{ token, user, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
