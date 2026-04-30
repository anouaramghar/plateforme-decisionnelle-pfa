import type { ReactNode } from 'react'

interface SectionHeaderProps {
  title: ReactNode
  subtitle?: ReactNode
  right?: ReactNode
}

export function SectionHeader({ title, subtitle, right }: SectionHeaderProps) {
  return (
    <div className="flex items-end justify-between mb-3">
      <div>
        <h2 className="text-[15px] font-semibold tracking-tight">{title}</h2>
        {subtitle && <div className="cap mt-0.5">{subtitle}</div>}
      </div>
      {right && <div className="flex items-center gap-2">{right}</div>}
    </div>
  )
}
