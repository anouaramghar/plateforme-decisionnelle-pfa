import { createContext, useContext, useState, useCallback } from 'react'
import { api } from '../services/api'

interface AuthContextValue {
  token: string | null
  user: { email: string; role: string; nom: string } | null
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(
    () => sessionStorage.getItem('token')
  )
  const [user, setUser] = useState<AuthContextValue['user']>(() => {
    const raw = sessionStorage.getItem('user')
    return raw ? JSON.parse(raw) : null
  })

  const login = useCallback(async (email: string, password: string) => {
    const res = await api.post('/auth/login', { email, motDePasse: password })
    const { token: jwt, email: userEmail, role, nomComplet } = res.data
    sessionStorage.setItem('token', jwt)
    sessionStorage.setItem('user', JSON.stringify({ email: userEmail, role, nom: nomComplet }))
    setToken(jwt)
    setUser({ email: userEmail, role, nom: nomComplet })
  }, [])

  const logout = useCallback(() => {
    sessionStorage.removeItem('token')
    sessionStorage.removeItem('user')
    setToken(null)
    setUser(null)
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
