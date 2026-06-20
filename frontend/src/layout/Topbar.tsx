import { useLocation } from 'react-router-dom'
import { Icon } from '../components/ui/Icon'
import { useTheme } from '../context/ThemeContext'

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
}

export function Topbar({ onCommandOpen }: TopbarProps) {
  const { pathname } = useLocation()
  const { theme, toggleTheme, density, cycleDensity } = useTheme()
  const breadcrumb = BREADCRUMBS[pathname] ?? ['Pilotage', 'Page']

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

        <button className="btn btn-sm btn-ghost relative" title="Notifications">
          <Icon name="bell" size={14} />
          <span
            style={{
              position: 'absolute',
              top: 6,
              right: 6,
              width: 6,
              height: 6,
              borderRadius: 999,
              background: 'var(--accent-500)',
              boxShadow: '0 0 0 2px var(--surface)',
            }}
          />
        </button>

        <button onClick={cycleDensity} className="btn btn-sm btn-ghost" title={`Densité : ${density}`}>
          <Icon name="filter" size={14} />
        </button>

        <button onClick={toggleTheme} className="btn btn-sm btn-ghost" title="Thème" style={{ fontSize: 14 }}>
          {theme === 'dark' ? '☀' : '☾'}
        </button>

        <div style={{ width: 1, height: 20, background: 'var(--border)', margin: '0 4px' }} />

        <button className="btn btn-sm btn-accent">
          <Icon name="plus" size={13} strokeWidth={2.4} />
          Nouveau
        </button>
      </div>
    </header>
  )
}
