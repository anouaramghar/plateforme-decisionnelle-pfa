import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { Pill, type PillTone } from '../components/ui/Pill'
import {
  fetchCase, transitionCase, fetchTasks, createTask, completeTask,
  fetchNotes, createNote, fetchCommunications, sendCommunication, retryCommunication,
  fetchTemplates, CASE_TRANSITIONS, ETAT_LABELS, PRIORITE_LABELS, OUTCOMES,
} from '../services/interventions'

const PRIORITE_TONE: Record<string, PillTone> = {
  Critical: 'bad', High: 'bad', Medium: 'warn', Low: 'neutral',
}

export default function CaseDetail() {
  const { id } = useParams()
  const caseId = Number(id)
  const navigate = useNavigate()
  const qc = useQueryClient()

  const { data: c, isLoading, isError } = useQuery({
    queryKey: ['case', caseId],
    queryFn: () => fetchCase(caseId),
  })

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['case', caseId] })
    qc.invalidateQueries({ queryKey: ['cases'] })
  }

  const transition = useMutation({
    mutationFn: (body: Parameters<typeof transitionCase>[1]) => transitionCase(caseId, body),
    onSuccess: invalidate,
    onError: (e: any) => alert(e?.response?.data?.message ?? 'Transition refusée.'),
  })

  const doTransition = (to: string) => {
    const body: Parameters<typeof transitionCase>[1] = { etat: to }
    if (to === 'Resolved') {
      const outcome = window.prompt(`Résultat (${OUTCOMES.join(' / ')}) :`)
      if (!outcome?.trim()) return
      const summary = window.prompt('Résumé de résolution :')
      if (!summary?.trim()) return
      body.outcome = outcome.trim()
      body.resolutionSummary = summary.trim()
    }
    const isReopen = (c?.etat === 'Resolved' || c?.etat === 'Closed') && to === 'InProgress'
    if (isReopen) {
      const raison = window.prompt('Raison de réouverture :')
      if (!raison?.trim()) return
      body.raison = raison.trim()
    }
    transition.mutate(body)
  }

  if (isLoading) return <div className="cap" style={{ color: 'var(--text-3)' }}>Chargement…</div>
  if (isError || !c) return <div className="cap" style={{ color: 'var(--bad)' }}>Cas introuvable.</div>

  const nextStates = CASE_TRANSITIONS[c.etat] ?? []

  return (
    <div className="space-y-4">
      <button className="btn btn-sm btn-ghost" onClick={() => navigate('/cases')}>
        <Icon name="chevLeft" size={13} /> Cas
      </button>

      {/* Header */}
      <div className="card p-4">
        <div className="flex items-start gap-3">
          <Pill tone={PRIORITE_TONE[c.priorite] ?? 'neutral'} dot>
            {PRIORITE_LABELS[c.priorite] ?? c.priorite}
          </Pill>
          <div className="flex-1 min-w-0">
            <h1 className="text-[18px] font-semibold tracking-tight">{c.motif}</h1>
            <div className="cap mt-0.5">
              {c.etudiant ? `${c.etudiant.prenom} ${c.etudiant.nom}` : `Étudiant #${c.etudiantId}`}
              {' · '}ouvert le {new Date(c.creeLe).toLocaleDateString('fr-FR')}
              {c.ownerId == null && ' · non assigné'}
            </div>
          </div>
          <Pill tone="info">{ETAT_LABELS[c.etat] ?? c.etat}</Pill>
        </div>
        {c.outcome && (
          <div className="cap mt-2">Résultat : {c.outcome} — {c.resolutionSummary}</div>
        )}
        <div className="flex items-center gap-1.5 mt-3 flex-wrap">
          {nextStates.map(s => (
            <button
              key={s}
              className="btn btn-sm"
              onClick={() => doTransition(s)}
              disabled={transition.isPending}
            >
              {ETAT_LABELS[s] ?? s}
            </button>
          ))}
          {nextStates.length === 0 && <span className="cap">Aucune transition possible.</span>}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <TasksCard caseId={caseId} onChange={invalidate} />
        <NotesCard caseId={caseId} onChange={invalidate} />
      </div>
      <CommunicationsCard caseId={caseId} onChange={invalidate} />
      <TimelineCard events={c.timeline ?? []} />
    </div>
  )
}

