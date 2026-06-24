import type { ReactNode } from 'react'
import { Icon, type IconName } from './Icon'

interface EmptyProps {
  title: ReactNode
  hint?: ReactNode
  action?: ReactNode
  icon?: IconName
}

export function Empty({ title, hint, action, icon = 'search' }: EmptyProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <div
        className="w-10 h-10 rounded-lg flex items-center justify-center mb-3"
        style={{ background: 'var(--surface-2)', color: 'var(--text-3)' }}
      >
        <Icon name={icon} size={18} />
      </div>
      <div className="text-sm font-medium">{title}</div>
      {hint && <div className="cap mt-1 max-w-xs">{hint}</div>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  )
}
