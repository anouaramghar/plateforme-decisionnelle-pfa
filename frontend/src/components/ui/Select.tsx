interface SelectProps {
  value: string
  onChange: (v: string) => void
  options: string[]
  label?: string
}

export function Select({ value, onChange, options, label }: SelectProps) {
  return (
    <div className="relative">
      <select
        value={value}
        onChange={e => onChange(e.target.value)}
        className="input pr-7"
        style={{ height: 30, paddingLeft: label ? 70 : 10, fontSize: 12.5 }}
      >
        {options.map(o => (
          <option key={o} value={o}>{o}</option>
        ))}
      </select>
      {label && (
        <span
          className="absolute left-2.5 top-1/2 -translate-y-1/2 text-[11.5px] pointer-events-none"
          style={{ color: 'var(--text-3)' }}
        >
          {label}:
        </span>
      )}
    </div>
  )
}
