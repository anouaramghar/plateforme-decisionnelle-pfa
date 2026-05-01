import { useNavigate } from 'react-router-dom'
import { Icon, type IconName } from '../components/ui/Icon'

interface PaletteItem {
  label: string
  icon: IconName
  to?: string
  kbd?: string
  sub?: string
}

interface PaletteGroup {
  name: string
  items: PaletteItem[]
}

const GROUPS: PaletteGroup[] = [
  {
    name: 'Pages',
    items: [
      { label: 'Tableau de bord', icon: 'dashboard', to: '/dashboard',   kbd: 'G D' },
      { label: 'Étudiants',       icon: 'students',  to: '/students',    kbd: 'G E' },
      { label: 'Alertes',         icon: 'bell',      to: '/alerts',      kbd: 'G A' },
      { label: 'Prédictions ML',  icon: 'brain',     to: '/predictions', kbd: 'G P' },
      { label: 'Rapports',        icon: 'doc',       to: '/reports',     kbd: 'G R' },
    ],
  },
  {
    name: 'Actions',
    items: [
      { label: 'Lancer une prédiction batch…',          icon: 'spark' },
      { label: 'Synchroniser le Data Warehouse',         icon: 'refresh' },
      { label: 'Exporter le rapport courant en PDF',     icon: 'download' },
    ],
  },
  {
    name: 'Étudiants récents',
    items: [
      { label: 'Mehdi Alaoui — GI 3A',  icon: 'user', sub: 'Score risque 78%' },
      { label: 'Salma Bennani — GI 2A', icon: 'user', sub: 'Score risque 12%' },
    ],
  },
]

interface CommandPaletteProps {
  open: boolean
  onClose: () => void
}

export function CommandPalette({ open, onClose }: CommandPaletteProps) {
  const navigate = useNavigate()
  if (!open) return null

  return (
    <div
      onClick={onClose}
      className="fixed inset-0 z-50 flex items-start justify-center pt-[10vh]"
      style={{ background: 'rgba(12,10,9,0.42)', backdropFilter: 'blur(2px)' }}
    >
      <div
        onClick={e => e.stopPropagation()}
        className="cmd-shadow rounded-xl w-full max-w-[560px] overflow-hidden"
        style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}
      >
        <div className="flex items-center gap-2.5 px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
          <Icon name="search" size={15} style={{ color: 'var(--text-3)' }} />
          <input
            autoFocus
            placeholder="Rechercher étudiant, alerte, action…"
            className="flex-1 bg-transparent outline-none text-[13.5px]"
            style={{ color: 'var(--text)' }}
          />
          <kbd>esc</kbd>
        </div>
        <div className="max-h-[440px] overflow-y-auto scroll-thin py-1.5">
          {GROUPS.map(g => (
            <div key={g.name} className="mb-1">
              <div
                className="px-4 py-1.5 text-[10.5px] uppercase tracking-wider font-medium"
                style={{ color: 'var(--text-3)', letterSpacing: '0.08em' }}
              >
                {g.name}
              </div>
              {g.items.map(it => (
                <div
                  key={it.label}
                  onClick={() => {
                    if (it.to) {
                      navigate(it.to)
                      onClose()
                    }
                  }}
                  className="flex items-center gap-2.5 px-4 py-2 mx-1.5 rounded-md cursor-pointer hover:bg-[var(--surface-2)]"
                  style={{ transition: 'background .1s' }}
                >
                  <Icon name={it.icon} size={14} style={{ color: 'var(--text-3)' }} />
                  <span className="flex-1 text-[12.5px]">{it.label}</span>
                  {it.sub && <span className="cap">{it.sub}</span>}
                  {it.kbd && <kbd>{it.kbd}</kbd>}
                </div>
              ))}
            </div>
          ))}
        </div>
        <div
          className="px-4 py-2 flex items-center justify-between text-[11px]"
          style={{ background: 'var(--surface-2)', borderTop: '1px solid var(--border)', color: 'var(--text-3)' }}
        >
          <div className="flex items-center gap-3">
            <span className="flex items-center gap-1">
              <kbd>↵</kbd> ouvrir
            </span>
            <span className="flex items-center gap-1">
              <kbd>↑</kbd>
              <kbd>↓</kbd> naviguer
            </span>
          </div>
          <span>Plateforme PFA · v0.4</span>
        </div>
      </div>
    </div>
  )
}
