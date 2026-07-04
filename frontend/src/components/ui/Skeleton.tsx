interface SkeletonProps {
  w?: number | string
  h?: number | string
  radius?: number | string
  className?: string
}

export function Skeleton({ w = '100%', h = 14, radius = 4, className = '' }: SkeletonProps) {
  return <div className={`skel ${className}`} style={{ width: w, height: h, borderRadius: radius }} />
}

/* Shimmer placeholder for loading lists/tables — staggered widths so it
   doesn't read as uniform stripes. Replaces bare "Chargement…" text. */
export function SkeletonRows({ rows = 4, className = '' }: { rows?: number; className?: string }) {
  return (
    <div className={`p-4 space-y-3 ${className}`} aria-hidden>
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="flex items-center gap-3">
          <Skeleton w={28} h={28} radius={6} />
          <div className="flex-1 space-y-1.5">
            <Skeleton w={`${55 + ((i * 17) % 30)}%`} h={11} />
            <Skeleton w={`${28 + ((i * 23) % 25)}%`} h={9} />
          </div>
        </div>
      ))}
    </div>
  )
}
