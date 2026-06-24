import { useState, useEffect } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Icon } from '../ui/Icon'
import { Pill } from '../ui/Pill'
import { Modal } from '../ui/Modal'
import {
  createOutreachDraft, updateOutreachDraft, scheduleAndSendOutreach, retryOutreach,
  type InterventionCase, type CaseCommunication,
} from '../../services/interventions'
import { generateOutreachDraft } from '../../services/outreachDraft'

export interface OutreachComposerProps {
  intervention: InterventionCase
  communications: CaseCommunication[]
  canManage: boolean
  onChanged: () => void
}

// The student-outreach email is identified by its template id. There is at most
// one per case (the backend rejects duplicates).
function findOutreachComm(comms: CaseCommunication[]) {
  return comms.find(c => c.templateId === 'student_outreach')
}

export function OutreachComposer({ intervention, communications, canManage, onChanged }: OutreachComposerProps) {
  const comm = findOutreachComm(communications)
  const status = comm?.status

  // ── Form state ────────────────────────────────────────────────────────────
  const [subject, setSubject]   = useState(comm?.sujet ?? '')
  const [body, setBody]         = useState(comm?.corps ?? '')
  const [scheduledFor, setScheduledFor] = useState('')
  const [location, setLocation] = useState(intervention.meetingLocation ?? '')
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Re-sync local text when the draft changes (e.g. after initial creation).
  useEffect(() => {
    if (comm && status === 'Draft') {
      setSubject(comm.sujet)
      setBody(comm.corps)
    }
  }, [comm?.id, status]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Mutations ──────────────────────────────────────────────────────────────
  const createDraft = useMutation({
    mutationFn: async () => {
      const draft = await generateOutreachDraft({
        firstName: intervention.etudiant?.prenom ?? 'étudiant',
        concernSummary: intervention.motif,
        scheduledFor: scheduledFor ? new Date(scheduledFor).toISOString() : new Date().toISOString(),
        location,
      })
      return createOutreachDraft(intervention.id, { subject: draft.subject, body: draft.body })
    },
    onSuccess: () => { setError(null); onChanged() },
    onError: () => setError('Impossible de préparer le brouillon.'),
  })

  const send = useMutation({
    mutationFn: async () => {
      // Persist the edited text before scheduling — scheduleAndSend uses the
      // STORED subject/body, not the form values.
      if (comm && (subject !== comm.sujet || body !== comm.corps)) {
        await updateOutreachDraft(intervention.id, comm.id, { subject, body })
      }
      return scheduleAndSendOutreach(intervention.id, comm!.id, {
        scheduledFor: new Date(scheduledFor).toISOString(),
        location,
      })
    },
    onSuccess: () => { setConfirmOpen(false); setError(null); onChanged() },
    onError: () => { setConfirmOpen(false); setError('L’envoi a échoué. Vous pouvez réessayer.') },
  })

  const retry = useMutation({
    mutationFn: () => retryOutreach(intervention.id, comm!.id),
    onSuccess: () => { setError(null); onChanged() },
    onError: () => setError('Le renvoi a échoué.'),
  })

  // ── Read-only for Enseignant ────────────────────────────────────────────────
  if (!canManage) {
    if (status === 'Draft') {
      return (
        <div className="card p-4">
          <div className="text-[13px] font-medium mb-1">Brouillon d’invitation</div>
          <div className="cap" style={{ color: 'var(--text-3)' }}>Brouillon en attente de validation par un responsable.</div>
          {comm && <ReadOnlyDraft comm={comm} />}
        </div>
      )
    }
    if (status === 'Sent' || status === 'Queued') {
      return (
        <div className="card p-4">
          <div className="text-[13px] font-medium mb-1">Email envoyé</div>
          {comm && <ReadOnlyDraft comm={comm} />}
          {intervention.meetingScheduledFor && <MeetingDetails c={intervention} />}
        </div>
      )
    }
    return null
  }

  // ── Open: no draft yet — prepare one ────────────────────────────────────────
  if (!comm && intervention.etat === 'Open') {
    return (
      <div className="card p-4">
        <div className="text-[13px] font-medium mb-2">Préparer l’invitation</div>
        <div className="space-y-2.5">
          <label className="flex flex-col gap-1">
            <span className="cap">Date et heure</span>
            <input type="datetime-local" className="input" value={scheduledFor}
              onChange={e => setScheduledFor(e.target.value)} />
          </label>
          <label className="flex flex-col gap-1">
            <span className="cap">Lieu</span>
            <input className="input" value={location} onChange={e => setLocation(e.target.value)}
              placeholder="Salle B12" />
          </label>
        </div>
        {error && <div className="cap mt-2" style={{ color: 'var(--bad)' }}>{error}</div>}
        <button
          className="btn btn-sm btn-primary mt-3"
          onClick={() => createDraft.mutate()}
          disabled={!scheduledFor || !location.trim() || createDraft.isPending}
        >
          <Icon name="edit" size={13} /> Préparer un brouillon
        </button>
      </div>
    )
  }

  // ── InProgress + Draft: edit and review ─────────────────────────────────────
  if (status === 'Draft') {
    return (
      <div className="card p-4">
        <div className="text-[13px] font-medium mb-2">Brouillon d’invitation</div>
        <div className="space-y-2.5">
          <label className="flex flex-col gap-1">
            <span className="cap">Sujet</span>
            <input className="input" value={subject} onChange={e => setSubject(e.target.value)} />
          </label>
          <label className="flex flex-col gap-1">
            <span className="cap">Corps</span>
            <textarea className="input" rows={6} value={body} onChange={e => setBody(e.target.value)} />
          </label>
          <div className="grid grid-cols-2 gap-2.5">
            <label className="flex flex-col gap-1">
              <span className="cap">Date et heure</span>
              <input type="datetime-local" className="input" value={scheduledFor}
                onChange={e => setScheduledFor(e.target.value)} />
            </label>
            <label className="flex flex-col gap-1">
              <span className="cap">Lieu</span>
              <input className="input" value={location} onChange={e => setLocation(e.target.value)} />
            </label>
          </div>
        </div>
        {error && <div className="cap mt-2" style={{ color: 'var(--bad)' }}>{error}</div>}
        <div className="flex justify-end mt-3">
          <button
            className="btn btn-sm btn-primary"
            onClick={() => setConfirmOpen(true)}
            disabled={!subject.trim() || !body.trim() || !scheduledFor || !location.trim()}
          >
            <Icon name="send" size={13} /> Vérifier et envoyer
          </button>
        </div>

        <Modal open={confirmOpen} onClose={() => setConfirmOpen(false)} title="Confirmer l’envoi">
          <div className="space-y-2 text-[12.5px]">
            <div><span className="cap">Destinataire</span><div>{comm?.destinataire}</div></div>
            <div><span className="cap">Sujet</span><div>{subject}</div></div>
            <div><span className="cap">Corps</span><pre className="font-sans whitespace-pre-wrap" style={{ color: 'var(--text-2)' }}>{body}</pre></div>
            <div><span className="cap">Rendez-vous</span><div>{scheduledFor && new Date(scheduledFor).toLocaleString('fr-FR')} · {location}</div></div>
          </div>
          <div className="flex justify-end gap-2 mt-4">
            <button className="btn btn-sm" onClick={() => setConfirmOpen(false)}>Annuler</button>
            <button
              className="btn btn-sm btn-primary"
              onClick={() => send.mutate()}
              disabled={send.isPending}
            >
              <Icon name="send" size={13} /> Envoyer l’email
            </button>
          </div>
        </Modal>
      </div>
    )
  }

  // ── Queued: delivery in flight — no retry ───────────────────────────────────
  if (status === 'Queued') {
    return (
      <div className="card p-4">
        <div className="text-[13px] font-medium mb-1">Email en cours d’envoi</div>
        <div className="cap" style={{ color: 'var(--text-3)' }}>Vérification de l’envoi en cours…</div>
        {comm && <ReadOnlyDraft comm={comm} />}
      </div>
    )
  }

  // ── Failed: show error + retry ───────────────────────────────────────────────
  if (status === 'Failed') {
    return (
      <div className="card p-4">
        <div className="text-[13px] font-medium mb-1">Échec de l’envoi</div>
        {comm?.erreur && <div className="cap mb-2" style={{ color: 'var(--bad)' }}>{comm.erreur}</div>}
        {comm && <ReadOnlyDraft comm={comm} />}
        {error && <div className="cap mt-2" style={{ color: 'var(--bad)' }}>{error}</div>}
        <div className="flex justify-end mt-3">
          <button className="btn btn-sm btn-primary" onClick={() => retry.mutate()} disabled={retry.isPending}>
            <Icon name="refresh" size={13} /> Renvoyer
          </button>
        </div>
      </div>
    )
  }

  // ── Sent: immutable copy + meeting details ──────────────────────────────────
  if (status === 'Sent') {
    return (
      <div className="card p-4">
        <div className="text-[13px] font-medium mb-1 flex items-center gap-2">
          Email envoyé <Pill tone="ok">Envoyé</Pill>
        </div>
        {comm && <ReadOnlyDraft comm={comm} />}
        {intervention.meetingScheduledFor && <MeetingDetails c={intervention} />}
      </div>
    )
  }

  return null
}

function ReadOnlyDraft({ comm }: { comm: CaseCommunication }) {
  return (
    <div className="mt-2 space-y-1 text-[12.5px]">
      <div><span className="cap">Sujet</span><div className="font-medium">{comm.sujet}</div></div>
      <div><span className="cap">Corps</span><pre className="font-sans whitespace-pre-wrap" style={{ color: 'var(--text-2)' }}>{comm.corps}</pre></div>
    </div>
  )
}

function MeetingDetails({ c }: { c: InterventionCase }) {
  if (!c.meetingScheduledFor) return null
  return (
    <div className="mt-3 pt-3 text-[12.5px]" style={{ borderTop: '1px solid var(--border)' }}>
      <div className="cap mb-1">Rendez-vous planifié</div>
      <div>{new Date(c.meetingScheduledFor).toLocaleString('fr-FR')} · {c.meetingLocation}</div>
    </div>
  )
}
