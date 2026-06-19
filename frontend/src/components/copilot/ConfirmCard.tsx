import { useState } from 'react'
import { Icon } from '../ui/Icon'
import { Pill } from '../ui/Pill'

export interface ConfirmData {
  draftId: number
  preview: {
    student_name?: string
    matricule?: string
    severity?: string
    message?: string
    expires_at?: string
  }
  tool: string
  state: 'pending' | 'confirmed' | 'dismissed'
}

export function ConfirmCard({
  data,
  onConfirm,
  onDismiss,
}: {
  data: ConfirmData
  onConfirm: () => void
  onDismiss: () => void
}) {
  const [confirming, setConfirming] = useState(false)

  const severityLabel: Record<string, string> = { high: 'Haute', medium: 'Moyenne', low: 'Basse' }
  const severityTone: Record<string, 'bad' | 'warn' | 'info'> = { high: 'bad', medium: 'warn', low: 'info' }
  const tone = severityTone[data.preview.severity ?? 'medium'] ?? 'warn'
  const label = severityLabel[data.preview.severity ?? 'medium'] ?? data.preview.severity

  if (data.state === 'confirmed') {
    return (
      <div
        className="flex items-center gap-2 px-3 py-2.5 rounded-xl text-[12.5px] max-w-[370px]"
        style={{
          background: 'color-mix(in oklch, var(--ok) 8%, transparent)',
          border: '1px solid color-mix(in oklch, var(--ok) 18%, transparent)',
          color: 'var(--ok)',
        }}
      >
        <Icon name="check" size={13} strokeWidth={2.2} />
        Alerte créée pour {data.preview.student_name ?? data.preview.matricule}
      </div>
    )
  }

  if (data.state === 'dismissed') {
    return (
      <div
        className="px-3 py-2 rounded-lg text-[12px] cap max-w-[370px]"
        style={{ background: 'var(--surface-2)', color: 'var(--text-3)', border: '1px solid var(--border)' }}
      >
        Alerte annulée.
      </div>
    )
  }

  const handleConfirm = () => {
    setConfirming(true)
    onConfirm()
  }

  return (
    <div
      className="rounded-xl overflow-hidden max-w-[370px]"
      style={{ border: '1px solid var(--border)', background: 'var(--surface)' }}
    >
      <div
        className="px-3 py-2 flex items-center gap-2"
        style={{ background: 'var(--surface-2)', borderBottom: '1px solid var(--border)' }}
      >
        <Icon name="bell" size={12} style={{ color: 'var(--warn)' }} />
        <span className="text-[11.5px] font-medium">Brouillon d'alerte · confirmation requise</span>
        <span className="ml-auto cap text-[10.5px]" style={{ color: 'var(--text-4)' }}>
          expire dans 5 min
        </span>
      </div>
      <div className="p-3 space-y-2.5">
        <div className="flex items-center justify-between gap-2">
          <span className="text-[12.5px] font-semibold tracking-tight">
            {data.preview.student_name ?? data.preview.matricule}
          </span>
          <Pill tone={tone}>{label}</Pill>
        </div>
        {data.preview.message && (
          <p className="text-[12px] leading-relaxed" style={{ color: 'var(--text-2)' }}>
            {data.preview.message}
          </p>
        )}
        <div className="flex items-center gap-2 pt-0.5">
          <button
            className="btn btn-sm btn-accent flex-1"
            onClick={handleConfirm}
            disabled={confirming}
          >
            <Icon name="check" size={12} strokeWidth={2.2} />
            {confirming ? 'Envoi…' : "Confirmer l'envoi"}
          </button>
          <button className="btn btn-sm" onClick={onDismiss}>
            Annuler
          </button>
        </div>
      </div>
    </div>
  )
}
