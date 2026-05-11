import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { Pill, type PillTone } from '../components/ui/Pill'
import { Empty } from '../components/ui/Empty'
import { FilterChip } from '../components/ui/FilterChip'
import { api } from '../services/api'

// Localised labels + tone for the alert types the backend emits.
// Inlined here (not in a "mock" module) because they're real UI strings.
const ALERT_TYPES: Record<string, { label: string; severity: string; pill: 'bad' | 'warn' | 'info' }> = {
  NoteFaible:       { label: 'Note faible',         severity: 'haute',   pill: 'bad'  },
  AbsenceExcessive: { label: 'Absences excessives', severity: 'moyenne', pill: 'warn' },
  RisqueEleve:      { label: 'Risque élevé (ML)',   severity: 'haute',   pill: 'bad'  },
  ModuleNonValide:  { label: 'Module non validé',   severity: 'moyenne', pill: 'warn' },
  RetardRecurrent:  { label: 'Retards récurrents',  severity: 'basse',   pill: 'info' },
}

// ── Backend types ────────────────────────────────────────────
interface BackendEtudiant {
  id: number
  nom: string
  prenom: string
  filiere: string
  niveau: string
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
type AlertType = 'NoteFaible' | 'AbsenceExcessive' | 'RisqueEleve'
type TypeFilter = AlertType | 'Tous'

const FILTERABLE_TYPES: AlertType[] = ['NoteFaible', 'AbsenceExcessive', 'RisqueEleve']

async function fetchAlertes(): Promise<BackendAlerte[]> {
  // Fetch up to 200 most recent alerts in a single request — enough for the UI.
  const res = await api.get<PaginatedResult<BackendAlerte>>('/alertes?pageSize=200')
  return res.data.items
}

export default function Alerts() {
  const [tab, setTab] = useState<Tab>('active')
  const [typeFilter, setTypeFilter] = useState<TypeFilter>('Tous')
  const queryClient = useQueryClient()

  const { data: all = [], isLoading, isError } = useQuery({
    queryKey: ['alertes'],
    queryFn: fetchAlertes,
    refetchInterval: 30_000,
  })

  const resolveMutation = useMutation({
    mutationFn: (id: number) => api.patch(`/alertes/${id}/resoudre`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['alertes'] }),
  })

  const filtered = all.filter(a => {
    if (tab === 'active' && a.resolue) return false
    if (tab === 'resolu' && !a.resolue) return false
    if (typeFilter !== 'Tous' && a.type !== typeFilter) return false
    return true
  })

  const counts: Record<AlertType, number> = {
    NoteFaible:       all.filter(a => a.type === 'NoteFaible'       && !a.resolue).length,
    AbsenceExcessive: all.filter(a => a.type === 'AbsenceExcessive' && !a.resolue).length,
    RisqueEleve:      all.filter(a => a.type === 'RisqueEleve'      && !a.resolue).length,
  }

