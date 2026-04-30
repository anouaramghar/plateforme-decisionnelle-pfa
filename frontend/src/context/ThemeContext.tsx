import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'

export type Theme = 'light' | 'dark'
export type Density = 'compact' | 'comfortable' | 'spacious'
export type Accent = 'amber' | 'indigo' | 'emerald' | 'rose'

const ACCENTS: Record<Accent, Record<string, string>> = {
  amber:   { 50:'#fff7ed', 100:'#ffedd5', 200:'#fed7aa', 300:'#fdba74', 400:'#fb923c', 500:'#f97316', 600:'#ea580c', 700:'#c2410c', 800:'#9a3412', 900:'#7c2d12' },
  indigo:  { 50:'#eef2ff', 100:'#e0e7ff', 200:'#c7d2fe', 300:'#a5b4fc', 400:'#818cf8', 500:'#6366f1', 600:'#4f46e5', 700:'#4338ca', 800:'#3730a3', 900:'#312e81' },
  emerald: { 50:'#ecfdf5', 100:'#d1fae5', 200:'#a7f3d0', 300:'#6ee7b7', 400:'#34d399', 500:'#10b981', 600:'#059669', 700:'#047857', 800:'#065f46', 900:'#064e3b' },
  rose:    { 50:'#fff1f2', 100:'#ffe4e6', 200:'#fecdd3', 300:'#fda4af', 400:'#fb7185', 500:'#f43f5e', 600:'#e11d48', 700:'#be123c', 800:'#9f1239', 900:'#881337' },
}

interface ThemeContextValue {
  theme: Theme
  density: Density
  accent: Accent
  sidebarCollapsed: boolean
  setTheme: (t: Theme) => void
  toggleTheme: () => void
  setDensity: (d: Density) => void
  cycleDensity: () => void
  setAccent: (a: Accent) => void
  setSidebarCollapsed: (c: boolean) => void
  toggleSidebar: () => void
}

const ThemeContext = createContext<ThemeContextValue | null>(null)

function applyToDocument(theme: Theme, density: Density, accent: Accent) {
  const root = document.documentElement
  root.classList.toggle('dark', theme === 'dark')
  root.dataset.density = density
  const palette = ACCENTS[accent]
  Object.entries(palette).forEach(([k, v]) => root.style.setProperty(`--accent-${k}`, v))
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  // Read once from localStorage so the toggle survives reloads. Density and
  // sidebar collapse are user-preference, not security-sensitive — fine to persist.
  const [theme, setThemeState] = useState<Theme>(
    (localStorage.getItem('pfa.theme') as Theme) || 'light',
  )
  const [density, setDensityState] = useState<Density>(
    (localStorage.getItem('pfa.density') as Density) || 'comfortable',
  )
  const [accent, setAccentState] = useState<Accent>(
    (localStorage.getItem('pfa.accent') as Accent) || 'amber',
  )
  const [sidebarCollapsed, setSidebarCollapsed] = useState<boolean>(
    localStorage.getItem('pfa.sidebar') === 'collapsed',
  )

  useEffect(() => {
    applyToDocument(theme, density, accent)
  }, [theme, density, accent])

  const setTheme = useCallback((t: Theme) => {
    setThemeState(t)
    localStorage.setItem('pfa.theme', t)
  }, [])
  const toggleTheme = useCallback(() => {
    setThemeState(prev => {
      const next = prev === 'dark' ? 'light' : 'dark'
      localStorage.setItem('pfa.theme', next)
      return next
    })
  }, [])
  const setDensity = useCallback((d: Density) => {
    setDensityState(d)
    localStorage.setItem('pfa.density', d)
  }, [])
  const cycleDensity = useCallback(() => {
    const order: Density[] = ['compact', 'comfortable', 'spacious']
    setDensityState(prev => {
      const next = order[(order.indexOf(prev) + 1) % order.length]
      localStorage.setItem('pfa.density', next)
      return next
    })
  }, [])
  const setAccent = useCallback((a: Accent) => {
    setAccentState(a)
    localStorage.setItem('pfa.accent', a)
  }, [])
  const updateSidebar = useCallback((c: boolean) => {
    setSidebarCollapsed(c)
    localStorage.setItem('pfa.sidebar', c ? 'collapsed' : 'expanded')
  }, [])
  const toggleSidebar = useCallback(() => {
    updateSidebar(!sidebarCollapsed)
  }, [sidebarCollapsed, updateSidebar])

  return (
    <ThemeContext.Provider
      value={{
        theme,
        density,
        accent,
        sidebarCollapsed,
        setTheme,
        toggleTheme,
        setDensity,
        cycleDensity,
        setAccent,
        setSidebarCollapsed: updateSidebar,
        toggleSidebar,
      }}
    >
      {children}
    </ThemeContext.Provider>
  )
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext)
  if (!ctx) throw new Error('useTheme must be used inside ThemeProvider')
  return ctx
}
