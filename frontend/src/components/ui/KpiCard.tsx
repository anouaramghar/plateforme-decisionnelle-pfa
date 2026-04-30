import type { ReactNode } from 'react'
import { Icon } from './Icon'
import { Pill, type PillTone } from './Pill'

interface KpiCardProps {
  label: string
  value: ReactNode
  suffix?: ReactNode
  delta?: number
  deltaTone?: PillTone
  spark?: number[]
  hint?: ReactNode
}

export function KpiCard({ label, value, suffix, delta, deltaTone = 'ok', spark, hint }: KpiCardProps) {
  return (
    <div className="card p-4 flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <div
          className="text-[11.5px] uppercase tracking-wider font-medium"
          style={{ color: 'var(--text-3)' }}
        >
          {label}
        </div>
        {delta != null && (
          <Pill tone={deltaTone}>
            <Icon name="trend" size={11} strokeWidth={2} />
            {delta > 0 ? '+' : ''}
            {delta}%
          </Pill>
        )}
      </div>
      <div className="flex items-baseline gap-1">
        <span className="num num-display">{value}</span>
        {suffix && <span className="text-sm" style={{ color: 'var(--text-3)' }}>{suffix}</span>}
      </div>
      {spark && (
        <div className="sparkbar">
          {spark.map((v, i) => (
            <div
              key={i}
              style={{
                height: `${Math.max(15, v)}%`,
                opacity: 0.55 + (i / spark.length) * 0.45,
              }}
            />
          ))}
        </div>
      )}
      {hint && <div className="cap">{hint}</div>}
    </div>
  )
}

interface MiniKpiProps {
  label: string
  value: ReactNode
  suffix?: ReactNode
  delta?: number
  deltaTone?: PillTone
  hint?: ReactNode
  emphasized?: boolean
}

export function MiniKpi({ label, value, suffix, delta, deltaTone = 'ok', hint, emphasized = false }: MiniKpiProps) {
  return (
    <div
      className="card p-5 flex flex-col justify-between"
      style={{
        minHeight: 124,
        background: emphasized
          ? 'linear-gradient(180deg, var(--surface) 0%, color-mix(in oklch, var(--bad) 4%, var(--surface)) 100%)'
          : 'var(--surface)',
        borderColor: emphasized ? 'color-mix(in oklch, var(--bad) 22%, var(--border))' : 'var(--border)',
      }}
    >
      <div className="flex items-center justify-between">
        <div
          className="text-[11px] uppercase tracking-[0.08em] font-medium"
          style={{ color: 'var(--text-3)' }}
        >
          {label}
        </div>
        {delta != null && (
          <Pill tone={deltaTone}>
            <Icon name="trend" size={10} strokeWidth={2.2} />
            {delta > 0 ? '+' : ''}
            {delta}%
          </Pill>
        )}
      </div>
      <div>
        <div className="flex items-baseline gap-1">
          <span className="num num-display">{value}</span>
          {suffix && <span className="text-[14px]" style={{ color: 'var(--text-3)' }}>{suffix}</span>}
        </div>
        {hint && <div className="cap mt-1.5">{hint}</div>}
      </div>
    </div>
  )
}
