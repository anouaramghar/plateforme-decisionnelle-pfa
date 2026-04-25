import { createContext, useContext, useState, useCallback, useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, setAuthToken, setOnUnauthorized } from '../services/api'

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
  // Token + user live in React state only — no storage persistence (XSS hardening).
  const [token, setToken] = useState<string | null>(null)
  const [user, setUser] = useState<AuthUser | null>(null)
  const queryClient = useQueryClient()

  // Wire the axios layer to React state. Updates whenever the token changes
  // so concurrent requests see the latest value.
  useEffect(() => {
    setAuthToken(token)
  }, [token])

  const logout = useCallback(() => {
    setToken(null)
    setUser(null)
    setAuthToken(null)
    queryClient.clear()
  }, [queryClient])

  // Centralize 401 handling: if any request comes back unauthorized,
  // wipe state and let the router redirect to /login.
  useEffect(() => {
    setOnUnauthorized(() => {
      setToken(null)
      setUser(null)
      queryClient.clear()
    })
    return () => setOnUnauthorized(null)
  }, [queryClient])

  const login = useCallback(async (email: string, password: string) => {
    const res = await api.post('/auth/login', { email, motDePasse: password })
    const { token: jwt, email: userEmail, role, nomComplet } = res.data
    setAuthToken(jwt)
    setToken(jwt)
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
