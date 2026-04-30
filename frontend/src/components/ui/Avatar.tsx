import { useMemo } from 'react'

interface AvatarProps {
  name: string
  size?: number
  ring?: boolean
}

export function Avatar({ name, size = 28, ring = false }: AvatarProps) {
  const initials = useMemo(() => {
    const parts = name.split(' ').filter(Boolean)
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase()
  }, [name])

  // Deterministic hue from name so the same person keeps the same color.
  const hue = useMemo(() => {
    let h = 0
    for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) % 360
    return h
  }, [name])

  return (
    <span
      className="avatar"
      style={{
        width: size,
        height: size,
        fontSize: size * 0.36,
        background: `oklch(0.92 0.06 ${hue})`,
        color: `oklch(0.32 0.10 ${hue})`,
        boxShadow: ring ? `0 0 0 2px var(--surface), 0 0 0 3px oklch(0.7 0.12 ${hue})` : 'none',
        borderRadius: size * 0.22,
      }}
    >
      {initials}
    </span>
  )
}
