import { useEffect, useRef } from 'react'

const FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

export function useDialog(open: boolean, onClose: () => void) {
  const panelRef = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose

  useEffect(() => {
    if (!open) return
    const previouslyFocused = document.activeElement as HTMLElement | null
    const focusables = (): HTMLElement[] =>
      Array.from(panelRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR) ?? [])

    const items = focusables()
    items[0]?.focus()

    const onKey = (ev: KeyboardEvent) => {
      if (ev.key === 'Escape') {
        ev.stopPropagation()
        onCloseRef.current()
        return
      }
      if (ev.key === 'Tab' && panelRef.current) {
        const f = focusables()
        if (f.length === 0) return
        const first = f[0]
        const last = f[f.length - 1]
        if (ev.shiftKey && document.activeElement === first) {
          last.focus()
          ev.preventDefault()
        } else if (!ev.shiftKey && document.activeElement === last) {
          first.focus()
          ev.preventDefault()
        }
      }
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('keydown', onKey)
      previouslyFocused?.focus?.()
    }
  }, [open])

  return panelRef
}