// ── Tasks ────────────────────────────────────────────────────────────────────

function TasksCard({ caseId, onChange }: { caseId: number; onChange: () => void }) {
  const qc = useQueryClient()
  const [titre, setTitre] = useState('')
  const { data: tasks = [] } = useQuery({ queryKey: ['case', caseId, 'tasks'], queryFn: () => fetchTasks(caseId) })

  const refresh = () => { qc.invalidateQueries({ queryKey: ['case', caseId, 'tasks'] }); onChange() }
  const add = useMutation({ mutationFn: () => createTask(caseId, { titre }), onSuccess: () => { setTitre(''); refresh() } })
  const done = useMutation({ mutationFn: (taskId: number) => completeTask(caseId, taskId), onSuccess: refresh })

  return (
    <div className="card p-4">
      <div className="text-[13px] font-medium mb-2">Tâches</div>
      <div className="space-y-1.5 mb-3">
        {tasks.length === 0 && <div className="cap">Aucune tâche.</div>}
        {tasks.map(t => (
          <div key={t.id} className="flex items-center gap-2 text-[12.5px]">
            <button
              onClick={() => !t.done && done.mutate(t.id)}
              disabled={t.done}
              title={t.done ? 'Terminée' : 'Marquer terminée'}
            >
              <Icon name="check" size={14} style={{ color: t.done ? 'var(--ok)' : 'var(--text-3)' }} />
            </button>
            <span style={{ textDecoration: t.done ? 'line-through' : 'none', color: t.done ? 'var(--text-3)' : 'var(--text)' }}>
              {t.titre}
            </span>
          </div>
        ))}
      </div>
      <form
        className="flex items-center gap-1.5"
        onSubmit={e => { e.preventDefault(); if (titre.trim()) add.mutate() }}
      >
        <input
          className="input flex-1" placeholder="Nouvelle tâche…"
          value={titre} onChange={e => setTitre(e.target.value)}
        />
        <button className="btn btn-sm btn-primary" disabled={!titre.trim() || add.isPending}>
          <Icon name="plus" size={13} />
        </button>
      </form>
    </div>
  )
}

// ── Notes ────────────────────────────────────────────────────────────────────

function NotesCard({ caseId, onChange }: { caseId: number; onChange: () => void }) {
  const qc = useQueryClient()
  const [contenu, setContenu] = useState('')
  const [isPrivate, setIsPrivate] = useState(false)
  const { data: notes = [] } = useQuery({ queryKey: ['case', caseId, 'notes'], queryFn: () => fetchNotes(caseId) })

  const add = useMutation({
    mutationFn: () => createNote(caseId, { contenu, isPrivate }),
    onSuccess: () => { setContenu(''); qc.invalidateQueries({ queryKey: ['case', caseId, 'notes'] }); onChange() },
  })

  return (
    <div className="card p-4">
      <div className="text-[13px] font-medium mb-2">Notes</div>
      <div className="space-y-2 mb-3">
        {notes.length === 0 && <div className="cap">Aucune note.</div>}
        {notes.map(n => (
          <div key={n.id} className="text-[12.5px]">
            <div className="flex items-center gap-1.5">
              <span className="font-medium">{n.auteurNom}</span>
              {n.isPrivate && <Pill tone="warn">privée</Pill>}
              <span className="cap">{new Date(n.creeLe).toLocaleDateString('fr-FR')}</span>
            </div>
            <div style={{ color: 'var(--text-2)' }}>{n.contenu}</div>
          </div>
        ))}
      </div>
      <form
        className="space-y-1.5"
        onSubmit={e => { e.preventDefault(); if (contenu.trim()) add.mutate() }}
      >
        <textarea
          className="input w-full" rows={2} placeholder="Ajouter une note…"
          value={contenu} onChange={e => setContenu(e.target.value)}
        />
        <div className="flex items-center justify-between">
          <label className="cap flex items-center gap-1.5 cursor-pointer">
            <input type="checkbox" checked={isPrivate} onChange={e => setIsPrivate(e.target.checked)} />
            Note privée (Admin/Responsable)
          </label>
          <button className="btn btn-sm btn-primary" disabled={!contenu.trim() || add.isPending}>
            <Icon name="plus" size={13} /> Ajouter
          </button>
        </div>
      </form>
    </div>
  )
}

