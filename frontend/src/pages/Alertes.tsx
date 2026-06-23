import { useState } from 'react'
import Triage from './Triage'
import Alerts from './Alerts'

// Fused "Alertes" hub: the same Alertes data at two altitudes.
// - Triage  : grouped by student, risk-ranked, → open a case (the worklist).
// - Journal : per-alert log, résoudre / batch / export (the audit view).
// One nav entry, one mental model; each tab keeps its own job intact.
type Tab = 'triage' | 'journal'

export default function Alertes() {
  const [tab, setTab] = useState<Tab>('triage')

  const tabs: { id: Tab; label: string }[] = [
    { id: 'triage', label: 'Triage' },
    { id: 'journal', label: 'Journal' },
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
            className="px-3 py-2 text-[12.5px]"
            style={{
              color: tab === t.id ? 'var(--text)' : 'var(--text-3)',
              fontWeight: tab === t.id ? 500 : 400,
              borderBottom: tab === t.id ? '2px solid var(--accent-500)' : '2px solid transparent',
              marginBottom: -1,
            }}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'triage' ? <Triage embedded /> : <Alerts embedded />}
    </div>
  )
}
