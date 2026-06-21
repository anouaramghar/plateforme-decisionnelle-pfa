import type { CSSProperties, ReactElement } from 'react'

const ICONS: Record<string, ReactElement> = {
  dashboard: <path d="M3 3h7v9H3zM14 3h7v5h-7zM14 11h7v10h-7zM3 15h7v6H3z" />,
  students:  <path d="M16 14a4 4 0 100-8 4 4 0 000 8zm-8 8a8 8 0 0116 0M8 14a3 3 0 100-6 3 3 0 000 6zM2 22a6 6 0 0110.5-4" />,
  bell:      <path d="M6 8a6 6 0 1112 0c0 7 3 9 3 9H3s3-2 3-9zM10 21a2 2 0 004 0" />,
  spark:     <path d="M5 3v4M3 5h4M19 17v4M17 19h4M14 3l2.5 6.5L23 12l-6.5 2.5L14 21l-2.5-6.5L5 12l6.5-2.5z" />,
  doc:       <path d="M14 3H6a2 2 0 00-2 2v14a2 2 0 002 2h12a2 2 0 002-2V9zM14 3v6h6M9 13h6M9 17h6" />,
  settings:  <path d="M12 15a3 3 0 100-6 3 3 0 000 6zM19.4 15a1.7 1.7 0 00.3 1.8l.1.1a2 2 0 11-2.8 2.8l-.1-.1a1.7 1.7 0 00-1.8-.3 1.7 1.7 0 00-1 1.5V21a2 2 0 11-4 0v-.1a1.7 1.7 0 00-1.1-1.5 1.7 1.7 0 00-1.8.3l-.1.1a2 2 0 11-2.8-2.8l.1-.1a1.7 1.7 0 00.3-1.8 1.7 1.7 0 00-1.5-1H3a2 2 0 110-4h.1a1.7 1.7 0 001.5-1.1 1.7 1.7 0 00-.3-1.8l-.1-.1a2 2 0 112.8-2.8l.1.1a1.7 1.7 0 001.8.3H9a1.7 1.7 0 001-1.5V3a2 2 0 114 0v.1a1.7 1.7 0 001 1.5 1.7 1.7 0 001.8-.3l.1-.1a2 2 0 112.8 2.8l-.1.1a1.7 1.7 0 00-.3 1.8V9a1.7 1.7 0 001.5 1H21a2 2 0 110 4h-.1a1.7 1.7 0 00-1.5 1z" />,
  search:    <path d="M11 19a8 8 0 100-16 8 8 0 000 16zM21 21l-4.3-4.3" />,
  filter:    <path d="M3 5h18M6 12h12M10 19h4" />,
  download:  <path d="M12 3v12m0 0l-4-4m4 4l4-4M5 21h14" />,
  plus:      <path d="M12 5v14M5 12h14" />,
  check:     <path d="M5 13l4 4L19 7" />,
  x:         <path d="M6 6l12 12M18 6L6 18" />,
  arrowRight:<path d="M5 12h14M13 5l7 7-7 7" />,
  chevDown:  <path d="M6 9l6 6 6-6" />,
  chevUp:    <path d="M18 15l-6-6-6 6" />,
  chevRight: <path d="M9 18l6-6-6-6" />,
  chevLeft:  <path d="M15 18l-6-6 6-6" />,
  more:      <path d="M5 12h.01M12 12h.01M19 12h.01" />,
  ext:       <path d="M14 3h7v7M21 3l-9 9M10 5H5a2 2 0 00-2 2v12a2 2 0 002 2h12a2 2 0 002-2v-5" />,
  star:      <path d="M12 2l3 7 7 .8-5.3 5.1L18.5 22 12 18.4 5.5 22l1.8-7.1L2 9.8 9 9z" />,
  alert:     <path d="M12 9v4M12 17h.01M10.3 3.9L1.8 18.2A2 2 0 003.5 21h17a2 2 0 001.7-2.8L13.7 3.9a2 2 0 00-3.4 0z" />,
  brain:     <path d="M12 5a3 3 0 00-3 3 3 3 0 00-3 3 3 3 0 002 2.8V16a3 3 0 003 3M12 5a3 3 0 013 3 3 3 0 013 3 3 3 0 01-2 2.8V16a3 3 0 01-3 3M12 5v14" />,
  upload:    <path d="M12 3v12m0-12l-4 4m4-4l4 4M5 21h14" />,
  refresh:   <path d="M3 12a9 9 0 0115-6.7L21 8M21 3v5h-5M21 12a9 9 0 01-15 6.7L3 16M3 21v-5h5" />,
  eye:       <path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12zM12 9a3 3 0 100 6 3 3 0 000-6z" />,
  eyeOff:    <><path d="M17.9 17.9A10 10 0 012.1 12S4.7 7 10 5.3M6.1 6.1A10 10 0 0121.9 12s-2.6 5-8 6.7"/><path d="M9.9 9.9a3 3 0 004.2 4.2M2 2l20 20"/></>,
  user:      <path d="M12 12a4 4 0 100-8 4 4 0 000 8zM4 21a8 8 0 0116 0" />,
  cmd:       <path d="M9 6V4.5A2.5 2.5 0 116.5 7H9zm0 0v12m0-12h6m-6 12H6.5A2.5 2.5 0 119 16.5V18zm6 0v-1.5a2.5 2.5 0 112.5 2.5H15zm0 0V6m0 0h2.5A2.5 2.5 0 1115 7.5V6z" />,
  bookmark:  <path d="M19 21l-7-5-7 5V5a2 2 0 012-2h10a2 2 0 012 2z" />,
  clock:     <path d="M12 22a10 10 0 100-20 10 10 0 000 20zM12 6v6l4 2" />,
  trend:     <path d="M3 17l6-6 4 4 8-8M21 7v6h-6" />,
  sun:       <><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></>,
  moon:      <path d="M21 12.8A9 9 0 1111.2 3a7 7 0 109.8 9.8z"/>,
  collapse:  <path d="M9 4v16M14 8l-4 4 4 4" />,
  expand:    <path d="M9 4v16M10 8l4 4-4 4" />,
  graduation:<path d="M22 10L12 5 2 10l10 5 10-5zM6 12v5c0 1 3 3 6 3s6-2 6-3v-5" />,
  database:  <path d="M12 6c4.4 0 8-1.3 8-3s-3.6-3-8-3-8 1.3-8 3 3.6 3 8 3zM4 5v6c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 11v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6" />,
  send:      <path d="M22 2L11 13M22 2l-7 20-4-9-9-4 20-7z" />,
  stop:      <path d="M6 6h12v12H6z" />,
  edit:      <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7M18.5 2.5a2.1 2.1 0 113 3L12 15l-4 1 1-4z" />,
  trash:     <path d="M3 6h18M8 6V4h8v2M19 6l-1 14a2 2 0 01-2 2H8a2 2 0 01-2-2L5 6" />,
  copilot:   <path d="M12 2a10 10 0 100 20 10 10 0 000-20zM8 11.5a4 4 0 008 0M9 9h.01M15 9h.01" />,
}

export type IconName = keyof typeof ICONS

interface IconProps {
  name: IconName | string
  size?: number
  className?: string
  strokeWidth?: number
  style?: CSSProperties
  title?: string
}

export function Icon({ name, size = 15, className = '', strokeWidth = 1.6, style, title }: IconProps) {
  const d = ICONS[name]
  if (!d) return null
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      style={style}
      aria-hidden={!title}
    >
      {title ? <title>{title}</title> : null}
      {d}
    </svg>
  )
}
