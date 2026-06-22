import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Icon, type IconName } from '../components/ui/Icon'
import { useAuth } from '../context/AuthContext'
import { api } from '../services/api'
import { canRunBatchPredictions } from '../auth/roles'

interface PaletteItem {
  label: string
  icon: IconName
  to?: string
  kbd?: string
  sub?: string
  action?: () => Promise<void> | void
}

interface PaletteGroup {
  name: string
  items: PaletteItem[]
}

const NAV_ITEMS: PaletteItem[] = [
  { label: 'Tableau de bord', icon: 'dashboard', to: '/dashboard',   kbd: 'G D' },
  { label: 'Étudiants',       icon: 'students',  to: '/students',    kbd: 'G E' },
  { label: 'Alertes',         icon: 'bell',      to: '/alerts',      kbd: 'G A' },
  { label: 'Prédictions ML',  icon: 'brain',     to: '/predictions', kbd: 'G P' },
  { label: 'Rapports',        icon: 'doc',       to: '/reports',     kbd: 'G R' },
]

interface CommandPaletteProps {
  open: boolean
  onClose: () => void
}

export function CommandPalette({ open, onClose }: CommandPaletteProps) {
  const navigate  = useNavigate()
  const { token, user } = useAuth()
  const [query, setQuery]       = useState('')
  const [active, setActive]     = useState(0)
  const [status, setStatus]     = useState<Record<string, 'idle' | 'loading' | 'ok' | 'err'>>({})
  const listRef = useRef<HTMLDivElement>(null)
  const isAdmin      = user?.role === 'Admin'
  const canBatch     = canRunBatchPredictions(user?.role)

  // Top-3 highest-risk students from the real API
  const { data: topStudents = [] } = useQuery({
    queryKey: ['cmd-palette', 'top-risk'],
    queryFn: async () => {
      const res = await api.get<{ items: { nomComplet?: string; nom?: string; prenom?: string; matricule: string; filiere?: string; niveau?: string; scoreRisque: number }[] }>(
        '/etudiants/with-stats?pageSize=300'
      )
      return res.data.items
        .filter(s => s.scoreRisque != null)
        .sort((a, b) => b.scoreRisque - a.scoreRisque)
        .slice(0, 3)
    },
    enabled: !!token && open,
    staleTime: 60_000,
  })

  const runAction = async (label: string, fn: () => Promise<void>) => {
    setStatus(s => ({ ...s, [label]: 'loading' }))
    try {
      await fn()
      setStatus(s => ({ ...s, [label]: 'ok' }))
      setTimeout(() => { setStatus(s => ({ ...s, [label]: 'idle' })); onClose() }, 900)
    } catch {
      setStatus(s => ({ ...s, [label]: 'err' }))
      setTimeout(() => setStatus(s => ({ ...s, [label]: 'idle' })), 2000)
    }
  }

  const ACTION_ITEMS: PaletteItem[] = [
    ...(canBatch ? [{
      label: 'Lancer une prédiction batch…',
      icon: 'spark' as IconName,
      action: () => runAction('batch', () => api.post('/predictions/batch', {}).then(() => {})),
    }] : []),
    ...(isAdmin ? [{
      label: 'Synchroniser le Data Warehouse',
      icon: 'refresh' as IconName,
      action: () => runAction('sync', () => api.post('/admin/sync-dw').then(() => {})),
    }] : []),
    {
      label: 'Exporter le rapport courant en PDF',
      icon: 'download',
      to: '/reports',
    },
  ]

  const GROUPS: PaletteGroup[] = useMemo(() => [
    { name: 'Pages',   items: NAV_ITEMS },
    { name: 'Actions', items: ACTION_ITEMS },
    {
      name: 'Étudiants à risque élevé',
      items: topStudents.map(s => ({
        label: s.nomComplet ?? (`${s.prenom ?? ''} ${s.nom ?? ''}`.trim() || s.matricule),
        icon: 'user' as IconName,
        sub: `Score risque ${Math.round(s.scoreRisque * 100)}%`,
        to: `/students`,
      })),
    },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [topStudents, isAdmin, status])

  const flat = useMemo(() => {
    const q = query.trim().toLowerCase()
    return GROUPS.flatMap(g =>
      g.items.filter(it =>
        !q || it.label.toLowerCase().includes(q) || it.sub?.toLowerCase().includes(q)
      )
    )
  }, [query, GROUPS])

  useEffect(() => {
    if (open) { setQuery(''); setActive(0) }
  }, [open])

  useEffect(() => {
    if (active >= flat.length) setActive(Math.max(0, flat.length - 1))
  }, [flat.length, active])

  useEffect(() => {
    const el = listRef.current?.querySelector<HTMLElement>(`[data-cmd-idx="${active}"]`)
    el?.scrollIntoView({ block: 'nearest' })
  }, [active])

  if (!open) return null

  const choose = (it: PaletteItem) => {
    if (it.action) { it.action(); return }
    if (it.to) { navigate(it.to); onClose() }
  }

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setActive(i => flat.length === 0 ? 0 : (i + 1) % flat.length)
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActive(i => flat.length === 0 ? 0 : (i - 1 + flat.length) % flat.length)
    } else if (e.key === 'Enter') {
      e.preventDefault()
      const it = flat[active]
      if (it) choose(it)
    } else if (e.key === 'Escape') {
      e.preventDefault()
      onClose()
    }
  }

  const statusIcon = (label: string): string | null => {
    const key = label.includes('batch') ? 'batch' : label.includes('Warehouse') ? 'sync' : null
    if (!key) return null
    const s = status[key]
    if (s === 'loading') return '…'
    if (s === 'ok')      return '✓'
    if (s === 'err')     return '✗'
    return null
  }

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
        {/* Search input */}
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

        {/* Results */}
        <div ref={listRef} className="max-h-[440px] overflow-y-auto scroll-thin py-1.5">
          {flat.length === 0 && (
            <div className="px-4 py-6 text-center cap">Aucun résultat</div>
          )}
          {GROUPS.map(g => {
            const visible = g.items.filter(it => {
              const q = query.trim().toLowerCase()
              return !q || it.label.toLowerCase().includes(q) || it.sub?.toLowerCase().includes(q)
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
                  const idx   = cursor++
                  const isAct = idx === active
                  const si    = statusIcon(it.label)
                  return (
                    <div
                      key={it.label}
                      data-cmd-idx={idx}
                      role="option"
                      aria-selected={isAct}
                      onMouseEnter={() => setActive(idx)}
                      onClick={() => choose(it)}
                      className="flex items-center gap-2.5 px-4 py-2 mx-1.5 rounded-md cursor-pointer"
                      style={{
                        background: isAct ? 'var(--surface-2)' : 'transparent',
                        transition: 'background .1s',
                      }}
                    >
                      <Icon name={it.icon} size={14} style={{ color: 'var(--text-3)' }} />
                      <span className="flex-1 text-[12.5px]">{it.label}</span>
                      {si && (
                        <span
                          style={{
                            fontSize: 12,
                            fontWeight: 600,
                            color: si === '✓' ? 'var(--ok)' : si === '✗' ? 'var(--bad)' : 'var(--text-3)',
                          }}
                        >
                          {si}
                        </span>
                      )}
                      {it.sub && <span className="cap">{it.sub}</span>}
                      {it.kbd && <kbd>{it.kbd}</kbd>}
                    </div>
                  )
                })}
              </div>
            )
          })}
        </div>

        {/* Footer */}
        <div
          className="px-4 py-2 flex items-center justify-between text-[11px]"
          style={{ background: 'var(--surface-2)', borderTop: '1px solid var(--border)', color: 'var(--text-3)' }}
        >
          <div className="flex items-center gap-3">
            <span className="flex items-center gap-1"><kbd>↵</kbd> ouvrir</span>
            <span className="flex items-center gap-1"><kbd>↑</kbd><kbd>↓</kbd> naviguer</span>
          </div>
          <span>Plateforme PFA · v0.4</span>
        </div>
      </div>
    </div>
  )
}
