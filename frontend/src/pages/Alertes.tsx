import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import Triage from './Triage'
import Alerts, { fetchAlertes } from './Alerts'
import { fetchTriage } from '../services/interventions'

// Fused "Alertes" hub: the same Alertes data at two altitudes.
// - Triage  : grouped by student, risk-ranked, → open a case (the worklist).
// - Journal : per-alert log, résoudre / batch / export (the audit view).
// One nav entry, one mental model; each tab keeps its own job intact.
type Tab = 'triage' | 'journal'

export default function Alertes() {
  const [tab, setTab] = useState<Tab>('triage')

  // Same query keys as the child tabs — shared cache, so the badges cost one
  // fetch per key and stay in sync with whichever tab is open.
  const { data: triageGroups = [] } = useQuery({
    queryKey: ['triage'], queryFn: fetchTriage, refetchInterval: 30_000,
  })
  const { data: alertes = [] } = useQuery({
    queryKey: ['alertes'], queryFn: fetchAlertes, refetchInterval: 30_000,
  })
  const activeCount = alertes.filter(a => !a.resolue).length

  const tabs: { id: Tab; label: string; count: number }[] = [
    { id: 'triage', label: 'Triage', count: triageGroups.length },
    { id: 'journal', label: 'Journal', count: activeCount },
  ]

  return (
    <div className="space-y-4">
      <div>
        <div className="cap mb-1">Signaux automatiques · triage et journal</div>
        <h1 className="text-[22px] font-semibold tracking-tight">Alertes</h1>
      </div>

      <div className="flex items-center gap-1" style={{ borderBottom: '1px solid var(--border)' }}>
        {tabs.map(t => (
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
            {t.label}
            {t.count > 0 && (
              <span
                className="num text-[10.5px] px-1.5 rounded-full"
                style={{
                  background: tab === t.id
                    ? 'color-mix(in oklch, var(--accent-500) 14%, transparent)'
                    : 'var(--surface-2)',
                  color: tab === t.id ? 'var(--accent-700)' : 'var(--text-3)',
                  lineHeight: '18px',
                }}
              >
                {t.count}
              </span>
            )}
          </button>
        ))}
      </div>

      {tab === 'triage' ? <Triage embedded /> : <Alerts embedded />}
    </div>
  )
}
