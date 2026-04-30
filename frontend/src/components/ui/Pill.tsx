import type { ReactNode } from 'react'
import clsx from 'clsx'

export type PillTone = 'ok' | 'warn' | 'bad' | 'info' | 'neutral' | 'accent'

interface PillProps {
  tone?: PillTone
  dot?: boolean
  children: ReactNode
  className?: string
}

export function Pill({ tone = 'neutral', dot = false, children, className }: PillProps) {
  return (
    <span className={clsx(`pill pill-${tone}`, className)}>
      {dot && <span className="pill-dot" style={{ background: 'currentColor' }} />}
      {children}
    </span>
  )
}
