import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { Pill, type PillTone } from '../components/ui/Pill'
import { Empty } from '../components/ui/Empty'
import {
  fetchTriage, dismissSignal, createCase,
  PRIORITE_LABELS, type TriageGroup,
} from '../services/interventions'

const NIVEAU_TONE: Record<string, PillTone> = {
  Critique: 'bad', Eleve: 'bad', Moyen: 'warn', Faible: 'neutral',
}

// Risk score (0–1) → pill colour, same thresholds as the backend priority blend.
const riskTone = (s: number): PillTone => (s >= 0.6 ? 'bad' : s >= 0.35 ? 'warn' : 'neutral')

export default function Triage() {
  const navigate = useNavigate()
  const qc = useQueryClient()

  const { data: groups = [], isLoading, isError } = useQuery({
    queryKey: ['triage'],
    queryFn: fetchTriage,
    refetchInterval: 30_000,
  })

  const openCase = useMutation({
    mutationFn: (g: TriageGroup) => createCase({
      etudiantId: g.etudiantId,
      motif: g.resume,
      priorite: g.suggestedPriorite,
      alerteId: g.signals[0]?.id,
    }),
    onSuccess: async (created) => {
      await qc.invalidateQueries({ queryKey: ['triage'] })
      navigate(`/cases/${created.id}`)
    },
  })

  const dismiss = useMutation({
    mutationFn: ({ id, raison }: { id: number; raison: string }) => dismissSignal(id, raison),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['triage'] }),
  })

  const onDismiss = (signalId: number) => {
    const raison = window.prompt('Raison du rejet de ce signal :')
    if (raison && raison.trim()) dismiss.mutate({ id: signalId, raison: raison.trim() })
  }

  return (
    <div className="space-y-4">
      <div>
        <div className="cap mb-1 flex items-center gap-2">
          <span className="pill-dot live-dot" style={{ background: 'var(--accent-500)' }} />
          Classée par risque prédit — top 50, actualisée toutes les 30 s
        </div>
        <h1 className="text-[22px] font-semibold tracking-tight">Triage des signaux</h1>
      </div>

      <div className="card overflow-hidden">
        {isLoading && (
          <div className="px-4 py-8 text-center cap" style={{ color: 'var(--text-3)' }}>
            Chargement de la file…
          </div>
        )}
        {isError && (
          <div className="px-4 py-8 text-center cap" style={{ color: 'var(--bad)' }}>
            Impossible de charger la file de triage.
          </div>
        )}
        {!isLoading && !isError && groups.length === 0 && (
          <Empty title="File vide" hint="Aucun signal en attente de triage." />
        )}
        {!isLoading && !isError && groups.map((g, i) => (
          <div
            key={g.etudiantId}
            className="px-4 py-3"
            style={{ borderBottom: i < groups.length - 1 ? '1px solid var(--border)' : 'none' }}
          >
            <div className="flex items-center gap-3">
              <Pill tone={riskTone(g.scoreRisque)} dot>
                {Math.round(g.scoreRisque * 100)}%
              </Pill>
              <Pill tone={NIVEAU_TONE[g.maxNiveau] ?? 'neutral'}>
                {PRIORITE_LABELS[g.suggestedPriorite] ?? g.suggestedPriorite}
              </Pill>
              <button
                className="text-[13px] font-medium hover:underline"
                onClick={() => navigate(`/students/${g.etudiantId}`)}
              >
                {g.etudiantNom || `Étudiant #${g.etudiantId}`}
              </button>
              <span className="cap font-mono">· {g.matricule}</span>
              <span className="cap">
                · {g.moyenne != null ? `moyenne ${g.moyenne}` : 'aucune note'}
                {g.absencesH > 0 && ` · ${g.absencesH}h abs.`}
              </span>
              <div className="flex-1" />
              {g.openCaseId ? (
                <button className="btn btn-sm" onClick={() => navigate(`/cases/${g.openCaseId}`)}>
                  <Icon name="eye" size={13} />
                  Cas ouvert #{g.openCaseId}
                </button>
              ) : (
                <button
                  className="btn btn-sm btn-primary"
                  onClick={() => openCase.mutate(g)}
                  disabled={openCase.isPending}
                >
                  <Icon name="plus" size={13} />
                  Ouvrir un cas
                </button>
              )}
            </div>

            <div className="mt-2 pl-1 space-y-1">
              {g.signals.map(s => (
                <div key={s.id} className="flex items-center gap-2 text-[12px]" style={{ color: 'var(--text-2)' }}>
                  <Pill tone={NIVEAU_TONE[s.niveau] ?? 'neutral'}>{s.type}</Pill>
                  <span className="truncate">{s.message}</span>
                  <span className="cap">
                    {new Date(s.creeLe).toLocaleDateString('fr-FR', { day: 'numeric', month: 'short' })}
                  </span>
                  <button
                    className="btn btn-sm btn-ghost ml-auto"
                    onClick={() => onDismiss(s.id)}
                    title="Rejeter ce signal (raison obligatoire)"
                  >
                    <Icon name="x" size={12} />
                    Rejeter
                  </button>
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
