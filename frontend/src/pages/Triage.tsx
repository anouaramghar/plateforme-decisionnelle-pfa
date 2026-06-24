import { useState } from 'react'
import type { AxiosError } from 'axios'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { Pill, type PillTone } from '../components/ui/Pill'
import { Empty } from '../components/ui/Empty'
import { Modal } from '../components/ui/Modal'
import {
  fetchTriage, dismissSignal, createCase,
  PRIORITE_LABELS, type TriageGroup,
} from '../services/interventions'

const NIVEAU_TONE: Record<string, PillTone> = {
  Critique: 'bad', Eleve: 'bad', Moyen: 'warn', Faible: 'neutral',
}

// Risk score (0–1) → pill colour, same thresholds as the backend priority blend.
const riskTone = (s: number): PillTone => (s >= 0.6 ? 'bad' : s >= 0.35 ? 'warn' : 'neutral')

export default function Triage({ embedded = false }: { embedded?: boolean } = {}) {
  const navigate = useNavigate()
  const qc = useQueryClient()

  const { data: groups = [], isLoading, isError } = useQuery({
    queryKey: ['triage'],
    queryFn: fetchTriage,
    refetchInterval: 30_000,
  })

  const [startError, setStartError] = useState<string | null>(null)

  const openCase = useMutation({
    mutationFn: (g: TriageGroup) => createCase({
      etudiantId: g.etudiantId,
      motif: g.resume,
      priorite: g.suggestedPriorite,
      alerteId: g.signals[0]?.id,
    }),
    onSuccess: async (created) => {
      await qc.invalidateQueries({ queryKey: ['triage'] })
      await qc.invalidateQueries({ queryKey: ['alertes'] })
      navigate(`/cases/${created.id}`)
    },
    onError: (error: AxiosError<{ existingCaseId?: number }>) => {
      const existing = error.response?.data.existingCaseId
      if (error.response?.status === 409 && existing) navigate(`/cases/${existing}`)
      else setStartError('Impossible de démarrer cette intervention.')
    },
  })

  const [dismissId, setDismissId] = useState<number | null>(null)
  const [raison, setRaison] = useState('')

  const dismiss = useMutation({
    mutationFn: ({ id, raison: r }: { id: number; raison: string }) => dismissSignal(id, r),
    onSuccess: () => {
      setDismissId(null)
      qc.invalidateQueries({ queryKey: ['triage'] })
      qc.invalidateQueries({ queryKey: ['alertes'] })
    },
  })

  const onDismiss = (signalId: number) => { setRaison(''); setDismissId(signalId) }

  return (
    <div className="space-y-4">
      <div>
        <div className="cap mb-1 flex items-center gap-2">
          <span className="pill-dot live-dot" style={{ background: 'var(--accent-500)' }} />
          Classée par risque prédit — top 50, actualisée toutes les 30 s
        </div>
        {!embedded && <h1 className="text-[22px] font-semibold tracking-tight">Triage des signaux</h1>}
      </div>

      {startError && (
        <div className="card px-4 py-2 text-[13px]" style={{ color: 'var(--bad)' }} role="alert">
          {startError}
        </div>
      )}

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
          <Empty title="File vide" hint="Aucun signal en attente de triage." icon="bell" />
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

      <Modal open={dismissId !== null} onClose={() => setDismissId(null)} title="Rejeter le signal">
        <label className="flex flex-col gap-1">
          <span className="cap">Raison du rejet</span>
          <textarea
            className="input"
            rows={3}
            value={raison}
            onChange={e => setRaison(e.target.value)}
            placeholder="Pourquoi ce signal ne nécessite pas de cas…"
            autoFocus
          />
        </label>
        <div className="flex justify-end gap-2 mt-4">
          <button className="btn btn-sm" onClick={() => setDismissId(null)}>Annuler</button>
          <button
            className="btn btn-sm btn-primary"
            onClick={() => dismissId !== null && raison.trim() && dismiss.mutate({ id: dismissId, raison: raison.trim() })}
            disabled={dismiss.isPending || !raison.trim()}
          >
            Rejeter
          </button>
        </div>
      </Modal>
    </div>
  )
}
