import { Icon } from './Icon'

interface FilterChipProps {
  label: string
  value: string
  onRemove: () => void
}

export function FilterChip({ label, value, onRemove }: FilterChipProps) {
  return (
    <span className="pill pill-neutral" style={{ paddingRight: 4 }}>
      <span style={{ color: 'var(--text-3)' }}>{label}:</span>
      <span style={{ fontWeight: 500 }}>{value}</span>
      <button
        onClick={onRemove}
        className="ml-1 -mr-0.5 w-4 h-4 rounded flex items-center justify-center hover:bg-[var(--surface-3)]"
        style={{ color: 'var(--text-3)' }}
        aria-label={`Retirer ${label}`}
      >
        <Icon name="x" size={10} strokeWidth={2.2} />
      </button>
    </span>
  )
}
