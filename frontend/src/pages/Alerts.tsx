import { useState } from 'react'
import type { AxiosError } from 'axios'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { Pill, type PillTone } from '../components/ui/Pill'
import { Empty } from '../components/ui/Empty'
import { FilterChip } from '../components/ui/FilterChip'
import { Modal } from '../components/ui/Modal'
import { SkeletonRows } from '../components/ui/Skeleton'
import { api } from '../services/api'
import { useAuth } from '../context/AuthContext'
import { createCase, dismissSignal } from '../services/interventions'

// Localised labels + tone for the alert types the backend emits.
// Inlined here (not in a "mock" module) because they're real UI strings.
const ALERT_TYPES: Record<string, { label: string; severity: string; pill: 'bad' | 'warn' | 'info' }> = {
  NoteFaible:       { label: 'Note faible',         severity: 'haute',   pill: 'bad'  },
  AbsenceExcessive: { label: 'Absences excessives', severity: 'moyenne', pill: 'warn' },
  RisqueEchec:      { label: 'Risque élevé (ML)',   severity: 'haute',   pill: 'bad'  },
  Abandon:          { label: 'Abandon',             severity: 'haute',   pill: 'bad'  },
}

// Alert niveau → case priority, same scale the triage endpoint suggested.
const PRIORITE_FROM_NIVEAU: Record<string, string> = {
  Critique: 'Critical', Eleve: 'High', Moyen: 'Medium', Faible: 'Low',
}

// ── Backend types ────────────────────────────────────────────
interface BackendEtudiant {
  id: number
  matricule: string
  nom: string
  prenom: string
  nomComplet: string
  filiere: string
  niveau: string
  semestre: string | null
}

// Global counts from /alertes/stats — the list endpoint is capped at 100 rows,
// so deriving totals from the fetched window under-counts (or shows 0 when a
// prediction batch floods the first page with one type).
interface AlerteStats {
  active: number
  resolue: number
  total: number
  parType: Record<string, number>
}

interface BackendAlerte {
  id: number
  etudiantId: number
  moduleId: number | null
  type: string
  niveau: string
  message: string
  resolue: boolean
  creeLe: string
  etudiant: BackendEtudiant | null
}

interface PaginatedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

type Tab = 'active' | 'resolu' | 'all'
type AlertType = 'NoteFaible' | 'AbsenceExcessive' | 'RisqueEchec' | 'Abandon'
type TypeFilter = AlertType | 'Tous'

const FILTERABLE_TYPES: AlertType[] = ['NoteFaible', 'AbsenceExcessive', 'RisqueEchec']

export async function fetchAlertes(): Promise<BackendAlerte[]> {
  // Fetch up to 200 most recent alerts in a single request — enough for the UI.
  const res = await api.get<PaginatedResult<BackendAlerte>>('/alertes?pageSize=200')
  return res.data.items
}

const PAGE_SIZE = 30

