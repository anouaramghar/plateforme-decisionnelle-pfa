import { useState } from 'react'
import { useCopilotReadable, useCopilotAction } from '@copilotkit/react-core'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Pill } from '../components/ui/Pill'
import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { RiskBar } from '../components/ui/RiskBar'
import { SectionHeader } from '../components/ui/SectionHeader'
import { MiniKpi } from '../components/ui/KpiCard'
import { ChartArea, ChartBars, ChartHistogram } from '../components/charts'
import { api } from '../services/api'
import { useFiliere } from '../context/FiliereContext'

const SPARK = [22, 28, 30, 26, 32, 38, 42, 40, 46, 52, 48, 55, 62, 58]

interface ModelMetrics {
  auc?: number
  f1?: number
  precision?: number
  recall?: number
  mae?: number
  r2?: number
  nSamples: number
  modelVersion: string
  trainedAt: string | null
  source: 'computed' | 'metadata'
  dataSource: string
  splitStrategy: string
}

interface MlMetrics {
  risk?: ModelMetrics
  forecast?: ModelMetrics
  auc: number
  f1: number
  precision: number
  recall: number
  nSamples: number
  modelVersion: string
  trainedAt: string | null
  source: 'computed' | 'metadata'
  dataSource?: string
  splitStrategy?: string
}

async function fetchMlMetrics(): Promise<MlMetrics> {
  const res = await api.get<MlMetrics>('/predictions/metrics')
  return res.data
}

// Threshold tier per metric: above the threshold → green, below → orange.
// Picked so a freshly-trained model doesn't paint everything red.
const ML_THRESHOLDS: Record<string, number> = {
  AUC:       0.80,
  'F1-score': 0.75,
  Précision: 0.80,
  Rappel:    0.75,
}

// Real activity feed: backed by AuditEntries via /api/dashboard/activity.
interface ActivityRow {
  id: number
  action: string
  utilisateurNom: string
  message: string
  creeLe: string
}

async function fetchActivity(): Promise<ActivityRow[]> {
  const res = await api.get<ActivityRow[]>('/dashboard/activity?limit=8')
  return res.data
}

const ACTION_ICONS: Record<string, string> = {
  PredictionBatch: '⚡',
  PredictionRun:   '⚡',
  NotePubliee:     '📝',
  DwSync:          '🔄',
  RapportGenere:   '📄',
  AlerteResolue:   '🔔',
  AlerteCreated:   '🔔',
  LoginOk:         '🔑',
  MlRetrain:       '🧠',
}

function formatAction(a: ActivityRow): string {
  switch (a.action) {
    case 'PredictionBatch': return 'Prédiction batch lancée'
    case 'PredictionRun':   return 'Prédiction exécutée'
    case 'NotePubliee':     return 'Note publiée'
    case 'DwSync':          return 'Synchronisation DW exécutée'
    case 'RapportGenere':   return 'Rapport généré'
    case 'AlerteResolue':   return 'Alerte résolue'
    case 'AlerteCreated':   return 'Nouvelle alerte'
    case 'LoginOk':         return 'Connexion réussie'
    case 'MlRetrain':       return 'Modèle ML ré-entraîné'
    default:                return a.action
  }
}

function relativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime()
  const min = Math.floor(diff / 60_000)
  if (min < 1)  return "à l'instant"
  if (min < 60) return `il y a ${min} min`
  const h = Math.floor(min / 60)
  if (h < 24)   return `il y a ${h} h`
  const d = Math.floor(h / 24)
  return d === 1 ? 'hier' : `il y a ${d} j`
}

interface DashboardSummary {
  kpis: {
    nbEtudiants: number
    moyGlobale: number
    tauxReussite: number
    alertesActives: number
    nbEtudiantsDelta: number
    moyGlobaleDelta: number
    tauxReussiteDelta: number
    alertesActivesDelta: number
    compareLabel: string
  }
  notesByFiliere: { filiere: string; moyenne: number; color: string }[]
  moyenneDistribution: { label: string; n: number }[]
  riskBreakdown: { label: string; n: number; color: string }[]
  absenceTrend: { semaine: string; heures: number; nbEtudiants: number }[]
  topARisque: {
    id: number
    nomComplet: string
    matricule: string
    filiere: string
    niveau: string
    moyenne: number
    absences: number
    scoreRisque: number
  }[]
  etudiantsParSemaine: number[]
  nouveauxCetteSemaine: number
  retraitsCetteSemaine: number
}

