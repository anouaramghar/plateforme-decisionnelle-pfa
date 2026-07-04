import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { Pill, type PillTone } from '../components/ui/Pill'
import { Empty } from '../components/ui/Empty'
import { SkeletonRows } from '../components/ui/Skeleton'
import {
  fetchCases, fetchFilieres, ETAT_LABELS, PRIORITE_LABELS,
  type InterventionCase,
} from '../services/interventions'

const PRIORITE_TONE: Record<string, PillTone> = {
  Critical: 'bad', High: 'bad', Medium: 'warn', Low: 'neutral',
}

const PRIMARY_STAGES = [
  { key: 'new',      label: 'Nouveau' },
  { key: 'contact',  label: 'A contacter' },
  { key: 'meeting',  label: 'Rendez-vous' },
  { key: 'resolved', label: 'Resolu' },
] as const

type StageKey = typeof PRIMARY_STAGES[number]['key']
type OwnerFilter = 'all' | 'assigned' | 'unassigned'
type StageFilter = 'all' | StageKey

function stageFor(c: InterventionCase): StageKey {
  if (c.etat === 'Resolved' || c.etat === 'Closed') return 'resolved'
  if (c.etat === 'WaitingStudent' || (c.meetingScheduledFor && !c.meetingAttendance)) return 'meeting'
  if (c.etat === 'Open' && c.ownerId == null) return 'new'
  return 'contact'
}

export default function Cases() {
  const navigate = useNavigate()

  const { data: cases = [], isLoading, isError } = useQuery({
    queryKey: ['cases'],
    queryFn: () => fetchCases(),
    refetchInterval: 30_000,
  })

  const { data: filieres = [] } = useQuery({
    queryKey: ['filieres'],
    queryFn: fetchFilieres,
    staleTime: 5 * 60_000,
  })

  const [owner, setOwner] = useState<OwnerFilter>('all')
  const [filiereCode, setFiliereCode] = useState<string>('all')
  const [priorite, setPriorite] = useState<string>('all')
  const [stage, setStage] = useState<StageFilter>('all')

  const filiereIdByCode = useMemo(() => {
    const m = new Map<string, number>()
    for (const f of filieres) m.set(f.code, f.id)
    return m
  }, [filieres])

  const passCommon = (c: InterventionCase) => {
    if (owner === 'assigned' && c.ownerId == null) return false
    if (owner === 'unassigned' && c.ownerId != null) return false
    if (priorite !== 'all' && c.priorite !== priorite) return false
    if (filiereCode !== 'all') {
      const wantId = filiereIdByCode.get(filiereCode)
      if (wantId == null || c.filiereId !== wantId) return false
    }
    return true
  }

  const primaryColumns = PRIMARY_STAGES.map(s => ({
    ...s,
    items: cases
      .filter(c => stageFor(c) === s.key && passCommon(c) && (stage === 'all' || stage === s.key))
      .sort((a, b) => +new Date(b.creeLe) - +new Date(a.creeLe)),
  }))

  return (
    <div className="space-y-4">
      <div>
        <div className="cap mb-1">Suivi des interventions</div>
        <h1 className="text-[22px] font-semibold tracking-tight">Interventions</h1>
      </div>

      <div className="card p-3 flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1">
          <span className="cap">Assigné</span>
          <select className="input" value={owner} onChange={e => setOwner(e.target.value as OwnerFilter)}>
            <option value="all">Tous</option>
            <option value="assigned">Assignés</option>
            <option value="unassigned">Non assignés</option>
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="cap">Filière</span>
          <select className="input" value={filiereCode} onChange={e => setFiliereCode(e.target.value)}>
            <option value="all">Toutes</option>
            {filieres.map(f => <option key={f.id} value={f.code}>{f.code}</option>)}
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="cap">Priorité</span>
          <select className="input" value={priorite} onChange={e => setPriorite(e.target.value)}>
            <option value="all">Toutes</option>
            {Object.entries(PRIORITE_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="cap">Étape</span>
          <select className="input" value={stage} onChange={e => setStage(e.target.value as StageFilter)}>
            <option value="all">Toutes les étapes</option>
            {PRIMARY_STAGES.map(s => <option key={s.key} value={s.key}>{s.label}</option>)}
          </select>
        </label>
        {(owner !== 'all' || filiereCode !== 'all' || priorite !== 'all' || stage !== 'all') && (
          <button
            className="btn btn-sm btn-ghost"
            onClick={() => { setOwner('all'); setFiliereCode('all'); setPriorite('all'); setStage('all') }}
          >
            <Icon name="x" size={12} /> Réinitialiser
          </button>
        )}
      </div>

      {isError && (
        <div className="card px-4 py-8 text-center cap" style={{ color: 'var(--bad)' }}>
          Impossible de charger les interventions.
        </div>
      )}

      <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
        {primaryColumns.map(col => (
          <section
            key={col.key}
            role="region"
            aria-label={col.label}
            className="card min-w-0 overflow-hidden flex flex-col"
            style={{ minHeight: 200 }}
          >
            <div className="px-3 py-2.5 flex items-center justify-between" style={{ borderBottom: '1px solid var(--border)' }}>
              <h2 className="text-[13px] font-semibold">{col.label}</h2>
              <span className="cap font-mono">{col.items.length}</span>
            </div>
            <div className="flex-1">
              {isLoading && <SkeletonRows rows={3} />}
              {!isLoading && col.items.length === 0 && (
                <div className="px-3 py-6 cap text-center" style={{ color: 'var(--text-3)' }}>Aucune intervention.</div>
              )}
              {col.items.map(c => (
                <CaseCard key={c.id} c={c} onClick={() => navigate(`/cases/${c.id}`)} />
              ))}
            </div>
          </section>
        ))}
      </div>

      {!isLoading && !isError && cases.length === 0 && (
        <Empty title="Aucune intervention" hint="Les cas ouverts depuis la file de triage apparaîtront ici." icon="bookmark" />
      )}
    </div>
  )
}

function CaseCard({ c, onClick }: { c: InterventionCase; onClick: () => void }) {
  const student = c.etudiant ? `${c.etudiant.prenom} ${c.etudiant.nom}` : `Étudiant #${c.etudiantId}`
  const meeting = c.meetingScheduledFor
  const due = c.dueDate
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={`Ouvrir le cas de ${student} : ${c.motif}`}
      className="w-full px-3 py-3 text-left hover:bg-[var(--surface-2)]"
      style={{ borderBottom: '1px solid var(--border)' }}
    >
      <div className="mb-2 flex items-center justify-between gap-2">
        <Pill tone={PRIORITE_TONE[c.priorite] ?? 'neutral'} dot>
          {PRIORITE_LABELS[c.priorite] ?? c.priorite}
        </Pill>
        <Pill tone="info">{ETAT_LABELS[c.etat] ?? c.etat}</Pill>
      </div>
      <div className="min-w-0">
        <div className="line-clamp-2 text-[12.5px] font-medium" title={c.motif}>{c.motif}</div>
        <div className="cap mt-1 truncate" title={student}>{student}{c.ownerId == null && ' · non assigné'}</div>
        {meeting && (
          <div className="cap" style={{ color: 'var(--text-3)' }}>
            <Icon name="clock" size={11} /> {new Date(meeting).toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' })}
            {c.meetingLocation && ` · ${c.meetingLocation}`}
          </div>
        )}
        {!meeting && due && (
          <div className="cap" style={{ color: 'var(--text-3)' }}>
            échéance {new Date(due).toLocaleDateString('fr-FR', { day: 'numeric', month: 'short' })}
          </div>
        )}
      </div>
    </button>
  )
}