export default function Alerts() {
  const { user } = useAuth()
  const canResolve = user?.role !== 'Enseignant'
  const navigate = useNavigate()
  const [tab, setTabRaw] = useState<Tab>('active')
  const [typeFilter, setTypeFilterRaw] = useState<TypeFilter>('Tous')
  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE)
  // Changing tab or filter resets pagination so the list never opens mid-way.
  const setTab = (t: Tab) => { setTabRaw(t); setVisibleCount(PAGE_SIZE) }
  const setTypeFilter = (f: TypeFilter) => { setTypeFilterRaw(f); setVisibleCount(PAGE_SIZE) }
  const [banner, setBanner] = useState<{ tone: 'ok' | 'bad'; text: string } | null>(null)
  const [confirmAllOpen, setConfirmAllOpen] = useState(false)
  const [dismissTarget, setDismissTarget] = useState<BackendAlerte | null>(null)
  const [dismissRaison, setDismissRaison] = useState('')
  const queryClient = useQueryClient()

  const flash = (tone: 'ok' | 'bad', text: string) => {
    setBanner({ tone, text })
    setTimeout(() => setBanner(null), 4000)
  }

  const { data: all = [], isLoading, isError } = useQuery({
    queryKey: ['alertes'],
    queryFn: fetchAlertes,
    refetchInterval: 30_000,
  })

  // Key starts with 'alertes' so every existing invalidateQueries(['alertes'])
  // after resolve/dismiss/case-open refreshes these counts too.
  const { data: stats } = useQuery({
    queryKey: ['alertes', 'stats'],
    queryFn: async () => (await api.get<AlerteStats>('/alertes/stats')).data,
    refetchInterval: 30_000,
  })

  const resolveMutation = useMutation({
    mutationFn: (id: number) => api.patch(`/alertes/${id}/resoudre`, {}),
    onSuccess: async (_res, id) => {
      // Prefix match: invalidating ['alertes'] also refreshes the badge count.
      await queryClient.invalidateQueries({ queryKey: ['alertes'] })
      const alerte = all.find(a => a.id === id)
      if (alerte) {
        const nom = alerte.etudiant
          ? `${alerte.etudiant.prenom} ${alerte.etudiant.nom}`
          : `Étudiant #${alerte.etudiantId}`
        flash('ok', `Alerte de ${nom} marquée comme résolue.`)
      }
    },
  })

  // Triage action folded into the journal: open an intervention case straight
  // from an alert. On 409 the backend returns the already-open case — go there.
  const openCase = useMutation({
    mutationFn: (a: BackendAlerte) => createCase({
      etudiantId: a.etudiantId,
      motif: a.message,
      priorite: PRIORITE_FROM_NIVEAU[a.niveau] ?? 'Medium',
      alerteId: a.id,
    }),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ['alertes'] })
      navigate(`/cases/${created.id}`)
    },
    onError: (error: AxiosError<{ existingCaseId?: number }>) => {
      const existing = error.response?.data.existingCaseId
      if (error.response?.status === 409 && existing) navigate(`/cases/${existing}`)
      else flash('bad', "Impossible d'ouvrir un cas pour cette alerte.")
    },
  })

  // Dismiss ≠ resolve: it records an explicit "not actionable" reason for audit.
  const dismiss = useMutation({
    mutationFn: ({ id, raison }: { id: number; raison: string }) => dismissSignal(id, raison),
    onSuccess: async () => {
      setDismissTarget(null)
      await queryClient.invalidateQueries({ queryKey: ['alertes'] })
      flash('ok', 'Signal rejeté avec raison consignée.')
    },
    onError: () => flash('bad', 'Impossible de rejeter ce signal.'),
  })

  // Bulk resolution applies only to the currently visible unresolved alerts.
  // Scoped to the filtered view so the action matches what the user is looking at.
  const resolveAllMutation = useMutation({
    mutationFn: (ids: number[]) => api.patch('/alertes/batch-resolve', { ids }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['alertes'] })
    },
  })

  const filtered = all.filter(a => {
    if (tab === 'active' && a.resolue) return false
    if (tab === 'resolu' && !a.resolue) return false
    if (typeFilter !== 'Tous' && a.type !== typeFilter) return false
    return true
  })

  // Export the currently-filtered alerts to an .xlsx workbook. CLAUDE.md
  // designates xlsx as the Excel export library for this project; this is
  // the alerts feature's deliverable from the responsability split.
  const handleExport = async () => {
    // Lazy-load SheetJS so the (large) xlsx lib only ships when a user exports.
    const XLSX = await import('xlsx')
    const rows = filtered.map(a => ({
      ID:        a.id,
      Type:      ALERT_TYPES[a.type as AlertType]?.label ?? a.type,
      Niveau:    a.niveau,
      Etudiant:  a.etudiant ? `${a.etudiant.prenom} ${a.etudiant.nom}` : `#${a.etudiantId}`,
      Filière:   a.etudiant?.filiere ?? '',
      Niveau_Et: a.etudiant?.niveau ?? '',
      Semestre:  a.etudiant?.semestre ?? '',
      Message:   a.message,
      Statut:    a.resolue ? 'Résolue' : 'Active',
      'Créée le': new Date(a.creeLe).toLocaleString('fr-FR'),
    }))
    const ws = XLSX.utils.json_to_sheet(rows)
    const wb = XLSX.utils.book_new()
    XLSX.utils.book_append_sheet(wb, ws, 'Alertes')
    const stamp = new Date().toISOString().slice(0, 10)
    XLSX.writeFile(wb, `alertes-${stamp}.xlsx`)
  }

  const handleMarkAllRead = () => {
    const unresolvedIds = filtered.filter(a => !a.resolue).map(a => a.id)
    if (unresolvedIds.length === 0) return
    setConfirmAllOpen(true)
  }

  const confirmMarkAllRead = () => {
    const unresolvedIds = filtered.filter(a => !a.resolue).map(a => a.id)
    if (unresolvedIds.length === 0) return
    setConfirmAllOpen(false)
    resolveAllMutation.mutate(unresolvedIds)
  }

  // Global (DB-side) counts; fall back to the fetched window until stats load.
  const counts: Record<string, number> = {
    NoteFaible:       stats?.parType.NoteFaible       ?? all.filter(a => a.type === 'NoteFaible'       && !a.resolue).length,
    AbsenceExcessive: stats?.parType.AbsenceExcessive ?? all.filter(a => a.type === 'AbsenceExcessive' && !a.resolue).length,
    RisqueEchec:      stats?.parType.RisqueEchec      ?? all.filter(a => a.type === 'RisqueEchec'      && !a.resolue).length,
  }

  const summary: { k: string; label: string; n: number; tone: PillTone; desc: string }[] = [
    { k: 'NoteFaible',       label: 'Note faible',         n: counts.NoteFaible,       tone: 'bad',  desc: 'Étudiants avec note < 10' },
    { k: 'AbsenceExcessive', label: 'Absences excessives', n: counts.AbsenceExcessive, tone: 'warn', desc: '> 20h non justifiées' },
    { k: 'RisqueEchec',      label: 'Risque élevé (ML)',   n: counts.RisqueEchec,      tone: 'bad',  desc: 'Score modèle > 65%' },
  ]

  const TABS: { id: Tab; label: string; n: number }[] = [
    { id: 'active', label: 'Actives',  n: stats?.active  ?? all.filter(a => !a.resolue).length },
    { id: 'resolu', label: 'Résolues', n: stats?.resolue ?? all.filter(a => a.resolue).length },
    { id: 'all',    label: 'Toutes',   n: stats?.total   ?? all.length },
  ]

  // True size of the current view (DB-side), vs. the ≤100 rows actually fetched.
  const viewTotal = tab === 'active' ? TABS[0].n : tab === 'resolu' ? TABS[1].n : TABS[2].n

  return (
    <div className="space-y-4">
      {/* Action feedback banner */}
      {banner && (
        <div
          className="flex items-center gap-3 px-4 py-3 rounded-lg text-[13px]"
          role="status"
          style={{
            background: `color-mix(in oklch, var(--${banner.tone}) 10%, transparent)`,
            border: `1px solid color-mix(in oklch, var(--${banner.tone}) 30%, transparent)`,
            color: `var(--${banner.tone})`,
          }}
        >
          <Icon name={banner.tone === 'ok' ? 'check' : 'alert'} size={15} style={{ flexShrink: 0 }} />
          <span className="flex-1 font-medium">{banner.text}</span>
          <button className="btn btn-sm btn-ghost" style={{ color: 'inherit' }} onClick={() => setBanner(null)} aria-label="Fermer">
            <Icon name="x" size={12} />
          </button>
        </div>
      )}

      <div className="page-heading">
        <div>
          <div className="cap mb-1 flex items-center gap-2">
            <span className="pill-dot live-dot" style={{ background: 'var(--accent-500)' }} />
            Signaux automatiques · mise à jour toutes les 30 s
          </div>
          <h1 className="text-[22px] font-semibold tracking-tight">Alertes</h1>
          <p className="mt-1 text-[12.5px]" style={{ color: 'var(--text-3)' }}>
            Résoudre, rejeter avec raison, ou ouvrir une intervention — tout depuis cette file.
          </p>
        </div>
        <div className="page-actions">
          {canResolve && (
            <button
              className="btn btn-sm"
              onClick={handleMarkAllRead}
              disabled={resolveAllMutation.isPending || filtered.every(a => a.resolue)}
            >
              <Icon name="check" size={13} />
              {resolveAllMutation.isPending ? 'Résolution…' : 'Tout résoudre'}
            </button>
          )}
          <button
            className="btn btn-sm"
            onClick={handleExport}
            disabled={filtered.length === 0}
          >
            <Icon name="download" size={13} />
            Exporter
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        {summary.map(c => (
          <button
            key={c.k}
            // Toggle: clicking the active card clears the filter again.
            onClick={() => setTypeFilter(typeFilter === c.k ? 'Tous' : (c.k as TypeFilter))}
            aria-pressed={typeFilter === c.k}
            className="card p-4 text-left"
            style={{
              borderColor: typeFilter === c.k ? 'var(--accent-500)' : 'var(--border)',
              boxShadow:
                typeFilter === c.k
                  ? '0 0 0 3px color-mix(in oklch, var(--accent-500) 14%, transparent)'
                  : 'none',
              transition: 'border-color .12s, box-shadow .12s',
            }}
          >
            <div className="flex items-center justify-between mb-2">
              <Pill tone={c.tone} dot>{c.label}</Pill>
              <Icon
                name="alert"
                size={14}
                style={{ color: c.tone === 'bad' ? 'var(--bad)' : 'var(--warn)' }}
              />
            </div>
            <div className="num text-[28px] font-medium" style={{ letterSpacing: '-0.025em' }}>
              {c.n}
            </div>
            <div className="cap mt-0.5">{c.desc}</div>
          </button>
        ))}
      </div>

      {/* The single panel: status + type filters live in its header, rows below. */}
      <div className="card overflow-hidden">
        <div
          className="flex items-center justify-between gap-3 flex-wrap px-4 py-3"
          style={{ borderBottom: '1px solid var(--border)' }}
        >
          <div>
            <div className="text-[13.5px] font-semibold tracking-tight">File des signaux</div>
            <div className="cap mt-0.5">
              {viewTotal > filtered.length && typeFilter === 'Tous'
                ? `${filtered.length} affichés (les plus récents) sur ${viewTotal}`
                : `${filtered.length} signal${filtered.length > 1 ? 'aux' : ''}`}
              {typeFilter !== 'Tous' && ` · type ${ALERT_TYPES[typeFilter as AlertType]?.label ?? typeFilter}`}
            </div>
          </div>
          <div className="flex items-center gap-2">
            {typeFilter !== 'Tous' && FILTERABLE_TYPES.includes(typeFilter as AlertType) && (
              <FilterChip
                label="Type"
                value={ALERT_TYPES[typeFilter as AlertType]?.label ?? typeFilter}
                onRemove={() => setTypeFilter('Tous')}
              />
            )}
            <div
              className="flex items-center rounded-lg overflow-hidden"
              style={{ border: '1px solid var(--border-2)', background: 'var(--surface)' }}
            >
              {TABS.map((t, i) => (
                <button
                  key={t.id}
                  onClick={() => setTab(t.id)}
                  aria-pressed={tab === t.id}
                  className="px-3 py-1.5 text-[12px] transition flex items-center gap-1.5"
                  style={{
                    background: tab === t.id ? 'var(--text)' : 'transparent',
                    color: tab === t.id ? 'var(--bg)' : 'var(--text-3)',
                    fontWeight: tab === t.id ? 500 : 400,
                    borderRight: i < TABS.length - 1 ? '1px solid var(--border)' : 'none',
                  }}
                >
                  {t.label}
                  <span className="num text-[10.5px]" style={{ opacity: 0.75 }}>{t.n}</span>
                </button>
              ))}
            </div>
          </div>
        </div>

        {isLoading && <SkeletonRows rows={6} />}
        {isError && (
          <div className="px-4 py-8 text-center cap" style={{ color: 'var(--bad)' }}>
            Impossible de charger les alertes. Vérifiez la connexion au serveur.
          </div>
        )}
        {!isLoading && !isError && filtered.length === 0 && (
          <Empty
            title="Aucune alerte"
            hint={typeFilter !== 'Tous'
              ? 'Aucune alerte de ce type dans cette vue — effacez le filtre pour tout revoir.'
              : 'Les alertes générées par le système apparaîtront ici.'}
          />
        )}
        {!isLoading && !isError && filtered.slice(0, visibleCount).map((a, i) => (
          <AlertRow
            key={a.id}
            alert={a}
            divider={i < Math.min(filtered.length, visibleCount) - 1}
            canResolve={canResolve}
            onView={() => navigate(`/students/${a.etudiantId}`)}
            onResolve={() => resolveMutation.mutate(a.id)}
            resolving={resolveMutation.isPending && resolveMutation.variables === a.id}
            onOpenCase={() => openCase.mutate(a)}
            openingCase={openCase.isPending && openCase.variables?.id === a.id}
            onDismiss={() => { setDismissRaison(''); setDismissTarget(a) }}
          />
        ))}
        {!isLoading && !isError && filtered.length > visibleCount && (
          <div className="px-4 py-3 text-center" style={{ borderTop: '1px solid var(--border)' }}>
            <button
              className="btn btn-sm"
              onClick={() => setVisibleCount(n => n + PAGE_SIZE)}
            >
              Afficher plus ({filtered.length - visibleCount} restantes)
            </button>
          </div>
        )}
      </div>

      <Modal open={confirmAllOpen} onClose={() => setConfirmAllOpen(false)} title="Marquer comme résolues">
        <p className="text-[12.5px]" style={{ color: 'var(--text-2)' }}>
          Marquer les alertes non résolues visibles comme résolues ?
        </p>
        <div className="flex justify-end gap-2 mt-4">
          <button className="btn btn-sm" onClick={() => setConfirmAllOpen(false)}>Annuler</button>
          <button
            className="btn btn-sm btn-primary"
            onClick={confirmMarkAllRead}
            disabled={resolveAllMutation.isPending}
          >
            Marquer comme résolues
          </button>
        </div>
      </Modal>

      <Modal open={dismissTarget !== null} onClose={() => setDismissTarget(null)} title="Rejeter le signal">
        <label className="flex flex-col gap-1">
          <span className="cap">Raison du rejet</span>
          <textarea
            className="input"
            rows={3}
            style={{ height: 'auto', padding: '8px 10px' }}
            value={dismissRaison}
            onChange={e => setDismissRaison(e.target.value)}
            placeholder="Pourquoi ce signal ne nécessite pas d'action…"
            autoFocus
          />
        </label>
        <div className="flex justify-end gap-2 mt-4">
          <button className="btn btn-sm" onClick={() => setDismissTarget(null)}>Annuler</button>
          <button
            className="btn btn-sm btn-primary"
            onClick={() => dismissTarget && dismissRaison.trim() && dismiss.mutate({ id: dismissTarget.id, raison: dismissRaison.trim() })}
            disabled={dismiss.isPending || !dismissRaison.trim()}
          >
            Rejeter
          </button>
        </div>
      </Modal>
    </div>
  )
}

