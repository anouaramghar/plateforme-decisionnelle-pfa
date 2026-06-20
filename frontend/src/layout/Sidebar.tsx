import { NavLink } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Icon, type IconName } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { useAuth } from '../context/AuthContext'
import { useTheme } from '../context/ThemeContext'
import { api } from '../services/api'

interface NavEntry {
  to: string
  label: string
  icon: IconName
  badge?: number
  badgeTone?: 'bad' | 'warn' | 'neutral'
}

interface PaginatedResult<T> { items: T[]; total: number }
interface AlerteRow { resolue: boolean }
interface EtudiantRow { id: number }

const SECONDARY: NavEntry[] = [
  { to: '/admin',    label: 'Administration', icon: 'database' },
  { to: '/settings', label: 'Paramètres',     icon: 'settings' },
]

export function Sidebar() {
  const { sidebarCollapsed, toggleSidebar } = useTheme()
  const { user, token } = useAuth()
  const W = sidebarCollapsed ? 64 : 232

  // Live badge counts. Gated on `token` so we don't fire unauthenticated calls
  // before login. Cheap polling — same cadence the Alerts page already uses.
  const { data: alertesData } = useQuery({
    queryKey: ['sidebar', 'alertes-unresolved'],
    queryFn: async () => {
      const res = await api.get<PaginatedResult<AlerteRow>>('/alertes?pageSize=200')
      return res.data.items.filter(a => !a.resolue).length
    },
    enabled: !!token,
    refetchInterval: 30_000,
    staleTime: 25_000,
  })
  const { data: etudiantsCount } = useQuery({
    queryKey: ['sidebar', 'etudiants-count'],
    queryFn: async () => {
      const res = await api.get<EtudiantRow[]>('/etudiants/with-stats')
      return res.data.length
    },
    enabled: !!token,
    refetchInterval: 5 * 60_000,
    staleTime: 4 * 60_000,
  })

  const PRIMARY: NavEntry[] = [
    { to: '/dashboard',   label: 'Tableau de bord', icon: 'dashboard' },
    { to: '/students',    label: 'Étudiants',       icon: 'students',   badge: etudiantsCount },
    { to: '/alerts',      label: 'Alertes',         icon: 'bell',       badge: alertesData, badgeTone: 'bad' },
    { to: '/predictions', label: 'Prédictions ML',  icon: 'brain' },
    { to: '/reports',     label: 'Rapports',        icon: 'doc' },
  ]

  // Best-effort display name; fall back to the email's local part if the
  // backend hasn't returned a NomComplet (e.g. legacy seed accounts).
  const displayName = user?.nom?.trim() || user?.email?.split('@')[0] || 'Utilisateur'
  const role = user?.role ? `Responsable · ${user.role}` : 'Plateforme PFA'
  const filiereCtx = 'GI'

  return (
    <aside
      style={{
        width: W,
        flexShrink: 0,
        background: 'var(--side-bg)',
        color: 'var(--side-text)',
        borderRight: '1px solid var(--side-border)',
        display: 'flex',
        flexDirection: 'column',
        transition: 'width .18s ease',
      }}
    >
      {/* logo */}
      <div
        className="px-3 pt-3 pb-3 flex items-center gap-2.5"
        style={{ height: 56, borderBottom: '1px solid var(--side-border)' }}
      >
        <div
          style={{
            width: 30,
            height: 30,
            borderRadius: 8,
            background: 'linear-gradient(135deg, var(--accent-400), var(--accent-700))',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#fff',
            fontWeight: 600,
            fontSize: 14,
            letterSpacing: '-0.03em',
            flexShrink: 0,
            boxShadow: 'inset 0 1px 0 rgba(255,255,255,.22), 0 1px 2px rgba(0,0,0,.3)',
          }}
        >
          É
        </div>
        {!sidebarCollapsed && (
          <div className="flex-1 overflow-hidden">
            <div className="text-[13px] font-semibold leading-none tracking-tight" style={{ color: '#fff' }}>
              ENIAD
            </div>
            <div
              className="text-[10.5px] mt-1.5 font-mono"
              style={{ color: 'var(--side-text-3)', letterSpacing: '0.02em' }}
            >
              plateforme · décisionnelle
            </div>
          </div>
        )}
      </div>

      {/* filiere context */}
      {!sidebarCollapsed && (
        <div className="px-3 py-3" style={{ borderBottom: '1px solid var(--side-border)' }}>
          <div
            className="text-[10px] uppercase tracking-wider mb-1.5"
            style={{ color: 'var(--side-text-3)', fontWeight: 500, letterSpacing: '0.08em' }}
          >
            Périmètre
          </div>
          <button
            className="w-full flex items-center gap-2 px-2 py-1.5 rounded-md text-left text-[12.5px] transition"
            style={{
              background: 'rgba(255,255,255,0.04)',
              color: 'var(--side-text)',
              border: '1px solid rgba(255,255,255,0.06)',
            }}
          >
            <span
              style={{
                width: 18,
                height: 18,
                borderRadius: 4,
                background: 'var(--accent-500)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: 9.5,
                fontWeight: 700,
                color: '#fff',
              }}
            >
              {filiereCtx}
            </span>
            <span className="flex-1 truncate">Filière {filiereCtx}</span>
            <Icon name="chevDown" size={13} className="opacity-60" />
          </button>
        </div>
      )}

      <nav className="flex-1 overflow-y-auto scroll-thin px-3 py-3">
        {!sidebarCollapsed && <div className="nav-section-title">Pilotage</div>}
        {PRIMARY.map(it => (
          <NavLink
            key={it.to}
            to={it.to}
            className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}
            title={sidebarCollapsed ? it.label : undefined}
          >
            <Icon name={it.icon} size={15} />
            {!sidebarCollapsed && <span className="flex-1 truncate">{it.label}</span>}
            {!sidebarCollapsed && it.badge != null && (
              <span
                className={`pill pill-${it.badgeTone ?? 'neutral'}`}
                style={{ fontSize: 10.5, padding: '1px 6px' }}
              >
                {it.badge}
              </span>
            )}
          </NavLink>
        ))}

        {!sidebarCollapsed && <div className="nav-section-title">Système</div>}
        {SECONDARY.filter(it => it.to !== '/admin' || user?.role === 'Admin').map(it => (
          <NavLink
            key={it.to}
            to={it.to}
            className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}
            title={sidebarCollapsed ? it.label : undefined}
          >
            <Icon name={it.icon} size={15} />
            {!sidebarCollapsed && <span className="flex-1 truncate">{it.label}</span>}
          </NavLink>
        ))}
      </nav>

      <div className="p-2.5" style={{ borderTop: '1px solid var(--side-border)' }}>
        {!sidebarCollapsed ? (
          <div
            className="flex items-center gap-2.5 px-1.5 py-1.5 rounded-md cursor-pointer"
            style={{ transition: 'background .12s' }}
          >
            <Avatar name={displayName} size={26} />
            <div className="flex-1 overflow-hidden">
              <div className="text-[12px] font-medium truncate" style={{ color: '#fff' }}>
                {displayName}
              </div>
              <div className="text-[10.5px] truncate" style={{ color: 'var(--side-text-3)' }}>
                {role}
              </div>
            </div>
            <Icon name="more" size={14} style={{ color: 'var(--side-text-3)' }} />
          </div>
        ) : (
          <div className="flex justify-center">
            <Avatar name={displayName} size={28} />
          </div>
        )}
        <button
          onClick={toggleSidebar}
          className="mt-2 w-full flex items-center justify-center gap-1.5 py-1.5 rounded-md text-[11px]"
          style={{ color: 'var(--side-text-3)', background: 'transparent' }}
          onMouseEnter={e => (e.currentTarget.style.background = 'rgba(255,255,255,0.04)')}
          onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}
        >
          <Icon name={sidebarCollapsed ? 'expand' : 'collapse'} size={13} />
          {!sidebarCollapsed && <span>Replier</span>}
        </button>
      </div>
    </aside>
  )
}
