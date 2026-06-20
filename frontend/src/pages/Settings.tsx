import { useTheme, type Theme, type Density, type Accent } from '../context/ThemeContext'
import { useAuth } from '../context/AuthContext'
import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'

// ── Sub-components ────────────────────────────────────────────────────────────

function SectionCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="card overflow-hidden">
      <div
        className="px-5 py-3 text-[12px] uppercase tracking-wider font-medium"
        style={{
          color: 'var(--text-3)',
          borderBottom: '1px solid var(--border)',
          background: 'var(--surface-2)',
          letterSpacing: '0.07em',
        }}
      >
        {title}
      </div>
      <div className="divide-y" style={{ borderColor: 'var(--border)' }}>
        {children}
      </div>
    </div>
  )
}

function Row({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between px-5 py-4">
      <div>
        <div className="text-[13px] font-medium">{label}</div>
        {hint && <div className="cap mt-0.5">{hint}</div>}
      </div>
      <div className="flex items-center gap-2">{children}</div>
    </div>
  )
}

// ── Appearance ────────────────────────────────────────────────────────────────

const THEMES: { value: Theme; label: string; icon: string }[] = [
  { value: 'light', label: 'Clair',  icon: '☀' },
  { value: 'dark',  label: 'Sombre', icon: '☾' },
]

const DENSITIES: { value: Density; label: string; desc: string }[] = [
  { value: 'compact',     label: 'Compact',     desc: 'Maximum d\'information à l\'écran' },
  { value: 'comfortable', label: 'Confortable',  desc: 'Équilibre lisibilité / densité' },
  { value: 'spacious',    label: 'Aéré',         desc: 'Plus d\'espace entre les éléments' },
]

const ACCENT_COLORS: { value: Accent; label: string; hex: string }[] = [
  { value: 'amber',   label: 'Ambre',    hex: '#f97316' },
  { value: 'indigo',  label: 'Indigo',   hex: '#6366f1' },
  { value: 'emerald', label: 'Émeraude', hex: '#10b981' },
  { value: 'rose',    label: 'Rose',     hex: '#f43f5e' },
]

// ── Main component ────────────────────────────────────────────────────────────

