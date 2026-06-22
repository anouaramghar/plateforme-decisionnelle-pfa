import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCopilotReadable, useCopilotAction } from '@copilotkit/react-core'
import { useNavigate } from 'react-router-dom'
import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { Pill } from '../components/ui/Pill'
import { KpiCard } from '../components/ui/KpiCard'
import { RiskBar } from '../components/ui/RiskBar'
import { SectionHeader } from '../components/ui/SectionHeader'
import { Select } from '../components/ui/Select'
import { ChartScatter } from '../components/charts'
import { api } from '../services/api'
import { ConfirmCard } from '../components/copilot/ConfirmCard'
import type { ConfirmData } from '../components/copilot/ConfirmCard'
import { useAuth } from '../context/AuthContext'

const FILIERES = ['TOUS', 'TCP', 'GI', 'IA', 'ROC', 'IRSI']
const NIVEAUX  = ['Toutes', 'CP1', 'CP2', 'CI1', 'CI2', 'CI3']

type Risque = 'faible' | 'modere' | 'eleve'

interface PredictionsSummary {
  kpis: { evalues: number; risqueEleve: number; risqueModere: number; auc: number }
  scatter: { x: number; y: number; r: Risque }[]
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
  runs: {
    id: string
    date: string
    filiere: string
    etudiants: number
    risqueEleve: number
    modele: string
    auc: number
    statut: string
  }[]
}

interface BatchResult {
  total: number
  predicted: number
  failed: number
  skipped: number
  risqueEleve: number
}

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

async function fetchSummary(): Promise<PredictionsSummary> {
  const res = await api.get<PredictionsSummary>('/predictions/summary')
  return res.data
}

async function fetchMlMetrics(): Promise<MlMetrics> {
  const res = await api.get<MlMetrics>('/predictions/metrics')
  return res.data
}

async function runBatch(input: { filiereCode?: string; niveau?: string }): Promise<BatchResult> {
  const res = await api.post<BatchResult>('/predictions/batch', input)
  return res.data
}

