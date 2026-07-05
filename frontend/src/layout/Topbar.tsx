import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { Icon, type IconName } from '../components/ui/Icon'
import { useTheme } from '../context/ThemeContext'
import { useAuth } from '../context/AuthContext'
import { api } from '../services/api'
import { useUnresolvedAlertCount } from '../services/useUnresolvedAlertCount'
import { canCreateStudent, canRunBatchPredictions, canUseCopilot } from '../auth/roles'

const BREADCRUMBS: Record<string, [string, string]> = {
  '/dashboard':   ['Pilotage', 'Tableau de bord'],
  '/students':    ['Pilotage', 'Étudiants'],
  '/alerts':      ['Pilotage', 'Alertes'],
  '/cases':       ['Pilotage', 'Interventions'],
  '/predictions': ['Pilotage', 'Prédictions ML'],
  '/reports':     ['Pilotage', 'Rapports'],
  '/admin':       ['Système', 'Administration'],
  '/settings':    ['Système', 'Paramètres'],
  '/enseignant':  ['Enseignement', 'Mon espace'],
  '/responsable': ['Pilotage', 'Mon espace'],
}

export function breadcrumbFor(pathname: string): [string, string] {
  if (/^\/students\/[^/]+$/.test(pathname)) return ['Étudiants', 'Fiche étudiant']
  if (/^\/cases\/[^/]+$/.test(pathname)) return ['Interventions', 'Détail du cas']
  return BREADCRUMBS[pathname] ?? ['Pilotage', 'Page introuvable']
}

interface TopbarProps {
  onCommandOpen: () => void
  onCopilotOpen: () => void
  onMenuOpen?: () => void
  navOpen?: boolean
  copilotActive?: boolean
}

interface NouveauItem {
  label: string
  icon: IconName
  action: () => void
  adminOnly?: boolean
}