async function fetchSummary(filiere: string, period = 'S2'): Promise<DashboardSummary> {
  const params = new URLSearchParams({ period })
  if (filiere && filiere !== 'TOUS') params.set('filiere', filiere)
  const res = await api.get<DashboardSummary>(`/dashboard/summary?${params}`)
  return res.data
}

type AbsTrendMode = 'heures' | 'etudiants' | 'pct'
const ABS_MODES: { key: AbsTrendMode; label: string }[] = [
  { key: 'heures',    label: 'Heures'    },
  { key: 'etudiants', label: 'Étudiants' },
  { key: 'pct',       label: '%'         },
]

type Period = '7j' | '30j' | 'S2' | 'Année'
const PERIODS: Period[] = ['7j', '30j', 'S2', 'Année']
const PERIOD_LABELS: Record<Period, string> = {
  '7j':    '7 derniers jours',
  '30j':   '30 derniers jours',
  'S2':    '14 dernières semaines',
  'Année': '12 derniers mois',
}

export default function Dashboard() {
  const navigate = useNavigate()
  const { filiere } = useFiliere()
  const [absTrendMode, setAbsTrendMode] = useState<AbsTrendMode>('heures')
  const [period, setPeriod] = useState<Period>('S2')

  const { data, isLoading, isError, refetch, isFetching } = useQuery({
    queryKey: ['dashboard-summary', filiere, period],
    queryFn: () => fetchSummary(filiere, period),
    refetchInterval: 60_000,
  })

  // Refetch ML metrics less aggressively — they only change on retrain.
  const { data: ml } = useQuery({
    queryKey: ['ml-metrics'],
    queryFn: fetchMlMetrics,
    refetchInterval: 5 * 60_000,
  })

  const { data: activity = [] } = useQuery({
    queryKey: ['dashboard-activity'],
    queryFn: fetchActivity,
    refetchInterval: 30_000,
  })

  const mlMetrics = ml
    ? [
        { l: 'AUC',       v: ml.auc.toFixed(2),       raw: ml.auc },
        { l: 'F1-score',  v: ml.f1.toFixed(2),        raw: ml.f1 },
        { l: 'Précision', v: ml.precision.toFixed(2), raw: ml.precision },
        { l: 'Rappel',    v: ml.recall.toFixed(2),    raw: ml.recall },
      ].map(m => ({
        ...m,
        t: (m.raw >= ML_THRESHOLDS[m.l] ? 'ok' : 'warn') as 'ok' | 'warn',
        bar: Math.round(m.raw * 100),
      }))
    : []

  const filiereLabel = filiere === 'TOUS' ? 'Toutes filières' : `Filière ${filiere}`

  useCopilotReadable({
    description: 'Live dashboard KPIs — total students, at-risk count, average score, active alerts',
    value: data?.kpis
      ? {
          totalStudents: data.kpis.nbEtudiants,
          activeAlerts: data.kpis.alertesActives,
          averageScore: data.kpis.moyGlobale,
          successRate: data.kpis.tauxReussite,
          topAtRisk: data.topARisque.slice(0, 5).map(s => ({
            matricule: s.matricule,
            name: s.nomComplet,
            filiere: s.filiere,
            score: s.scoreRisque,
          })),
        }
      : null,
  })

  useCopilotAction({
    name: 'navigate_to_student',
    description: 'Navigate to the Students page and pre-filter to a specific student by matricule.',
    parameters: [
      {
        name: 'matricule',
        type: 'string',
        description: 'Student matricule, e.g. E10001',
        required: true,
      },
    ],
    handler: async ({ matricule }: { matricule: string }) => {
      navigate(`/students?q=${encodeURIComponent(matricule)}`)
      return `Navigation vers l'étudiant ${matricule}`
    },
  })

  const today = new Date().toLocaleDateString('fr-FR', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  })

  if (isLoading) {
    return (
      <div className="card p-8 text-center" style={{ color: 'var(--text-3)' }}>
        Chargement du tableau de bord…
      </div>
    )
  }

  if (isError || !data) {
    return (
      <div className="card p-8 text-center" style={{ color: 'var(--bad)' }}>
        Impossible de charger les données du tableau de bord.
        <div className="mt-3">
          <button className="btn btn-sm" onClick={() => refetch()}>
            Réessayer
          </button>
        </div>
      </div>
    )
  }

  const k = data.kpis
  const notesByFil = data.notesByFiliere
  const moyDist = data.moyenneDistribution
  const risk = data.riskBreakdown
  const absTrend = data.absenceTrend
  const top = data.topARisque
  const totalRisk = risk.reduce((a, b) => a + b.n, 0)

  return (
    <div className="space-y-6">
      {/* Hero header */}
      <div className="flex items-end justify-between gap-8 pb-2">
        <div className="flex-1">
          <div className="flex items-center gap-2 mb-3">
            <span className="pill pill-accent" style={{ padding: '2px 8px' }}>
              <span className="pill-dot live-dot" style={{ background: 'currentColor' }} />
              {filiereLabel}
            </span>
            <span className="cap">Semestre 2 · 2025/2026</span>
            <span style={{ color: 'var(--text-4)' }}>·</span>
            <span className="cap">{today}</span>
          </div>
          <h1
            className="tracking-tight"
            style={{ fontSize: 32, fontWeight: 500, letterSpacing: '-0.025em', lineHeight: 1.05 }}
          >
            Pilotage{' '}
            <span className="display-serif" style={{ color: 'var(--accent-700)' }}>
              pédagogique
            </span>
          </h1>
          <p
            className="mt-2 max-w-[60ch]"
            style={{ color: 'var(--text-3)', fontSize: 13.5, lineHeight: 1.55 }}
          >
            Vue d'ensemble en temps réel des indicateurs clés, du risque ML, et des actions à
            prioriser cette semaine.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <div
            className="flex items-center rounded-lg overflow-hidden"
            style={{ border: '1px solid var(--border-2)', background: 'var(--surface)' }}
          >
            {PERIODS.map((p, i) => (
              <button
                key={p}
                onClick={() => setPeriod(p)}
                className="px-3 py-1.5 text-[12px] transition"
                style={{
                  background: period === p ? 'var(--text)' : 'transparent',
                  color: period === p ? 'var(--bg)' : 'var(--text-3)',
                  fontWeight: period === p ? 500 : 400,
                  borderRight: i < 3 ? '1px solid var(--border)' : 'none',
                }}
              >
                {p}
              </button>
            ))}
          </div>
          <button
            className="btn btn-sm btn-ghost"
            title="Actualiser"
            onClick={() => refetch()}
            disabled={isFetching}
          >
            <Icon name="refresh" size={13} />
          </button>
          <button className="btn btn-sm">
            <Icon name="download" size={13} />
            Exporter
          </button>
        </div>
      </div>

      {/* Hero metric + supporting KPIs */}
      <div className="grid grid-cols-12 gap-4">
        <div className="card-feature col-span-5 p-6 relative overflow-hidden">
          <div
            className="absolute inset-0 grid-bg pointer-events-none"
            style={{
              maskImage: 'linear-gradient(180deg, transparent 0%, black 60%)',
              WebkitMaskImage: 'linear-gradient(180deg, transparent 0%, black 60%)',
            }}
          />
          <div className="relative">
            <div className="flex items-center justify-between mb-5">
              <div
                className="text-[11px] uppercase tracking-[0.08em] font-medium"
                style={{ color: 'var(--text-3)' }}
              >
                Étudiants suivis
              </div>
              <Pill tone={k.nbEtudiantsDelta >= 0 ? 'ok' : 'warn'}>
                <Icon name="trend" size={11} strokeWidth={2.2} />
                {k.nbEtudiantsDelta >= 0 ? '+' : ''}
                {k.nbEtudiantsDelta.toFixed(1)}%
              </Pill>
            </div>
            <div className="flex items-baseline gap-3">
              <span className="num num-hero">{k.nbEtudiants}</span>
              <span className="display-serif text-[20px]" style={{ color: 'var(--text-3)' }}>
                actifs
              </span>
            </div>
            <div className="mt-5 flex items-end gap-6">
              <div className="flex-1">
                <div className="sparkbar" style={{ height: 38 }}>
                  {(() => {
                    const series = data.etudiantsParSemaine.length > 0
                      ? data.etudiantsParSemaine
                      : SPARK
                    const lo = Math.min(...series)
                    const hi = Math.max(...series)
                    const range = Math.max(1, hi - lo)
                    return series.map((v, i) => (
                      <div
                        key={i}
                        style={{
                          // Normalize to 15–100% so even tiny variations stay visible.
                          height: `${15 + ((v - lo) / range) * 85}%`,
                          background: 'var(--accent-500)',
                          opacity: 0.45 + (i / series.length) * 0.55,
                          borderRadius: 1.5,
                        }}
                      />
                    ))
                  })()}
                </div>
                <div className="mt-2 flex items-center justify-between cap">
                  <span>il y a 14 sem.</span>
                  <span>aujourd'hui</span>
                </div>
              </div>
              <div className="text-right">
                <div className="cap">cette semaine</div>
                <div className="num text-[15px] font-medium" style={{ color: 'var(--ok)' }}>
                  +{data.nouveauxCetteSemaine}
                </div>
                <div className="cap mt-2">retraits</div>
                <div className="num text-[15px] font-medium" style={{ color: 'var(--text-3)' }}>
                  −{data.retraitsCetteSemaine}
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="col-span-7 grid grid-cols-3 gap-4">
          <MiniKpi
            label="Moyenne globale"
            value={k.moyGlobale}
            suffix="/20"
            delta={k.moyGlobaleDelta}
            deltaTone={k.moyGlobaleDelta >= 0 ? 'ok' : 'warn'}
            hint={k.compareLabel}
          />
          <MiniKpi
            label="Taux de réussite"
            value={k.tauxReussite}
            suffix="%"
            delta={k.tauxReussiteDelta}
            deltaTone={k.tauxReussiteDelta >= 0 ? 'ok' : 'warn'}
            hint="modules ≥ 10/20"
          />
          <MiniKpi
            label="Alertes actives"
            value={k.alertesActives}
            delta={k.alertesActivesDelta}
            deltaTone={k.alertesActivesDelta > 0 ? 'bad' : 'ok'}
            hint="vs il y a 7 jours"
            emphasized
          />

          <div className="card p-4 col-span-3">
            <div className="flex items-center justify-between mb-3">
              <div>
                <div className="text-[12.5px] font-medium">Répartition du risque</div>
                <div className="cap">Score ML actuel · {totalRisk} étudiants</div>
              </div>
              <div className="flex items-center gap-3 text-[11px]">
                {risk.map(r => (
                  <span key={r.label} className="flex items-center gap-1.5" style={{ color: 'var(--text-2)' }}>
                    <span style={{ width: 8, height: 8, borderRadius: 2, background: r.color }} />
                    {r.label}
                    <span className="num font-medium" style={{ color: 'var(--text)' }}>
                      {r.n}
                    </span>
                  </span>
                ))}
              </div>
            </div>
            <div className="flex h-2.5 rounded-full overflow-hidden" style={{ background: 'var(--surface-2)' }}>
              {risk.map(r => (
                <div
                  key={r.label}
                  style={{ width: `${(r.n / Math.max(1, totalRisk)) * 100}%`, background: r.color }}
                  title={`${r.label}: ${r.n}`}
                />
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Charts */}
      <div className="grid grid-cols-12 gap-4">
        <div className="card col-span-8 p-5">
          <SectionHeader
            title="Moyennes par filière"
            subtitle="Comparaison /20 — semestre courant"
            right={<Pill tone="neutral" dot>Moyenne</Pill>}
          />
          <ChartBars data={notesByFil} height={232} />
        </div>

        <div className="card col-span-4 p-5">
          <SectionHeader title="Distribution des moyennes" subtitle="Effectifs par tranche /20" />
          <ChartHistogram data={moyDist} height={232} />
        </div>
      </div>

      {/* Absence trend */}
      <div className="card p-5">
        <SectionHeader
          title="Heures d'absence cumulées"
          subtitle={`${PERIOD_LABELS[period]}${filiere !== 'TOUS' ? ` — filière ${filiere}` : ' — toutes filières'}`}
          right={
            <>
              {absTrend.length > 0 && (() => {
                const peak = absTrend.reduce((a, b) => b.heures > a.heures ? b : a)
                return peak.heures > 0 ? <Pill tone="warn" dot>Pic {peak.semaine}</Pill> : null
              })()}
              <div
                className="flex items-center rounded-md overflow-hidden ml-2"
                style={{ border: '1px solid var(--border)' }}
              >
                {ABS_MODES.map((m, i) => (
                  <button
                    key={m.key}
                    onClick={() => setAbsTrendMode(m.key)}
                    className="px-2.5 py-1 text-[11px] transition"
                    style={{
                      background: absTrendMode === m.key ? 'var(--surface-2)' : 'transparent',
                      color: absTrendMode === m.key ? 'var(--text)' : 'var(--text-3)',
                      fontWeight: absTrendMode === m.key ? 500 : 400,
                      borderRight: i < 2 ? '1px solid var(--border)' : 'none',
                    }}
                  >
                    {m.label}
                  </button>
                ))}
              </div>
            </>
          }
        />
        <ChartArea data={absTrend} height={200} mode={absTrendMode} totalEtudiants={k.nbEtudiants} />
      </div>

      {/* Bottom — table + activity */}
      <div className="grid grid-cols-12 gap-4">
        <div className="card col-span-8 overflow-hidden">
          <div className="flex items-end justify-between p-5 pb-4">
            <div>
              <h2 className="text-[15px] font-semibold tracking-tight">Étudiants à surveiller</h2>
              <div className="cap mt-0.5">
                Score de risque ML le plus élevé · mis à jour il y a 2h
              </div>
            </div>
            <button className="btn btn-sm btn-ghost" style={{ color: 'var(--accent-700)' }} onClick={() => navigate('/students')}>
              Voir tous <Icon name="arrowRight" size={12} />
            </button>
          </div>
          <table className="tbl">
            <thead>
              <tr>
                <th>Étudiant</th>
                <th>Filière</th>
                <th>Niveau</th>
                <th>Moy.</th>
                <th>Absences</th>
                <th style={{ minWidth: 140 }}>Score ML</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {top.map(e => (
                <tr key={e.id} style={{ cursor: 'pointer' }} onClick={() => navigate(`/students?q=${encodeURIComponent(e.matricule)}`)}>
                  <td>
                    <div className="flex items-center gap-2.5">
                      <Avatar name={e.nomComplet} size={28} />
                      <div>
                        <div className="font-medium">{e.nomComplet}</div>
                        <div className="cap font-mono">{e.matricule}</div>
                      </div>
                    </div>
                  </td>
                  <td>
                    <Pill tone="neutral">{e.filiere}</Pill>
                  </td>
                  <td className="font-mono">{e.niveau}</td>
                  <td className="num font-medium">{e.moyenne.toFixed(2)}</td>
                  <td className="num">{e.absences}h</td>
                  <td>
                    <RiskBar score={e.scoreRisque} />
                  </td>
                  <td>
                    <button className="btn btn-sm btn-ghost" onClick={ev => { ev.stopPropagation(); navigate(`/students?q=${encodeURIComponent(e.matricule)}`) }}>
                      <Icon name="chevRight" size={13} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="card col-span-4 p-5">
          <SectionHeader title="Activité récente" subtitle="Audit de la plateforme" />
          <div className="space-y-3 relative">
            <div
              className="absolute left-[13px] top-3 bottom-3 w-px"
              style={{ background: 'var(--border)' }}
            />
            {activity.length === 0 ? (
              <div className="cap text-center py-4" style={{ color: 'var(--text-3)' }}>
                Aucune activité récente
              </div>
            ) : (
              activity.map(a => (
                <div key={a.id} className="flex items-start gap-3 relative">
                  <div
                    className="w-7 h-7 rounded-md flex items-center justify-center flex-shrink-0 relative z-10"
                    style={{ background: 'var(--surface-2)', border: '1px solid var(--border)' }}
                  >
                    {ACTION_ICONS[a.action] ?? '•'}
                  </div>
                  <div className="flex-1 min-w-0 pt-0.5">
                    <div className="text-[12.5px] leading-snug">
                      <span className="font-medium">{a.utilisateurNom}</span>{' '}
                      <span style={{ color: 'var(--text-2)' }}>
                        {formatAction(a)}
                        {a.message ? ` — ${a.message}` : ''}
                      </span>
                    </div>
                    <div className="cap mt-1">{relativeTime(a.creeLe)}</div>
                  </div>
                </div>
              ))
            )}
          </div>
          <button
            className="btn btn-sm w-full mt-4"
            style={{ background: 'var(--surface-2)', border: 'none', color: 'var(--text-2)' }}
          >
            Voir le journal complet
          </button>
        </div>
      </div>

      {/* ML model status */}
      <div className="card-feature p-5 flex items-center gap-6 relative overflow-hidden">
        <div className="flex items-center gap-3 min-w-0">
          <div
            className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
            style={{
              background: 'var(--accent-100)',
              color: 'var(--accent-700)',
              border: '1px solid color-mix(in oklch, var(--accent-300) 40%, transparent)',
            }}
          >
            <Icon name="brain" size={18} />
          </div>
          <div className="min-w-0">
            <div className="text-[13px] font-medium flex items-center gap-2">
              XGBoost <span className="font-mono cap">{ml?.modelVersion ?? '…'}</span>
              <Pill tone="ok" dot>En production</Pill>
            </div>
            <div className="cap mt-0.5">
              {ml?.trainedAt
                ? `Ré-entraîné le ${new Date(ml.trainedAt).toLocaleDateString('fr-FR')} · ${
                    ml.source === 'computed'
                      ? 'métriques calculées en direct'
                      : 'métriques stockées'
                  }${ml.nSamples > 0 ? ` · ${ml.nSamples} échantillons` : ''}`
                : 'Modèle non encore entraîné'}
            </div>
          </div>
        </div>
        <div style={{ width: 1, height: 36, background: 'var(--border)' }} />
        <div className="flex-1 grid grid-cols-4 gap-6">
          {(mlMetrics.length > 0 ? mlMetrics : [
            { l: 'AUC',       v: '—', t: 'warn' as const, bar: 0 },
            { l: 'F1-score',  v: '—', t: 'warn' as const, bar: 0 },
            { l: 'Précision', v: '—', t: 'warn' as const, bar: 0 },
            { l: 'Rappel',    v: '—', t: 'warn' as const, bar: 0 },
          ]).map(m => (
            <div key={m.l}>
              <div className="cap mb-1">{m.l}</div>
              <div
                className="num text-[20px] font-medium"
                style={{ color: m.t === 'ok' ? 'var(--ok)' : 'var(--warn)' }}
              >
                {m.v}
              </div>
              <div className="mt-1.5 h-[3px] rounded-full" style={{ background: 'var(--surface-2)' }}>
                <div
                  style={{
                    width: `${m.bar}%`,
                    height: '100%',
                    background: m.t === 'ok' ? 'var(--ok)' : 'var(--warn)',
                    borderRadius: 999,
                  }}
                />
              </div>
            </div>
          ))}
        </div>
        <div className="flex items-center gap-2">
          <button className="btn btn-sm">Voir les features</button>
          <button className="btn btn-sm btn-primary">Ré-entraîner</button>
        </div>
      </div>
    </div>
  )
}