export default function Settings() {
  const { theme, setTheme, density, setDensity, accent, setAccent, sidebarCollapsed, setSidebarCollapsed } = useTheme()
  const { user, logout } = useAuth()

  const displayName = user?.nom?.trim() || user?.email?.split('@')[0] || 'Utilisateur'

  return (
    <div className="space-y-5 max-w-2xl">
      <div>
        <div className="cap mb-1">Système</div>
        <h1 className="text-[22px] font-semibold tracking-tight">Paramètres</h1>
      </div>

      {/* Profile */}
      <SectionCard title="Profil">
        <div className="px-5 py-4 flex items-center gap-4">
          <Avatar name={displayName} size={44} />
          <div>
            <div className="text-[14px] font-semibold">{displayName}</div>
            <div className="cap mt-0.5">{user?.email}</div>
            <div className="mt-1">
              <span
                className="text-[11px] px-2 py-0.5 rounded-full font-medium"
                style={{
                  background: user?.role === 'Admin'
                    ? 'color-mix(in oklch, var(--bad) 12%, transparent)'
                    : user?.role === 'Responsable'
                    ? 'color-mix(in oklch, var(--warn) 14%, transparent)'
                    : 'color-mix(in oklch, var(--ok) 12%, transparent)',
                  color: user?.role === 'Admin'
                    ? 'var(--bad)'
                    : user?.role === 'Responsable'
                    ? '#b45309'
                    : 'var(--ok)',
                }}
              >
                {user?.role}
              </span>
            </div>
          </div>
        </div>
        <Row
          label="Se déconnecter"
          hint="Termine la session en cours et efface le jeton d'authentification"
        >
          <button
            className="btn btn-sm"
            onClick={logout}
            style={{ color: 'var(--bad)', borderColor: 'color-mix(in oklch, var(--bad) 30%, transparent)' }}
          >
            <Icon name="x" size={13} />
            Déconnexion
          </button>
        </Row>
      </SectionCard>

      {/* Appearance */}
      <SectionCard title="Apparence">
        {/* Theme toggle */}
        <Row label="Thème" hint="Choix entre l'interface claire et sombre">
          <div className="flex items-center gap-1 p-0.5 rounded-lg" style={{ background: 'var(--surface-2)', border: '1px solid var(--border)' }}>
            {THEMES.map(t => (
              <button
                key={t.value}
                onClick={() => setTheme(t.value)}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-[12.5px] transition-all"
                style={{
                  background: theme === t.value ? 'var(--surface)' : 'transparent',
                  color: theme === t.value ? 'var(--text)' : 'var(--text-3)',
                  fontWeight: theme === t.value ? 500 : 400,
                  boxShadow: theme === t.value ? '0 1px 3px rgba(0,0,0,.1)' : 'none',
                }}
              >
                <span style={{ fontSize: 13 }}>{t.icon}</span>
                {t.label}
              </button>
            ))}
          </div>
        </Row>

        {/* Accent color */}
        <Row label="Couleur d'accent" hint="Teinte principale des boutons, badges et graphiques">
          <div className="flex items-center gap-2">
            {ACCENT_COLORS.map(c => (
              <button
                key={c.value}
                onClick={() => setAccent(c.value)}
                title={c.label}
                style={{
                  width: 24,
                  height: 24,
                  borderRadius: 999,
                  background: c.hex,
                  border: accent === c.value
                    ? `3px solid var(--text)`
                    : '3px solid transparent',
                  outline: accent === c.value
                    ? `2px solid ${c.hex}`
                    : '2px solid transparent',
                  outlineOffset: 1,
                  transition: 'border .1s, outline .1s',
                }}
              />
            ))}
            <span className="text-[12.5px] ml-1" style={{ color: 'var(--text-3)' }}>
              {ACCENT_COLORS.find(c => c.value === accent)?.label}
            </span>
          </div>
        </Row>

        {/* Density */}
        <Row label="Densité de l'interface" hint="Espacement entre les éléments de la page">
          <div className="flex items-center gap-1 p-0.5 rounded-lg" style={{ background: 'var(--surface-2)', border: '1px solid var(--border)' }}>
            {DENSITIES.map(d => (
              <button
                key={d.value}
                onClick={() => setDensity(d.value)}
                title={d.desc}
                className="px-3 py-1.5 rounded-md text-[12.5px] transition-all"
                style={{
                  background: density === d.value ? 'var(--surface)' : 'transparent',
                  color: density === d.value ? 'var(--text)' : 'var(--text-3)',
                  fontWeight: density === d.value ? 500 : 400,
                  boxShadow: density === d.value ? '0 1px 3px rgba(0,0,0,.1)' : 'none',
                }}
              >
                {d.label}
              </button>
            ))}
          </div>
        </Row>

        {/* Sidebar */}
        <Row label="Sidebar réduite" hint="Afficher uniquement les icônes dans la barre latérale">
          <button
            role="switch"
            aria-checked={sidebarCollapsed}
            onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
            className="relative flex-shrink-0"
            style={{
              width: 40,
              height: 22,
              borderRadius: 999,
              background: sidebarCollapsed ? 'var(--accent-500)' : 'var(--surface-2)',
              border: '1px solid var(--border)',
              transition: 'background .15s',
              cursor: 'pointer',
            }}
          >
            <span
              style={{
                position: 'absolute',
                top: 2,
                left: sidebarCollapsed ? 20 : 2,
                width: 16,
                height: 16,
                borderRadius: 999,
                background: '#fff',
                boxShadow: '0 1px 3px rgba(0,0,0,.2)',
                transition: 'left .15s',
              }}
            />
          </button>
        </Row>
      </SectionCard>

      {/* About */}
      <SectionCard title="À propos">
        <Row label="Plateforme ENIAD" hint="Système décisionnel & analytique pour les performances étudiants">
          <span className="cap font-mono">2025 / 2026</span>
        </Row>
        <Row label="Équipe" hint="AIT ALI · AMGHAR · ENGAR — encadré par Prof. LHIADI">
          <span className="cap font-mono">PFA</span>
        </Row>
        <Row label="Version" hint="Stack : React · ASP.NET Core 8 · FastAPI · SQL Server">
          <span className="cap font-mono">v1.0.0</span>
        </Row>
      </SectionCard>
    </div>
  )
}
