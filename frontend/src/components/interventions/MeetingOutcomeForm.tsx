import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Icon } from '../ui/Icon'
import {
  recordMeetingResult, OUTCOMES, type MeetingAttendance,
} from '../../services/interventions'

export interface MeetingOutcomeFormProps {
  caseId: number
  canManage: boolean
  onChanged: () => void
}

const ATTENDANCE_OPTIONS: { value: MeetingAttendance; label: string }[] = [
  { value: 'Held',      label: 'Tenu' },
  { value: 'Absent',    label: 'Étudiant absent' },
  { value: 'Cancelled', label: 'Annulé' },
]

export function MeetingOutcomeForm({ caseId, canManage, onChanged }: MeetingOutcomeFormProps) {
  const [attendance, setAttendance] = useState<MeetingAttendance | ''>('')
  const [heldAt, setHeldAt] = useState('')
  const [outcome, setOutcome] = useState<string>(OUTCOMES[0])
  const [summary, setSummary] = useState('')
  const [error, setError] = useState<string | null>(null)

  const isHeld = attendance === 'Held'
  const heldInvalid = isHeld && (!heldAt || !outcome || !summary.trim())
  const canSubmit = attendance !== '' && !heldInvalid

  const submit = useMutation({
    mutationFn: () => {
      if (attendance === '') throw new Error('Aucune présence sélectionnée.')
      if (attendance === 'Held') {
        return recordMeetingResult(caseId, {
          attendance: 'Held',
          heldAt: new Date(heldAt).toISOString(),
          outcome,
          summary: summary.trim(),
        })
      }
      return recordMeetingResult(caseId, { attendance })
    },
    onSuccess: () => {
      setError(null)
      setAttendance('')
      setHeldAt('')
      setSummary('')
      onChanged()
    },
    onError: () => setError('Impossible d’enregistrer l’entretien.'),
  })

  return (
    <div className="card p-4">
      <div className="text-[13px] font-medium mb-2">Résultat de l’entretien</div>
      <div className="space-y-2.5">
        <label className="flex flex-col gap-1">
          <span className="cap">Présence</span>
          <select
            className="input"
            value={attendance}
            onChange={e => setAttendance(e.target.value as MeetingAttendance | '')}
            disabled={!canManage}
          >
            <option value="">Choisir…</option>
            {ATTENDANCE_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
        </label>

        {isHeld && (
          <>
            <label className="flex flex-col gap-1">
              <span className="cap">Heure réelle</span>
              <input
                type="datetime-local"
                className="input"
                value={heldAt}
                onChange={e => setHeldAt(e.target.value)}
              />
            </label>
            <label className="flex flex-col gap-1">
              <span className="cap">Résultat</span>
              <select className="input" value={outcome} onChange={e => setOutcome(e.target.value)}>
                {OUTCOMES.map(o => <option key={o} value={o}>{o}</option>)}
              </select>
            </label>
            <label className="flex flex-col gap-1">
              <span className="cap">Résumé</span>
              <textarea
                className="input"
                rows={3}
                value={summary}
                onChange={e => setSummary(e.target.value)}
                placeholder="Ce qui a été discuté et le résultat obtenu…"
              />
            </label>
          </>
        )}
      </div>

      {error && <div className="cap mt-2" style={{ color: 'var(--bad)' }}>{error}</div>}

      {canManage && (
        <div className="flex justify-end mt-3">
          <button
            className="btn btn-sm btn-primary"
            onClick={() => submit.mutate()}
            disabled={!canSubmit || submit.isPending}
          >
            <Icon name="check" size={13} /> Enregistrer l’entretien
          </button>
        </div>
      )}
    </div>
  )
}
