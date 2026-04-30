import { useState } from 'react'
import { Icon } from '../components/ui/Icon'
import { Pill, type PillTone } from '../components/ui/Pill'
import { Empty } from '../components/ui/Empty'
import { FilterChip } from '../components/ui/FilterChip'
import { ALERTES, ALERT_TYPES, type Alerte } from '../data/mock'

type Tab = 'active' | 'resolu' | 'all'
type AlertType = Alerte['type']
type TypeFilter = AlertType | 'Tous'

const FILTERABLE_TYPES: AlertType[] = ['NoteFaible', 'AbsenceExcessive', 'RisqueEleve']

export default function Alerts() {
  const [tab, setTab] = useState<Tab>('active')
  const [typeFilter, setTypeFilter] = useState<TypeFilter>('Tous')
  const all = ALERTES

  const filtered = all.filter(a => {
    if (tab === 'active' && a.statut !== 'active') return false
    if (tab === 'resolu' && a.statut !== 'resolue') return false
    if (typeFilter !== 'Tous' && a.type !== typeFilter) return false
    return true
  })

  const counts: Record<AlertType, number> = {
    NoteFaible:        all.filter(a => a.type === 'NoteFaible' && a.statut === 'active').length,
    AbsenceExcessive:  all.filter(a => a.type === 'AbsenceExcessive' && a.statut === 'active').length,
    RisqueEleve:       all.filter(a => a.type === 'RisqueEleve' && a.statut === 'active').length,
    ModuleNonValide:   0,
    RetardRecurrent:   0,
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
              { id: 'active', label: 'Actives', n: all.filter(a => a.statut === 'active').length },
              { id: 'resolu', label: 'Résolues', n: all.filter(a => a.statut === 'resolue').length },
              { id: 'all',    label: 'Toutes',  n: all.length },
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
              value={ALERT_TYPES[typeFilter as AlertType].label}
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
        {filtered.length === 0 && (
          <Empty
            title="Aucune alerte"
            hint="Les alertes générées par le système apparaîtront ici."
          />
        )}
        {filtered.slice(0, 30).map((a, i) => (
          <AlertRow key={a.id} alert={a} divider={i < filtered.length - 1} />
        ))}
      </div>
    </div>
  )
}

function AlertRow({ alert, divider }: { alert: Alerte; divider: boolean }) {
  const meta = ALERT_TYPES[alert.type]
  const iconName: 'brain' | 'clock' | 'alert' =
    alert.type === 'RisqueEleve'
      ? 'brain'
      : alert.type === 'AbsenceExcessive'
      ? 'clock'
      : 'alert'

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
          <Pill tone={meta.pill}>{meta.label}</Pill>
          <span className="text-[12.5px] font-medium">{alert.etudiant}</span>
          <span className="cap font-mono">· {alert.filiere}</span>
          {alert.statut === 'active' && (
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
          {new Date(alert.date).toLocaleDateString('fr-FR', { day: 'numeric', month: 'short' })}
        </div>
        <div className="cap">
          {new Date(alert.date).toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })}
        </div>
      </div>
      {alert.statut === 'active' ? (
        <div className="flex items-center gap-1.5">
          <button className="btn btn-sm">Voir</button>
          <button className="btn btn-sm btn-primary">
            <Icon name="check" size={12} strokeWidth={2.4} />
            Résoudre
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
