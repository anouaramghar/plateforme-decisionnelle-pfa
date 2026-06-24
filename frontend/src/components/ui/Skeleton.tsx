interface SkeletonProps {
  w?: number | string
  h?: number | string
  radius?: number | string
  className?: string
}

export function Skeleton({ w = '100%', h = 14, radius = 4, className = '' }: SkeletonProps) {
  return <div className={`skel ${className}`} style={{ width: w, height: h, borderRadius: radius }} />
}