function EnseignantRestricted() {
  const navigate = useNavigate()
  return (
    <div className="flex flex-col items-center justify-center py-24 gap-5 text-center">
      <div
        style={{
          width: 64,
          height: 64,
          borderRadius: 16,
          background: 'color-mix(in oklch, var(--accent-500) 12%, transparent)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Icon name="brain" size={30} style={{ color: 'var(--accent-500)' }} />
      </div>
      <div>
        <h2 className="text-[18px] font-semibold mb-1.5">Prédictions ML non disponibles</h2>
        <p className="cap max-w-sm" style={{ lineHeight: 1.6 }}>
          Le moteur de prédiction par intelligence artificielle est réservé aux rôles
          <strong> Administrateur</strong> et <strong>Responsable</strong>.
          En tant qu'enseignant, vous pouvez saisir notes et absences depuis votre espace dédié.
        </p>
      </div>
      <button className="btn btn-accent" onClick={() => navigate('/enseignant')}>
        <Icon name="doc" size={14} />
        Aller à mon espace
      </button>
    </div>
  )
}

// Renders the persisted draft returned by the backend `draft_alert` tool and
// promotes/cancels it through the server-side draft lifecycle. The draft_id is
// real (the backend already wrote a pending_user_confirm row) so confirm/cancel
// hit /copilot/drafts/{id}/{confirm|cancel} — never the Admin-only /alertes.
function DraftAlertCard({ result }: { result: string }) {
  const [state, setState] = useState<'pending' | 'confirmed' | 'dismissed'>('pending')

  let parsed: {
    ok?: boolean
    data?: { draft_id: number; student_name?: string; matricule?: string; severity?: string; message?: string; expires_at?: string }
  } | null = null
  try { parsed = JSON.parse(result) } catch { parsed = null }

  if (!parsed?.ok || !parsed.data) {
    return (
      <div className="px-3 py-2 rounded-lg text-[12px] cap" style={{ background: 'var(--surface-2)', color: 'var(--bad)', border: '1px solid var(--border)' }}>
        Échec de la préparation du brouillon d'alerte.
      </div>
    )
  }

  const d = parsed.data
  const confirmData: ConfirmData = {
    draftId: d.draft_id,
    preview: {
      student_name: d.student_name,
      matricule: d.matricule,
      severity: d.severity,
      message: d.message,
      expires_at: d.expires_at,
    },
    tool: 'draft_alert',
    state,
  }

  return (
    <ConfirmCard
      data={confirmData}
      onConfirm={async () => {
        await api.post(`/copilot/drafts/${d.draft_id}/confirm`)
        setState('confirmed')
      }}
      onDismiss={async () => {
        try { await api.post(`/copilot/drafts/${d.draft_id}/cancel`) } catch { /* draft may already be expired/finalized */ }
        setState('dismissed')
      }}
    />
  )
}

export default function Predictions() {
  const { user } = useAuth()
  const [filiere, setFiliere] = useState('TOUS')
  const [niveau, setNiveau] = useState('Toutes')
  const [highlightedMatricule, setHighlightedMatricule] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['predictions-summary'],
    queryFn: fetchSummary,
    refetchInterval: 60_000,
  })

  const { data: ml } = useQuery({
    queryKey: ['ml-metrics'],
    queryFn: fetchMlMetrics,
    refetchInterval: 5 * 60_000,
  })

  const batchMutation = useMutation({
    mutationFn: () =>
      runBatch({
        filiereCode: filiere === 'TOUS' ? undefined : filiere,
        niveau: niveau === 'Toutes' ? undefined : niveau,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['predictions-summary'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] })
      queryClient.invalidateQueries({ queryKey: ['etudiants-with-stats'] })
      queryClient.invalidateQueries({ queryKey: ['alertes'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard-activity'] })
    },
  })

  useCopilotReadable({
    description: 'Top at-risk students on the predictions page — matricule, name, filière, niveau, risk score',
    value: data?.topARisque.map(s => ({
      matricule: s.matricule,
      name: s.nomComplet,
      filiere: s.filiere,
      niveau: s.niveau,
      averageScore: s.moyenne,
      absences: s.absences,
      riskScore: s.scoreRisque,
    })) ?? [],
  })

  useCopilotReadable({
    description: 'Current ML model metrics — AUC, F1, precision, recall',
    value: ml ? { auc: ml.auc, f1: ml.f1, precision: ml.precision, recall: ml.recall } : null,
  })

  useCopilotAction({
    name: 'highlight_student',
    description: 'Highlight a specific student row in the top-at-risk table by matricule.',
    parameters: [
      { name: 'matricule', type: 'string', description: 'Student matricule to highlight.', required: true },
    ],
    handler: async ({ matricule }: { matricule: string }) => {
      setHighlightedMatricule(matricule)
      const found = data?.topARisque.find(s => s.matricule === matricule)
      if (!found) return `Étudiant ${matricule} absent du tableau`
      setTimeout(() => {
        document.querySelector(`[data-matricule="${matricule}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
      }, 100)
      return `${found.nomComplet} mis en évidence (score: ${found.scoreRisque.toFixed(2)})`
    },
  })

  // Render-only. The runtime `draft_alert` tool persists the draft server-side
  // and returns the real draft_id; we must NOT register a handler here (that
  // would shadow the backend tool, fabricate a fake id, and bypass the
  // persisted-draft lifecycle). We only render its result and drive
  // confirm/cancel through the /copilot/drafts endpoints.
  useCopilotAction({
    name: 'draft_alert',
    available: 'disabled',
    render: ({ status, result }: { status: string; result?: string }) => {
      if (status !== 'complete' || !result) {
        return (
          <div className="px-3 py-2 rounded-lg text-[12px] cap" style={{ background: 'var(--surface-2)', color: 'var(--text-3)', border: '1px solid var(--border)' }}>
            Préparation du brouillon d'alerte…
          </div>
        )
      }
      return <DraftAlertCard result={result} />
    },
  })

  // Retrain triggers a fresh training run on the ML service. May take a while
  // — TanStack Query keeps the spinner up until the proxy returns.
  const retrainMutation = useMutation({
    mutationFn: async () => (await api.post('/predictions/retrain')).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['ml-metrics'] })
      queryClient.invalidateQueries({ queryKey: ['predictions-summary'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard-activity'] })
    },
  })

  if (user?.role === 'Enseignant') return <EnseignantRestricted />

  if (isLoading) {
    return (
      <div className="card p-8 text-center" style={{ color: 'var(--text-3)' }}>
        Chargement…
      </div>
    )
  }
  if (isError || !data) {
    return (
      <div className="card p-8 text-center" style={{ color: 'var(--bad)' }}>
        Impossible de charger les prédictions.
        <div className="mt-3">
          <button className="btn btn-sm" onClick={() => refetch()}>Réessayer</button>
        </div>
      </div>
    )
  }

  const { kpis, scatter, topARisque, runs } = data
  const evaluesPct = (n: number) =>
    kpis.evalues > 0 ? `(${Math.round((n / kpis.evalues) * 100)}%)` : ''

  const trainedAt = ml?.risk?.trainedAt ?? ml?.trainedAt

  return (
    <div className="space-y-4">
      {(ml?.risk?.dataSource === 'synthetic' || ml?.forecast?.dataSource === 'synthetic' || ml?.dataSource === 'synthetic') && (
        <div
          className="card p-3 text-[12.5px] flex items-center gap-3 mb-4"
          style={{ background: 'var(--accent-50)', borderColor: 'var(--accent-300)', color: 'var(--accent-700)' }}
        >
          <Icon name="warning" size={14} />
          <span>
            Modèle de démonstration entraîné sur des données synthétiques — ne pas utiliser pour une décision pédagogique.
          </span>
        </div>
      )}

      <div className="flex items-end justify-between">
        <div>
          <div className="cap mb-1">
            Modèle XGBoost {ml?.risk?.modelVersion ?? ml?.modelVersion ?? '…'} · AUC {ml?.risk?.auc?.toFixed(2) ?? ml?.auc?.toFixed(2) ?? '—'}
            {trainedAt
              ? ` · ré-entraîné ${new Date(trainedAt).toLocaleDateString('fr-FR')}`
              : ''}
          </div>
          <h1 className="text-[22px] font-semibold tracking-tight">Prédictions ML</h1>
        </div>
        <div className="flex items-center gap-2">
          <button
            className="btn btn-sm"
            onClick={() => retrainMutation.mutate()}
            disabled={retrainMutation.isPending}
            title="Relance l'entraînement et atomically swap les artefacts"
          >
            <Icon name="refresh" size={13} />
            {retrainMutation.isPending ? 'Ré-entraînement…' : 'Ré-entraîner'}
          </button>
          <button
            className="btn btn-sm btn-accent"
            onClick={() => batchMutation.mutate()}
            disabled={batchMutation.isPending}
          >
            <Icon name="spark" size={13} />
            {batchMutation.isPending ? 'En cours…' : 'Lancer une prédiction batch'}
          </button>
        </div>
      </div>

      {batchMutation.data && (
        <div
          className="card p-3 text-[12.5px] flex items-center gap-3"
          style={{ background: 'var(--surface-2)' }}
        >
          <Icon name="spark" size={14} />
          <span>
            Batch terminé — {batchMutation.data.predicted}/{batchMutation.data.total} prédits
            {batchMutation.data.skipped > 0 && ` · ${batchMutation.data.skipped} sans données`}
            {batchMutation.data.failed > 0 && ` · ${batchMutation.data.failed} échecs`}
            {batchMutation.data.risqueEleve > 0 &&
              ` · ${batchMutation.data.risqueEleve} en risque élevé`}
          </span>
        </div>
      )}
      {batchMutation.isError && (
        <div className="card p-3 text-[12.5px]" style={{ color: 'var(--bad)' }}>
          Échec du batch — vérifiez que le service ML est joignable.
        </div>
      )}

      <div
        className="card p-5"
        style={{
          background: 'linear-gradient(135deg, var(--surface) 0%, var(--accent-50) 100%)',
          borderColor: 'var(--accent-200)',
        }}
      >
        <div className="flex items-center gap-5">
          <div
            className="w-12 h-12 rounded-xl flex items-center justify-center"
            style={{
              background: 'linear-gradient(135deg, var(--accent-500), var(--accent-700))',
              color: '#fff',
              boxShadow:
                'inset 0 1px 0 rgba(255,255,255,.2), 0 8px 24px -8px rgba(249,115,22,.5)',
            }}
          >
            <Icon name="brain" size={22} />
          </div>
          <div className="flex-1">
            <div className="text-[14px] font-semibold tracking-tight">
              Évaluer le risque d'une cohorte
            </div>
            <div className="text-[12px] mt-1" style={{ color: 'var(--text-2)' }}>
              Sélectionnez la filière puis le niveau. Le service ML calcule un score 0–1 et
              déclenche les alertes pour les étudiants au-dessus du seuil.
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Select value={filiere} onChange={setFiliere} options={FILIERES} label="Filière" />
            <Select value={niveau} onChange={setNiveau} options={NIVEAUX} label="Niveau" />
            <button
              className="btn btn-accent"
              onClick={() => batchMutation.mutate()}
              disabled={batchMutation.isPending}
            >
              {batchMutation.isPending ? 'En cours…' : 'Lancer'}
            </button>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-4 gap-3">
        <KpiCard label="Étudiants évalués" value={kpis.evalues} hint="Cohorte courante" />
        <KpiCard
          label="Risque élevé"
          value={kpis.risqueEleve}
          suffix={evaluesPct(kpis.risqueEleve)}
        />
        <KpiCard
          label="Risque modéré"
          value={kpis.risqueModere}
          suffix={evaluesPct(kpis.risqueModere)}
        />
        <KpiCard
          label="AUC du modèle"
          value={ml?.auc?.toFixed(2) ?? kpis.auc.toFixed(2)}
          hint={ml?.modelVersion ? `Version ${ml.modelVersion}` : 'Modèle non chargé'}
        />
      </div>

      <div className="grid grid-cols-3 gap-3">
        <div className="card p-4 col-span-2">
          <SectionHeader
            title="Cartographie du risque"
            subtitle="Moyenne /20 vs heures d'absence — chaque point = 1 étudiant"
            right={
              <>
                <Pill tone="ok" dot>Faible</Pill>
                <Pill tone="warn" dot>Modéré</Pill>
                <Pill tone="bad" dot>Élevé</Pill>
              </>
            }
          />
          <ChartScatter points={scatter} height={300} />
        </div>
        <div className="card p-4">
          <SectionHeader title="Top à risque" subtitle="Score le plus élevé" />
          <div className="space-y-2.5">
            {topARisque.length === 0 ? (
              <div className="cap text-center py-4">Aucune donnée</div>
            ) : (
              topARisque.map((e, i) => (
                <div
                  key={e.id}
                  data-matricule={e.matricule}
                  className="flex items-center gap-2.5"
                  style={highlightedMatricule === e.matricule
                    ? { background: 'color-mix(in oklch, var(--accent-500) 10%, transparent)', borderRadius: 6, padding: '2px 4px' }
                    : undefined}
                >
                  <span className="num text-[11px] w-5" style={{ color: 'var(--text-4)' }}>
                    {String(i + 1).padStart(2, '0')}
                  </span>
                  <Avatar name={e.nomComplet} size={26} />
                  <div className="flex-1 min-w-0">
                    <div className="text-[12.5px] font-medium truncate">{e.nomComplet}</div>
                    <div className="cap font-mono">
                      {e.filiere} · {e.niveau}
                    </div>
                  </div>
                  <RiskBar score={e.scoreRisque} width={50} />
                </div>
              ))
            )}
          </div>
          <button
            className="btn btn-sm w-full mt-3"
            style={{ background: 'var(--surface-2)', border: 'none' }}
          >
            Générer le rapport · {topARisque.length} étudiants
          </button>
        </div>
      </div>

      <div className="card overflow-hidden">
        <div
          className="px-4 py-3 flex items-center justify-between"
          style={{ borderBottom: '1px solid var(--border)' }}
        >
          <div>
            <div className="text-[13px] font-semibold">Historique des prédictions</div>
            <div className="cap mt-0.5">
              Regroupé par jour × filière — 10 derniers lots
            </div>
          </div>
          <button className="btn btn-sm btn-ghost">
            Voir tout <Icon name="arrowRight" size={12} />
          </button>
        </div>
        <table className="tbl">
          <thead>
            <tr>
              <th>ID</th>
              <th>Date</th>
              <th>Filière</th>
              <th>Étudiants</th>
              <th>Risque élevé</th>
              <th>Modèle</th>
              <th>AUC</th>
              <th>Statut</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {runs.length === 0 ? (
              <tr>
                <td colSpan={9} className="cap text-center py-6">
                  Aucun lot encore exécuté — lancez une prédiction batch ci-dessus.
                </td>
              </tr>
            ) : (
              runs.map(r => (
                <tr key={r.id}>
                  <td className="font-mono cap">{r.id}</td>
                  <td>{r.date}</td>
                  <td>
                    <Pill tone="neutral">{r.filiere}</Pill>
                  </td>
                  <td className="num">{r.etudiants}</td>
                  <td className="num font-medium" style={{ color: 'var(--bad)' }}>
                    {r.risqueEleve}
                  </td>
                  <td className="font-mono cap">{r.modele}</td>
                  <td className="num">{r.auc.toFixed(2)}</td>
                  <td>
                    {r.statut === 'OK' ? (
                      <Pill tone="ok" dot>OK</Pill>
                    ) : (
                      <Pill tone="warn" dot>Dégradé</Pill>
                    )}
                  </td>
                  <td>
                    <button className="btn btn-sm btn-ghost">
                      <Icon name="more" size={14} />
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