export function Topbar({ onCommandOpen, onCopilotOpen, onMenuOpen, navOpen = false, copilotActive }: TopbarProps) {
  const { pathname } = useLocation()
  const navigate = useNavigate()
  const { theme, toggleTheme } = useTheme()
  const { user, token } = useAuth()
  const breadcrumb = breadcrumbFor(pathname)
  const mayAccessAlerts = user?.role === 'Admin' || user?.role === 'Responsable'
  const mayUseCopilot = canUseCopilot(user?.role)
  const [nouveauOpen, setNouveauOpen] = useState(false)
  const [batchLoading, setBatchLoading] = useState(false)
  const [syncLoading, setSyncLoading] = useState(false)
  const nouveauRef = useRef<HTMLDivElement>(null)
  const { data: alertCount = 0 } = useUnresolvedAlertCount(!!token && mayAccessAlerts)

  useEffect(() => {
    const closeOutside = (event: MouseEvent) => {
      if (nouveauRef.current && !nouveauRef.current.contains(event.target as Node)) setNouveauOpen(false)
    }
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setNouveauOpen(false)
    }
    document.addEventListener('mousedown', closeOutside)
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.removeEventListener('mousedown', closeOutside)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [])

  const runBatch = async () => {
    if (batchLoading) return
    setBatchLoading(true)
    try {
      await api.post('/predictions/batch', {})
    } finally {
      setBatchLoading(false)
      setNouveauOpen(false)
    }
  }

  const runSync = async () => {
    if (syncLoading) return
    setSyncLoading(true)
    try {
      await api.post('/admin/sync-dw')
    } finally {
      setSyncLoading(false)
      setNouveauOpen(false)
    }
  }

  const isAdmin = user?.role === 'Admin'
  const items: NouveauItem[] = [
    ...(canCreateStudent(user?.role) ? [{
      label: 'Ajouter un étudiant',
      icon: 'students' as IconName,
      action: () => {
        navigate('/admin', { state: { tab: 'etudiants' } })
        setNouveauOpen(false)
      },
    }] : []),
    ...(canRunBatchPredictions(user?.role) ? [{
      label: batchLoading ? 'Calcul en cours…' : 'Prédictions batch',
      icon: 'brain' as IconName,
      action: runBatch,
    }] : []),
    {
      label: syncLoading ? 'Sync en cours…' : 'Synchroniser DW',
      icon: 'refresh',
      action: runSync,
      adminOnly: true,
    },
  ]
  const visibleItems = items.filter(item => !item.adminOnly || isAdmin)

  return (
    <header
      className="flex shrink-0 items-center justify-between gap-2 px-3 sm:px-6"
      style={{
        height: 60,
        background: 'color-mix(in oklch, var(--surface) 88%, transparent)',
        backdropFilter: 'saturate(150%) blur(10px)',
        borderBottom: '1px solid var(--border)',
        boxShadow: '0 1px 10px rgba(8,17,14,.04)',
        position: 'sticky',
        top: 0,
        zIndex: 30,
      }}
    >
      <div className="flex min-w-0 items-center gap-2 sm:gap-3">
        <button
          type="button"
          onClick={onMenuOpen}
          className="btn btn-sm btn-ghost shrink-0 lg:hidden"
          aria-label="Ouvrir le menu de navigation"
          aria-controls="primary-navigation"
          aria-expanded={navOpen}
        >
          <Icon name="menu" size={16} />
        </button>
        <nav aria-label="Fil d'Ariane" className="flex min-w-0 items-center gap-2 text-[12.5px]">
          {breadcrumb.map((crumb, index) => (
            <span key={`${crumb}-${index}`} className={`items-center gap-2 ${index === 0 ? 'hidden sm:flex' : 'flex min-w-0'}`}>
              <span
                className={`${index === breadcrumb.length - 1 ? 'font-medium' : ''} truncate`}
                style={{ color: index === breadcrumb.length - 1 ? 'var(--text)' : 'var(--text-3)' }}
                aria-current={index === breadcrumb.length - 1 ? 'page' : undefined}
              >
                {crumb}
              </span>
              {index < breadcrumb.length - 1 && (
                <Icon name="chevRight" size={11} style={{ color: 'var(--text-4)' }} />
              )}
            </span>
          ))}
        </nav>
      </div>

      <div className="flex shrink-0 items-center gap-1 sm:gap-1.5">
        <button
          type="button"
          onClick={onCommandOpen}
          className="flex h-8 items-center gap-2.5 rounded-lg px-2 transition sm:min-w-[190px] sm:pl-2.5 lg:min-w-[260px]"
          style={{
            background: 'var(--surface)',
            border: '1px solid var(--border-2)',
            boxShadow: '0 1px 2px rgba(8,17,14,.03)',
          }}
          aria-label="Rechercher dans la plateforme"
        >
          <Icon name="search" size={13} style={{ color: 'var(--text-3)' }} />
          <span className="hidden flex-1 text-left text-[12.5px] sm:inline" style={{ color: 'var(--text-3)' }}>
            Rechercher…
          </span>
          <kbd className="hidden lg:inline">⌘K</kbd>
        </button>

        <div className="hidden sm:block" style={{ width: 1, height: 20, background: 'var(--border)', margin: '0 4px' }} />

        {mayAccessAlerts && (
          <button
            type="button"
            className="btn btn-sm btn-ghost relative"
            onClick={() => navigate('/alerts')}
            aria-label={`Alertes${alertCount > 0 ? `, ${alertCount} non résolues` : ''}`}
          >
            <Icon name="bell" size={14} />
            {alertCount > 0 && (
              <span
                className="absolute right-1 top-1 flex h-3.5 min-w-3.5 items-center justify-center rounded-full px-0.5 text-[9px] font-semibold text-white"
                style={{ background: 'var(--accent-500)', boxShadow: '0 0 0 2px var(--surface)' }}
                aria-hidden="true"
              >
                {alertCount > 9 ? '9+' : alertCount}
              </span>
            )}
          </button>
        )}

        <button
          type="button"
          onClick={toggleTheme}
          className="btn btn-sm btn-ghost hidden sm:inline-flex"
          aria-label={theme === 'dark' ? 'Passer en mode clair' : 'Passer en mode sombre'}
        >
          <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={15} strokeWidth={1.8} />
        </button>

        {mayUseCopilot && (
          <button
            type="button"
            onClick={onCopilotOpen}
            className="btn btn-sm"
            aria-label={copilotActive ? 'Fermer ENIAD Copilot' : 'Ouvrir ENIAD Copilot'}
            aria-expanded={copilotActive}
            style={copilotActive ? {
              background: 'color-mix(in oklch, var(--accent-500) 12%, transparent)',
              borderColor: 'color-mix(in oklch, var(--accent-500) 30%, transparent)',
              color: 'var(--accent-600)',
            } : undefined}
          >
            <Icon name="brain" size={13} />
            <span className="hidden md:inline">Copilot</span>
          </button>
        )}

        {visibleItems.length > 0 && (
          <div ref={nouveauRef} className="relative">
            <button
              type="button"
              className="btn btn-sm btn-accent"
              onClick={() => setNouveauOpen(open => !open)}
              aria-expanded={nouveauOpen}
              aria-haspopup="menu"
              aria-label="Ouvrir le menu Nouveau"
            >
              <Icon name="plus" size={13} strokeWidth={2.4} />
              <span className="hidden md:inline">Nouveau</span>
            </button>

            {nouveauOpen && (
              <div
                role="menu"
                className="absolute right-0 top-[calc(100%+6px)] z-50 min-w-[220px] overflow-hidden rounded-[10px] py-1"
                style={{ background: 'var(--surface)', border: '1px solid var(--border)', boxShadow: '0 8px 24px rgba(0,0,0,.22)' }}
              >
                {visibleItems.map(item => (
                  <button
                    type="button"
                    role="menuitem"
                    key={item.label}
                    onClick={item.action}
                    disabled={batchLoading || syncLoading}
                    className="flex w-full items-center gap-2.5 px-3.5 py-2.5 text-left text-[12.5px] transition hover:bg-[var(--surface-2)]"
                    style={{ color: 'var(--text)' }}
                  >
                    <Icon name={item.icon} size={13} style={{ color: 'var(--text-3)', flexShrink: 0 }} />
                    {item.label}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </header>
  )
}
