import type { ReactNode } from 'react'

interface FieldProps {
  label: ReactNode
  hint?: ReactNode
  error?: ReactNode
  children: ReactNode
  required?: boolean
}

export function Field({ label, hint, error, children, required = false }: FieldProps) {
  return (
    <label className="block">
      <div className="flex items-center justify-between mb-1.5">
        <span className="text-[12px] font-medium" style={{ color: 'var(--text-2)' }}>
          {label}
          {required && <span style={{ color: 'var(--accent-600)' }}> *</span>}
        </span>
        {hint && <span className="cap">{hint}</span>}
      </div>
      {children}
      {error && <div className="text-[11.5px] mt-1" style={{ color: 'var(--bad)' }}>{error}</div>}
    </label>
  )
}