  const summary: { k: AlertType; label: string; n: number; tone: PillTone; desc: string }[] = [
    { k: 'NoteFaible',       label: 'Note faible',         n: counts.NoteFaible,       tone: 'bad',  desc: 'Étudiants avec note < 10' },
    { k: 'AbsenceExcessive', label: 'Absences excessives', n: counts.AbsenceExcessive, tone: 'warn', desc: '> 20h non justifiées' },
    { k: 'RisqueEleve',      label: 'Risque élevé (ML)',   n: counts.RisqueEleve,      tone: 'bad',  desc: 'Score modèle > 65%' },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-end justify-between">
        <div>
          <div className="cap mb-1 flex items-center gap-2">
            <span className="pill-dot live-dot" style={{ background: 'var(--accent-500)' }} />
            Mise à jour automatique toutes les 30 secondes
          </div>
          <h1 className="text-[22px] font-semibold tracking-tight">Alertes</h1>
        </div>
        <div className="flex items-center gap-2">
          <button className="btn btn-sm">
            <Icon name="check" size={13} />
            Tout marquer lu
          </button>
          <button className="btn btn-sm">
            <Icon name="download" size={13} />
            Exporter
          </button>
        </div>
      </div>

      <div className="grid grid-cols-3 gap-3">
        {summary.map(c => (
          <button
            key={c.k}
            onClick={() => setTypeFilter(c.k)}
            className="card p-4 text-left hover:border-stone-400"
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

      <div className="flex items-center justify-between">
        <div className="flex items-center gap-1" style={{ borderBottom: '1px solid var(--border)' }}>
          {(
            [
              { id: 'active', label: 'Actives',  n: all.filter(a => !a.resolue).length },
              { id: 'resolu', label: 'Résolues', n: all.filter(a => a.resolue).length },
              { id: 'all',    label: 'Toutes',   n: all.length },
            ] as { id: Tab; label: string; n: number }[]
          ).map(t => (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              className="px-3 py-2 text-[12.5px] flex items-center gap-1.5"
              style={{
                color: tab === t.id ? 'var(--text)' : 'var(--text-3)',
                fontWeight: tab === t.id ? 500 : 400,
                borderBottom: tab === t.id ? '2px solid var(--accent-500)' : '2px solid transparent',
                marginBottom: -1,
              }}
            >
              {t.label} <span className="cap font-mono">{t.n}</span>
            </button>
          ))}
        </div>
        <div className="flex items-center gap-2">
          {typeFilter !== 'Tous' && FILTERABLE_TYPES.includes(typeFilter as AlertType) && (
            <FilterChip
              label="Type"
              value={ALERT_TYPES[typeFilter as AlertType]?.label ?? typeFilter}
              onRemove={() => setTypeFilter('Tous')}
            />
          )}
          <button className="btn btn-sm btn-ghost">
            <Icon name="filter" size={13} />
            Filtres
          </button>
        </div>
      </div>

      <div className="card overflow-hidden">
        {isLoading && (
          <div className="px-4 py-8 text-center cap" style={{ color: 'var(--text-3)' }}>
            Chargement des alertes…
          </div>
        )}
        {isError && (
          <div className="px-4 py-8 text-center cap" style={{ color: 'var(--bad)' }}>
            Impossible de charger les alertes. Vérifiez la connexion au serveur.
          </div>
        )}
        {!isLoading && !isError && filtered.length === 0 && (
          <Empty
            title="Aucune alerte"
            hint="Les alertes générées par le système apparaîtront ici."
          />
        )}
        {!isLoading && !isError && filtered.slice(0, 30).map((a, i) => (
          <AlertRow
            key={a.id}
            alert={a}
            divider={i < Math.min(filtered.length, 30) - 1}
            onResolve={() => resolveMutation.mutate(a.id)}
            resolving={resolveMutation.isPending && resolveMutation.variables === a.id}
          />
        ))}
      </div>
    </div>
  )
}

function AlertRow({
  alert,
  divider,
  onResolve,
  resolving,
}: {
  alert: BackendAlerte
  divider: boolean
  onResolve: () => void
  resolving: boolean
}) {
  const meta = ALERT_TYPES[alert.type as AlertType] ?? { label: alert.type, severity: '', pill: 'neutral' as PillTone }
  const iconName: 'brain' | 'clock' | 'alert' =
    alert.type === 'RisqueEleve'
      ? 'brain'
      : alert.type === 'AbsenceExcessive'
      ? 'clock'
      : 'alert'

  const etudiantName = alert.etudiant
    ? `${alert.etudiant.prenom} ${alert.etudiant.nom}`
    : `Étudiant #${alert.etudiantId}`
  const filiere = alert.etudiant?.filiere ?? '—'

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
          <span className="text-[12.5px] font-medium">{etudiantName}</span>
          <span className="cap font-mono">· {filiere}</span>
          {!alert.resolue && (
            <span className="pill-dot live-dot ml-1" style={{ background: 'var(--bad)' }} />
          )}
        </div>
        <div className="text-[12px] mt-0.5" style={{ color: 'var(--text-2)' }}>
          {alert.message}
        </div>
      </div>
      <div
        className="text-[11.5px] text-right"
        style={{ color: 'var(--text-3)', minWidth: 110 }}
      >
        <div>
          {new Date(alert.creeLe).toLocaleDateString('fr-FR', { day: 'numeric', month: 'short' })}
        </div>
        <div className="cap">
          {new Date(alert.creeLe).toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })}
        </div>
      </div>
      {!alert.resolue ? (
        <div className="flex items-center gap-1.5">
          <button className="btn btn-sm">Voir</button>
          <button
            className="btn btn-sm btn-primary"
            onClick={onResolve}
            disabled={resolving}
          >
            <Icon name="check" size={12} strokeWidth={2.4} />
            {resolving ? '…' : 'Résoudre'}
          </button>
        </div>
      ) : (
        <Pill tone="ok" dot>
          Résolue
        </Pill>
      )}
    </div>
  )
}
