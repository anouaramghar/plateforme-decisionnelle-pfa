import { cloneElement, isValidElement, useId, type ReactNode, type ReactElement } from 'react'

interface FieldProps {
  label: ReactNode
  hint?: ReactNode
  error?: ReactNode
  children: ReactNode
  required?: boolean
}

export function Field({ label, hint, error, children, required = false }: FieldProps) {
  const errorId = useId()
  // Wire the control to its error so screen readers announce it (WCAG 3.3.1).
  // Falls back gracefully if children isn't a single element.
  const control = isValidElement(children) && error
    ? cloneElement(children as ReactElement<Record<string, unknown>>, {
        'aria-invalid': true,
        'aria-describedby': errorId,
      })
    : children

  return (
    <label className="block">
      <div className="flex items-center justify-between mb-1.5">
        <span className="text-[12px] font-medium" style={{ color: 'var(--text-2)' }}>
          {label}
          {required && <span style={{ color: 'var(--accent-600)' }}> *</span>}
        </span>
        {hint && <span className="cap">{hint}</span>}
      </div>
      {control}
      {error && (
        <div id={errorId} role="alert" className="text-[11.5px] mt-1" style={{ color: 'var(--bad)' }}>
          {error}
        </div>
      )}
    </label>
  )
}
