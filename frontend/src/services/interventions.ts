import { api } from './api'

// ── Backend types (System.Text.Json camelCase) ──────────────────────────────

export interface EtudiantRef {
  id: number
  nom: string
  prenom: string
  matricule?: string
}

export interface TimelineEvent {
  id: number
  caseId: number
  action: string
  description: string | null
  utilisateurNom: string
  creeLe: string
}

export interface InterventionCase {
  id: number
  etudiantId: number
  filiereId: number
  motif: string
  priorite: string
  etat: string
  ownerId: number | null
  dueDate: string | null
  escaladeLe: string | null
  outcome: string | null
  resolutionSummary: string | null
  followUpDate: string | null
  creeLe: string
  clotureLe: string | null
  etudiant?: EtudiantRef | null
  timeline?: TimelineEvent[]
}

export interface TriageSignal {
  id: number
  type: string
  niveau: string
  message: string | null
  creeLe: string
}

export interface TriageGroup {
  etudiantId: number
  matricule: string
  etudiantNom: string
  filiereId: number
  signalCount: number
  maxNiveau: string
  suggestedPriorite: string
  openCaseId: number | null
  signals: TriageSignal[]
}

export interface CaseTask {
  id: number
  caseId: number
  titre: string
  assigneeId: number | null
  dueDate: string | null
  done: boolean
  doneLe: string | null
  completionEvidence: string | null
  creeLe: string
}

export interface CaseNote {
  id: number
  caseId: number
  contenu: string
  isPrivate: boolean
  auteurNom: string
  creeLe: string
}

export interface CaseCommunication {
  id: number
  caseId: number
  destinataire: string
  templateId: string
  sujet: string
  corps: string
  status: 'Queued' | 'Sent' | 'Failed'
  erreur: string | null
  creeLe: string
  envoyeLe: string | null
}

export interface EmailTemplate {
  id: string
  nom: string
  sujet: string
  corps: string
}

interface Paginated<T> { items: T[]; total: number; page: number; pageSize: number }

// ── Case workflow ────────────────────────────────────────────────────────────

// Allowed transitions, mirrored from the backend CaseWorkflow state machine so
// the UI only offers legal next states. The backend re-validates regardless.
export const CASE_TRANSITIONS: Record<string, string[]> = {
  Open:           ['InProgress'],
  InProgress:     ['WaitingStudent', 'Monitoring', 'Escalated', 'Resolved'],
  WaitingStudent: ['InProgress', 'Monitoring', 'Escalated'],
  Monitoring:     ['InProgress', 'Resolved'],
  Escalated:      ['InProgress', 'Resolved'],
  Resolved:       ['Closed', 'InProgress'],
  Closed:         ['InProgress'],
}

export const ETAT_LABELS: Record<string, string> = {
  Open: 'Ouvert', InProgress: 'En cours', WaitingStudent: 'En attente étudiant',
  Monitoring: 'Suivi', Escalated: 'Escaladé', Resolved: 'Résolu', Closed: 'Clôturé',
}

export const PRIORITE_LABELS: Record<string, string> = {
  Critical: 'Critique', High: 'Haute', Medium: 'Moyenne', Low: 'Basse',
}

export const OUTCOMES = [
  'Improved', 'Stable', 'Deteriorated', 'Withdrawn',
  'AdministrativeResolution', 'UnableToContact', 'NoLongerApplicable',
] as const

// ── API ──────────────────────────────────────────────────────────────────────

export const fetchTriage = () =>
  api.get<TriageGroup[]>('/alertes/triage').then(r => r.data)

export const dismissSignal = (id: number, raison: string) =>
  api.patch(`/alertes/${id}/dismiss`, { raison })

export const fetchCases = (params: { etat?: string; etudiantId?: number } = {}) => {
  const q = new URLSearchParams({ pageSize: '100' })
  if (params.etat) q.set('etat', params.etat)
  if (params.etudiantId) q.set('etudiantId', String(params.etudiantId))
  return api.get<Paginated<InterventionCase>>(`/intervention-cases?${q}`).then(r => r.data.items)
}

export const fetchCase = (id: number) =>
  api.get<InterventionCase>(`/intervention-cases/${id}`).then(r => r.data)

export interface CreateCaseBody {
  etudiantId: number; motif: string; priorite?: string; alerteId?: number; ownerId?: number
}
export const createCase = (body: CreateCaseBody) =>
  api.post<InterventionCase>('/intervention-cases', body).then(r => r.data)

export interface TransitionBody {
  etat: string; outcome?: string; resolutionSummary?: string; raison?: string; ownerId?: number
}
export const transitionCase = (id: number, body: TransitionBody) =>
  api.patch(`/intervention-cases/${id}/transition`, body)

export const fetchTasks = (id: number) =>
  api.get<CaseTask[]>(`/intervention-cases/${id}/tasks`).then(r => r.data)
export const createTask = (id: number, body: { titre: string; dueDate?: string }) =>
  api.post(`/intervention-cases/${id}/tasks`, body)
export const completeTask = (id: number, taskId: number, completionEvidence?: string) =>
  api.patch(`/intervention-cases/${id}/tasks/${taskId}/complete`, { completionEvidence })

export const fetchNotes = (id: number) =>
  api.get<CaseNote[]>(`/intervention-cases/${id}/notes`).then(r => r.data)
export const createNote = (id: number, body: { contenu: string; isPrivate: boolean }) =>
  api.post(`/intervention-cases/${id}/notes`, body)

export const fetchCommunications = (id: number) =>
  api.get<CaseCommunication[]>(`/intervention-cases/${id}/communications`).then(r => r.data)
export const sendCommunication = (id: number, templateId: string) =>
  api.post(`/intervention-cases/${id}/communications`, { templateId })
export const retryCommunication = (id: number, commId: number) =>
  api.post(`/intervention-cases/${id}/communications/${commId}/retry`)

export const fetchTemplates = () =>
  api.get<EmailTemplate[]>('/email-templates').then(r => r.data)

export interface InterventionKpis {
  triageQueue: number; openCases: number; unassignedCases: number
  escalatedCases: number; overdueCases: number; overdueTasks: number; failedEmails: number
}
export const fetchInterventionKpis = () =>
  api.get<InterventionKpis>('/dashboard/interventions').then(r => r.data)
