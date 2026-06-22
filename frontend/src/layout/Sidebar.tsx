import { useRef, useState, useEffect } from 'react'
import { NavLink } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Icon, type IconName } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { useAuth } from '../context/AuthContext'
import { useTheme } from '../context/ThemeContext'
import { useFiliere } from '../context/FiliereContext'
import { api } from '../services/api'
import { useUnresolvedAlertCount } from '../services/useUnresolvedAlertCount'

const FILIERES = ['TOUS', 'TCP', 'GI', 'IA', 'ROC', 'IRSI']

interface NavEntry {
  to: string
  label: string
  icon: IconName
  badge?: number
  badgeTone?: 'bad' | 'warn' | 'neutral'
}

interface EtudiantRow { id: number }

const SECONDARY: NavEntry[] = [
  { to: '/admin',    label: 'Administration', icon: 'database' },
  { to: '/settings', label: 'Paramètres',     icon: 'settings' },
]

export function Sidebar() {
  const { sidebarCollapsed, toggleSidebar } = useTheme()
  const { user, token } = useAuth()
  const { filiere, setFiliere } = useFiliere()
  const W = sidebarCollapsed ? 64 : 232
  const mayAccessAlerts = user?.role === 'Admin' || user?.role === 'Responsable'

  const [dropOpen, setDropOpen] = useState(false)
  const dropRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (dropRef.current && !dropRef.current.contains(e.target as Node)) {
        setDropOpen(false)
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  // Live badge counts. Gated on `token` so we don't fire unauthenticated calls
  // before login. Alert count is shared with the topbar (one poll, not two).
  const { data: alertesData } = useUnresolvedAlertCount(!!token && mayAccessAlerts)
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
    ...(user?.role !== 'Enseignant'
      ? [{ to: '/predictions', label: 'Prédictions ML', icon: 'brain' as IconName }]
      : []),
    { to: '/reports',     label: 'Rapports',        icon: 'doc' },
  ]

  // Best-effort display name; fall back to the email's local part if the
  // backend hasn't returned a NomComplet (e.g. legacy seed accounts).
  const displayName = user?.nom?.trim() || user?.email?.split('@')[0] || 'Utilisateur'
  const role = user?.role ?? 'Plateforme PFA'
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
        <div className="px-3 py-3" style={{ borderBottom: '1px solid var(--side-border)', position: 'relative' }} ref={dropRef}>
          <div
            className="text-[10px] uppercase tracking-wider mb-1.5"
            style={{ color: 'var(--side-text-3)', fontWeight: 500, letterSpacing: '0.08em' }}
          >
            Périmètre
          </div>
          <button
            onClick={() => setDropOpen(o => !o)}
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
                fontSize: filiere === 'TOUS' ? 7.5 : 9.5,
                fontWeight: 700,
                color: '#fff',
                flexShrink: 0,
              }}
            >
              {filiere === 'TOUS' ? 'ALL' : filiere}
            </span>
            <span className="flex-1 truncate">{filiere === 'TOUS' ? 'Toutes filières' : `Filière ${filiere}`}</span>
            <Icon name={dropOpen ? 'chevUp' : 'chevDown'} size={13} className="opacity-60" />
          </button>

          {dropOpen && (
            <div
              style={{
                position: 'absolute',
                top: '100%',
                left: 12,
                right: 12,
                background: 'var(--surface)',
                border: '1px solid var(--border)',
                borderRadius: 8,
                boxShadow: '0 8px 24px rgba(0,0,0,.25)',
                zIndex: 50,
                overflow: 'hidden',
                marginTop: 4,
              }}
            >
              {FILIERES.map(f => (
                <button
                  key={f}
                  onClick={() => { setFiliere(f); setDropOpen(false) }}
                  className="w-full flex items-center gap-2.5 px-3 py-2 text-[12.5px] text-left transition"
                  style={{
                    background: f === filiere ? 'color-mix(in oklch, var(--accent-500) 10%, transparent)' : 'transparent',
                    color: f === filiere ? 'var(--accent-600)' : 'var(--text)',
                    fontWeight: f === filiere ? 500 : 400,
                  }}
                  onMouseEnter={e => { if (f !== filiere) e.currentTarget.style.background = 'var(--surface-2)' }}
                  onMouseLeave={e => { if (f !== filiere) e.currentTarget.style.background = 'transparent' }}
                >
                  <span
                    style={{
                      width: 18,
                      height: 18,
                      borderRadius: 4,
                      background: f === filiere ? 'var(--accent-500)' : 'var(--surface-2)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontSize: 9,
                      fontWeight: 700,
                      color: f === filiere ? '#fff' : 'var(--text-3)',
                      flexShrink: 0,
                    }}
                  >
                    {f}
                  </span>
                  {f === 'TOUS' ? 'Toutes filières' : `Filière ${f}`}
                  {f === filiere && <Icon name="check" size={12} style={{ marginLeft: 'auto', color: 'var(--accent-500)' }} />}
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      <nav className="flex-1 overflow-y-auto scroll-thin px-3 py-3">
        {!sidebarCollapsed && <div className="nav-section-title">Pilotage</div>}
        {PRIMARY.filter(it => it.to !== '/alerts' || mayAccessAlerts).map(it => (
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
        {user?.role === 'Enseignant' && (
          <NavLink to="/enseignant" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
            <Icon name="doc" size={15} />
            {!sidebarCollapsed && <span className="flex-1 truncate">Espace Enseignant</span>}
          </NavLink>
        )}
        {user?.role === 'Responsable' && (
          <NavLink to="/responsable" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
            <Icon name="graduation" size={15} />
            {!sidebarCollapsed && <span className="flex-1 truncate">Espace Responsable</span>}
          </NavLink>
        )}
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
