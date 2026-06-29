import type { ReactNode } from 'react'

interface SectionHeaderProps {
  title: ReactNode
  subtitle?: ReactNode
  right?: ReactNode
}

export function SectionHeader({ title, subtitle, right }: SectionHeaderProps) {
  return (
    <div className="mb-3 flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
      <div className="min-w-0">
        <h2 className="text-[15px] font-semibold tracking-tight">{title}</h2>
        {subtitle && <div className="cap mt-0.5">{subtitle}</div>}
      </div>
      {right && <div className="flex flex-wrap items-center gap-2 sm:justify-end">{right}</div>}
    </div>
  )
}