function AlertRow({
  alert,
  divider,
  canResolve,
  onView,
  onResolve,
  resolving,
  onOpenCase,
  openingCase,
  onDismiss,
}: {
  alert: BackendAlerte
  divider: boolean
  canResolve: boolean
  onView: () => void
  onResolve: () => void
  resolving: boolean
  onOpenCase: () => void
  openingCase: boolean
  onDismiss: () => void
}) {
  const meta = ALERT_TYPES[alert.type as AlertType] ?? { label: alert.type, severity: '', pill: 'neutral' as PillTone }
  const iconName: 'brain' | 'clock' | 'alert' =
    alert.type === 'RisqueEchec'
      ? 'brain'
      : alert.type === 'AbsenceExcessive'
      ? 'clock'
      : 'alert'

  const etudiantName = alert.etudiant
    ? `${alert.etudiant.prenom} ${alert.etudiant.nom}`
    : `Étudiant #${alert.etudiantId}`
  // "IA · CI2 · S8" — enough context to navigate without opening the profile.
  const contexte = alert.etudiant
    ? [alert.etudiant.filiere, alert.etudiant.niveau, alert.etudiant.semestre].filter(Boolean).join(' · ')
    : '—'

  return (
    <div
      className="flex items-center gap-3 px-4 py-3"
      style={{ borderBottom: divider ? '1px solid var(--border)' : 'none' }}
    >
      <div
        className="w-8 h-8 rounded-md flex items-center justify-center flex-shrink-0"
        style={{
          background:
            meta.pill === 'bad'
              ? 'color-mix(in oklch, var(--bad) 12%, transparent)'
              : meta.pill === 'warn'
              ? 'color-mix(in oklch, var(--warn) 14%, transparent)'
              : 'var(--surface-2)',
          color:
            meta.pill === 'bad'
              ? 'var(--bad)'
              : meta.pill === 'warn'
              ? '#b45309'
              : 'var(--text-3)',
        }}
      >
        <Icon name={iconName} size={15} />
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <Pill tone={meta.pill as PillTone}>{meta.label}</Pill>
          <button
            className="text-[12.5px] font-medium hover:underline"
            onClick={onView}
            title="Voir la fiche étudiant"
          >
            {etudiantName}
          </button>
          <span className="cap font-mono">· {contexte}</span>
          {!alert.resolue && (
            <span className="pill-dot live-dot ml-1" style={{ background: 'var(--bad)' }} />
          )}
        </div>
        <div className="text-[12px] mt-0.5 truncate" style={{ color: 'var(--text-2)' }}>
          {alert.message}
        </div>
      </div>
      <div
        className="text-[11.5px] text-right flex-shrink-0"
        style={{ color: 'var(--text-3)', minWidth: 72 }}
      >
        <div>
          {new Date(alert.creeLe).toLocaleDateString('fr-FR', { day: 'numeric', month: 'short' })}
        </div>
        <div className="cap">
          {new Date(alert.creeLe).toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })}
        </div>
      </div>
      {!alert.resolue ? (
        <div className="flex items-center gap-1.5 flex-shrink-0">
          {canResolve && (
            <>
              <button
                className="btn btn-sm btn-ghost"
                onClick={onDismiss}
                title="Rejeter ce signal (raison obligatoire)"
              >
                <Icon name="x" size={12} />
              </button>
              <button
                className="btn btn-sm"
                onClick={onOpenCase}
                disabled={openingCase}
                title="Ouvrir une intervention pour cet étudiant"
              >
                <Icon name="plus" size={12} />
                {openingCase ? '…' : 'Ouvrir un cas'}
              </button>
              <button
                className="btn btn-sm btn-primary"
                onClick={onResolve}
                disabled={resolving}
                title="Marquer cette alerte comme résolue"
              >
                <Icon name="check" size={12} strokeWidth={2.4} />
                {resolving ? '…' : 'Résoudre'}
              </button>
            </>
          )}
          {!canResolve && (
            <button className="btn btn-sm" onClick={onView}>
              <Icon name="eye" size={13} />
              Voir
            </button>
          )}
        </div>
      ) : (
        <Pill tone="ok" dot>Résolue</Pill>
      )}
    </div>
  )
}
