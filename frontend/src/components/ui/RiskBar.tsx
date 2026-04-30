import { Pill } from './Pill'

export type RiskLevel = 'faible' | 'modere' | 'eleve'

interface RiskBarProps {
  score: number // 0..1
  withLabel?: boolean
  width?: number
}

export function RiskBar({ score, withLabel = true, width = 80 }: RiskBarProps) {
  const pct = Math.round(score * 100)
  const tone = score >= 0.65 ? 'bad' : score >= 0.35 ? 'warn' : 'ok'
  const colorVar = tone === 'bad' ? 'var(--bad)' : tone === 'warn' ? 'var(--warn)' : 'var(--ok)'
  return (
    <div className="flex items-center gap-2">
      <div
        className="rounded-full overflow-hidden"
        style={{ width, height: 6, background: 'var(--surface-3)' }}
      >
        <div
          style={{
            width: `${pct}%`,
            height: '100%',
            background: colorVar,
            borderRadius: 999,
            transition: 'width .2s',
          }}
        />
      </div>
      {withLabel && (
        <span
          className="num text-[11.5px]"
          style={{ color: colorVar, fontWeight: 500, minWidth: 32 }}
        >
          {pct}%
        </span>
      )}
    </div>
  )
}

const RISK_MAP: Record<RiskLevel, { tone: 'ok' | 'warn' | 'bad'; label: string }> = {
  faible: { tone: 'ok',   label: 'Faible' },
  modere: { tone: 'warn', label: 'Modéré' },
  eleve:  { tone: 'bad',  label: 'Élevé' },
}

export function RiskPill({ risque }: { risque: RiskLevel }) {
  const m = RISK_MAP[risque] ?? RISK_MAP.faible
  return (
    <Pill tone={m.tone} dot>
      {m.label}
    </Pill>
  )
}
