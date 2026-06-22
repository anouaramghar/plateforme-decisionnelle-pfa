import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { Pill, type PillTone } from '../components/ui/Pill'
import { Empty } from '../components/ui/Empty'
import { fetchCases, ETAT_LABELS, PRIORITE_LABELS } from '../services/interventions'

const PRIORITE_TONE: Record<string, PillTone> = {
  Critical: 'bad', High: 'bad', Medium: 'warn', Low: 'neutral',
}
const ACTIVE = ['Open', 'InProgress', 'WaitingStudent', 'Monitoring', 'Escalated']

type Tab = 'active' | 'closed' | 'all'

export default function Cases() {
  const navigate = useNavigate()
  const [tab, setTab] = useState<Tab>('active')

  const { data: cases = [], isLoading, isError } = useQuery({
    queryKey: ['cases'],
    queryFn: () => fetchCases(),
    refetchInterval: 30_000,
  })

  const filtered = cases.filter(c => {
    if (tab === 'active') return ACTIVE.includes(c.etat)
    if (tab === 'closed') return c.etat === 'Resolved' || c.etat === 'Closed'
    return true
  })

  return (
    <div className="space-y-4">
      <h1 className="text-[22px] font-semibold tracking-tight">Cas d'intervention</h1>

      <div className="flex items-center gap-1" style={{ borderBottom: '1px solid var(--border)' }}>
        {([
          { id: 'active', label: 'Actifs',   n: cases.filter(c => ACTIVE.includes(c.etat)).length },
          { id: 'closed', label: 'Clôturés', n: cases.filter(c => c.etat === 'Resolved' || c.etat === 'Closed').length },
          { id: 'all',    label: 'Tous',     n: cases.length },
        ] as { id: Tab; label: string; n: number }[]).map(t => (
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

      <div className="card overflow-hidden">
        {isLoading && <div className="px-4 py-8 text-center cap" style={{ color: 'var(--text-3)' }}>Chargement…</div>}
        {isError && <div className="px-4 py-8 text-center cap" style={{ color: 'var(--bad)' }}>Impossible de charger les cas.</div>}
        {!isLoading && !isError && filtered.length === 0 && (
          <Empty title="Aucun cas" hint="Les cas ouverts depuis la file de triage apparaîtront ici." />
        )}
        {!isLoading && !isError && filtered.map((c, i) => (
          <button
            key={c.id}
            onClick={() => navigate(`/cases/${c.id}`)}
            className="w-full flex items-center gap-3 px-4 py-3 text-left hover:bg-[var(--surface-2)]"
            style={{ borderBottom: i < filtered.length - 1 ? '1px solid var(--border)' : 'none' }}
          >
            <Pill tone={PRIORITE_TONE[c.priorite] ?? 'neutral'} dot>
              {PRIORITE_LABELS[c.priorite] ?? c.priorite}
            </Pill>
            <div className="flex-1 min-w-0">
              <div className="text-[13px] font-medium truncate">{c.motif}</div>
              <div className="cap">
                {c.etudiant ? `${c.etudiant.prenom} ${c.etudiant.nom}` : `Étudiant #${c.etudiantId}`}
                {c.ownerId == null && ' · non assigné'}
              </div>
            </div>
            <Pill tone="info">{ETAT_LABELS[c.etat] ?? c.etat}</Pill>
            {c.dueDate && (
              <span className="cap" style={{ minWidth: 90, textAlign: 'right' }}>
                échéance {new Date(c.dueDate).toLocaleDateString('fr-FR', { day: 'numeric', month: 'short' })}
              </span>
            )}
            <Icon name="chevRight" size={14} style={{ color: 'var(--text-3)' }} />
          </button>
        ))}
      </div>
    </div>
  )
}