// ── Communications ───────────────────────────────────────────────────────────

function CommunicationsCard({ caseId, onChange }: { caseId: number; onChange: () => void }) {
  const qc = useQueryClient()
  const [templateId, setTemplateId] = useState('')
  const { data: comms = [] } = useQuery({ queryKey: ['case', caseId, 'comms'], queryFn: () => fetchCommunications(caseId) })
  const { data: templates = [] } = useQuery({ queryKey: ['email-templates'], queryFn: fetchTemplates })

  const refresh = () => { qc.invalidateQueries({ queryKey: ['case', caseId, 'comms'] }); onChange() }
  const send = useMutation({ mutationFn: () => sendCommunication(caseId, templateId), onSuccess: refresh })
  const retry = useMutation({ mutationFn: (commId: number) => retryCommunication(caseId, commId), onSuccess: refresh })

  const statusTone: Record<string, PillTone> = { Sent: 'ok', Failed: 'bad', Queued: 'neutral' }

  return (
    <div className="card p-4">
      <div className="text-[13px] font-medium mb-2">Communications</div>
      <div className="space-y-1.5 mb-3">
        {comms.length === 0 && <div className="cap">Aucun email envoyé.</div>}
        {comms.map(m => (
          <div key={m.id} className="flex items-center gap-2 text-[12.5px]">
            <Icon name="send" size={13} style={{ color: 'var(--text-3)' }} />
            <span className="flex-1 truncate">{m.sujet} → {m.destinataire}</span>
            <Pill tone={statusTone[m.status] ?? 'neutral'}>{m.status}</Pill>
            {m.status === 'Failed' && (
              <button className="btn btn-sm" onClick={() => retry.mutate(m.id)} disabled={retry.isPending} title={m.erreur ?? ''}>
                <Icon name="refresh" size={12} /> Renvoyer
              </button>
            )}
          </div>
        ))}
      </div>
      <div className="flex items-center gap-1.5">
        <select className="input flex-1" value={templateId} onChange={e => setTemplateId(e.target.value)}>
          <option value="">Choisir un modèle…</option>
          {templates.map(t => <option key={t.id} value={t.id}>{t.nom}</option>)}
        </select>
        <button
          className="btn btn-sm btn-primary"
          onClick={() => { if (templateId && window.confirm('Envoyer cet email à l\'étudiant ?')) send.mutate() }}
          disabled={!templateId || send.isPending}
        >
          <Icon name="send" size={13} /> Envoyer
        </button>
      </div>
    </div>
  )
}

// ── Timeline ─────────────────────────────────────────────────────────────────

function TimelineCard({ events }: { events: { id: number; action: string; description: string | null; utilisateurNom: string; creeLe: string }[] }) {
  return (
    <div className="card p-4">
      <div className="text-[13px] font-medium mb-3">Historique</div>
      <div className="space-y-2.5">
        {events.length === 0 && <div className="cap">Aucun évènement.</div>}
        {events.map(ev => (
          <div key={ev.id} className="flex items-start gap-2.5 text-[12.5px]">
            <span className="pill-dot" style={{ background: 'var(--accent-500)', marginTop: 5 }} />
            <div className="flex-1">
              <span className="font-medium">{ev.action}</span>
              {ev.description && <span style={{ color: 'var(--text-2)' }}> — {ev.description}</span>}
              <div className="cap">
                {ev.utilisateurNom} · {new Date(ev.creeLe).toLocaleString('fr-FR')}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
