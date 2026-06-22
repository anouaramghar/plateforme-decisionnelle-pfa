import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { useTheme } from '../context/ThemeContext'
import { useAuth } from '../context/AuthContext'
import { api } from '../services/api'
import { canCreateStudent, canRunBatchPredictions } from '../auth/roles'

const BREADCRUMBS: Record<string, [string, string]> = {
  '/dashboard':   ['Pilotage', 'Tableau de bord'],
  '/students':    ['Pilotage', 'Étudiants'],
  '/alerts':      ['Pilotage', 'Alertes'],
  '/predictions': ['Pilotage', 'Prédictions ML'],
  '/reports':     ['Pilotage', 'Rapports'],
  '/admin':       ['Système', 'Administration'],
  '/settings':    ['Système', 'Paramètres'],
}

interface TopbarProps {
  onCommandOpen: () => void
  onCopilotOpen: () => void
  copilotActive?: boolean
}

interface NouveauItem {
  label: string
  icon: Parameters<typeof Icon>[0]['name']
  action: () => void
  adminOnly?: boolean
}

export function Topbar({ onCommandOpen, onCopilotOpen, copilotActive }: TopbarProps) {
  const { pathname } = useLocation()
  const navigate = useNavigate()
  const { theme, toggleTheme } = useTheme()
  const { user, token } = useAuth()
  const mayAccessAlerts = user?.role === 'Admin' || user?.role === 'Responsable'
  const breadcrumb = BREADCRUMBS[pathname] ?? ['Pilotage', 'Page']

  const [nouveauOpen, setNouveauOpen]   = useState(false)
  const [batchLoading, setBatchLoading] = useState(false)
  const [syncLoading, setSyncLoading]   = useState(false)
  const nouveauRef = useRef<HTMLDivElement>(null)

  // Unresolved alert count for bell badge
  const { data: alertCount = 0 } = useQuery({
    queryKey: ['topbar', 'alertes-unresolved'],
    queryFn: async () => {
      const res = await api.get<{ items: { resolue: boolean }[]; total: number }>(
        '/alertes?pageSize=200'
      )
      return res.data.items.filter(a => !a.resolue).length
    },
    enabled: !!token && mayAccessAlerts,
    refetchInterval: 30_000,
    staleTime: 25_000,
  })

  // Close dropdown on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (nouveauRef.current && !nouveauRef.current.contains(e.target as Node))
        setNouveauOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const runBatch = async () => {
    if (batchLoading) return
    setBatchLoading(true)
    try { await api.post('/predictions/batch', {}) }
    finally { setBatchLoading(false); setNouveauOpen(false) }
  }

  const runSync = async () => {
    if (syncLoading) return
    setSyncLoading(true)
    try { await api.post('/admin/sync-dw') }
    finally { setSyncLoading(false); setNouveauOpen(false) }
  }

  const isAdmin  = user?.role === 'Admin'
  const canAddStudent = canCreateStudent(user?.role)
  const canBatch      = canRunBatchPredictions(user?.role)

  const NOUVEAU_ITEMS: NouveauItem[] = [
    ...(canAddStudent ? [{
      label: 'Ajouter un étudiant',
      icon: 'students' as Parameters<typeof Icon>[0]['name'],
      action: () => { navigate('/admin', { state: { tab: 'etudiants' } }); setNouveauOpen(false) },
    }] : []),
    ...(canBatch ? [{
      label: batchLoading ? 'Calcul en cours…' : 'Prédictions batch',
      icon: 'brain' as Parameters<typeof Icon>[0]['name'],
      action: runBatch,
    }] : []),
    {
      label: syncLoading ? 'Sync en cours…' : 'Synchroniser DW',
      icon: 'refresh' as Parameters<typeof Icon>[0]['name'],
      action: runSync,
      adminOnly: true,
    },
  ]

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
      {/* Breadcrumb */}
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
        {/* Search */}
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

        {/* Bell → /alerts */}
        {mayAccessAlerts && <button
          className="btn btn-sm btn-ghost relative"
          title={`Alertes${alertCount > 0 ? ` (${alertCount} non résolues)` : ''}`}
          onClick={() => navigate('/alerts')}
        >
          <Icon name="bell" size={14} />
          {alertCount > 0 && (
            <span
              style={{
                position: 'absolute',
                top: 4,
                right: 4,
                minWidth: 14,
                height: 14,
                borderRadius: 999,
                background: 'var(--accent-500)',
                boxShadow: '0 0 0 2px var(--surface)',
                fontSize: 9,
                fontWeight: 600,
                color: '#fff',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                padding: '0 2px',
              }}
            >
              {alertCount > 9 ? '9+' : alertCount}
            </span>
          )}
        </button>}

        {/* Theme */}
        <button
          onClick={toggleTheme}
          className="btn btn-sm btn-ghost"
          title={theme === 'dark' ? 'Passer en mode clair' : 'Passer en mode sombre'}
        >
          <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={15} strokeWidth={1.8} />
        </button>

        <div style={{ width: 1, height: 20, background: 'var(--border)', margin: '0 4px' }} />

        {/* ENIAD Copilot toggle */}
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

        {/* + Nouveau dropdown */}
        <div ref={nouveauRef} style={{ position: 'relative' }}>
          <button
            className="btn btn-sm btn-accent"
            onClick={() => setNouveauOpen(o => !o)}
          >
            <Icon name="plus" size={13} strokeWidth={2.4} />
            Nouveau
          </button>

          {nouveauOpen && (
            <div
              style={{
                position: 'absolute',
                top: 'calc(100% + 6px)',
                right: 0,
                minWidth: 220,
                background: 'var(--surface)',
                border: '1px solid var(--border)',
                borderRadius: 10,
                boxShadow: '0 8px 24px rgba(0,0,0,.22)',
                zIndex: 50,
                overflow: 'hidden',
                padding: '4px 0',
              }}
            >
              {NOUVEAU_ITEMS.filter(it => !it.adminOnly || isAdmin).map(it => (
                <button
                  key={it.label}
                  onClick={it.action}
                  disabled={batchLoading || syncLoading}
                  className="w-full flex items-center gap-2.5 px-3.5 py-2.5 text-[12.5px] text-left transition"
                  style={{ color: 'var(--text)', background: 'transparent' }}
                  onMouseEnter={e => (e.currentTarget.style.background = 'var(--surface-2)')}
                  onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}
                >
                  <Icon name={it.icon} size={13} style={{ color: 'var(--text-3)', flexShrink: 0 }} />
                  {it.label}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>
    </header>
  )
}
