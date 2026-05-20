import { useEffect, useMemo, useRef, useState } from 'react'
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
  const [query, setQuery] = useState('')
  const [active, setActive] = useState(0)
  const listRef = useRef<HTMLDivElement>(null)

  // Flatten the visible groups into a 1-D index so ↑/↓ jump across group
  // boundaries the way users expect from a command palette.
  const flat = useMemo(() => {
    const q = query.trim().toLowerCase()
    const list: PaletteItem[] = []
    for (const g of GROUPS) {
      for (const it of g.items) {
        if (!q || it.label.toLowerCase().includes(q) || (it.sub?.toLowerCase().includes(q))) {
          list.push(it)
        }
      }
    }
    return list
  }, [query])

  // Reset selection + query whenever the palette opens.
  useEffect(() => {
    if (open) {
      setQuery('')
      setActive(0)
    }
  }, [open])

  // Clamp active index when the filtered list shrinks.
  useEffect(() => {
    if (active >= flat.length) setActive(Math.max(0, flat.length - 1))
  }, [flat.length, active])

  // Keep the focused row in view as the user arrow-keys past the viewport.
  useEffect(() => {
    const el = listRef.current?.querySelector<HTMLElement>(`[data-cmd-idx="${active}"]`)
    el?.scrollIntoView({ block: 'nearest' })
  }, [active])

  if (!open) return null

  const choose = (it: PaletteItem) => {
    if (it.to) {
      navigate(it.to)
      onClose()
    }
  }

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setActive(i => (flat.length === 0 ? 0 : (i + 1) % flat.length))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActive(i => (flat.length === 0 ? 0 : (i - 1 + flat.length) % flat.length))
    } else if (e.key === 'Enter') {
      e.preventDefault()
      const it = flat[active]
      if (it) choose(it)
    } else if (e.key === 'Escape') {
      e.preventDefault()
      onClose()
    }
  }

  // Walk groups so we can render headers, but key off the flat index for ↑/↓.
  let cursor = 0

  return (
    <div
      onClick={onClose}
      className="fixed inset-0 z-50 flex items-start justify-center pt-[10vh]"
      style={{ background: 'rgba(12,10,9,0.42)', backdropFilter: 'blur(2px)' }}
    >
      <div
        onClick={e => e.stopPropagation()}
        onKeyDown={onKeyDown}
        role="dialog"
        aria-modal="true"
        aria-label="Palette de commandes"
        className="cmd-shadow rounded-xl w-full max-w-[560px] overflow-hidden"
        style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}
      >
        <div className="flex items-center gap-2.5 px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
          <Icon name="search" size={15} style={{ color: 'var(--text-3)' }} />
          <input
            autoFocus
            value={query}
            onChange={e => setQuery(e.target.value)}
            placeholder="Rechercher étudiant, alerte, action…"
            className="flex-1 bg-transparent outline-none text-[13.5px]"
            style={{ color: 'var(--text)' }}
          />
          <kbd>esc</kbd>
        </div>
        <div ref={listRef} className="max-h-[440px] overflow-y-auto scroll-thin py-1.5">
          {flat.length === 0 && (
            <div className="px-4 py-6 text-center cap">Aucun résultat</div>
          )}
          {GROUPS.map(g => {
            const visible = g.items.filter(it => {
              const q = query.trim().toLowerCase()
              return !q || it.label.toLowerCase().includes(q) || (it.sub?.toLowerCase().includes(q))
            })
            if (visible.length === 0) return null
            return (
              <div key={g.name} className="mb-1">
                <div
                  className="px-4 py-1.5 text-[10.5px] uppercase tracking-wider font-medium"
                  style={{ color: 'var(--text-3)', letterSpacing: '0.08em' }}
                >
                  {g.name}
                </div>
                {visible.map(it => {
                  const idx = cursor++
                  const isActive = idx === active
                  return (
                    <div
                      key={it.label}
                      data-cmd-idx={idx}
                      role="option"
                      aria-selected={isActive}
                      onMouseEnter={() => setActive(idx)}
                      onClick={() => choose(it)}
                      className="flex items-center gap-2.5 px-4 py-2 mx-1.5 rounded-md cursor-pointer"
                      style={{
                        background: isActive ? 'var(--surface-2)' : 'transparent',
                        transition: 'background .1s',
                      }}
                    >
                      <Icon name={it.icon} size={14} style={{ color: 'var(--text-3)' }} />
                      <span className="flex-1 text-[12.5px]">{it.label}</span>
                      {it.sub && <span className="cap">{it.sub}</span>}
                      {it.kbd && <kbd>{it.kbd}</kbd>}
                    </div>
                  )
                })}
              </div>
            )
          })}
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
