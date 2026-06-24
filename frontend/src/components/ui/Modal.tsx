import { useEffect, useRef } from 'react'
import { Icon } from './Icon'

// Thin wrapper over the native <dialog> element: focus trap, Escape-to-close,
// and backdrop come for free from the platform — no library, no portal.
interface ModalProps {
  open: boolean
  onClose: () => void
  title: string
  children: React.ReactNode
}

export function Modal({ open, onClose, title, children }: ModalProps) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const d = ref.current
    if (!d) return
    if (open && !d.open) d.showModal()
    if (!open && d.open) d.close()
  }, [open])

  return (
    <dialog
      ref={ref}
      aria-label={title}
      onCancel={e => { e.preventDefault(); onClose() }}
      onClick={e => { if (e.target === ref.current) onClose() }} // click outside the panel closes
      style={{
        padding: 0,
        border: 'none',
        borderRadius: 12,
        maxWidth: 440,
        width: '92%',
        background: 'var(--surface)',
        color: 'var(--text)',
        boxShadow: '0 20px 60px rgba(0,0,0,.35)',
      }}
    >
      <div className="flex items-center justify-between px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
        <div className="text-[14px] font-semibold">{title}</div>
        <button className="btn btn-sm btn-ghost" onClick={onClose} aria-label="Fermer">
          <Icon name="x" size={14} />
        </button>
      </div>
      <div className="p-4">{children}</div>
    </dialog>
  )
}
