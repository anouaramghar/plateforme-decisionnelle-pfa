import { api } from './api'

// ── Backend types (System.Text.Json camelCase) ──────────────────────────────

export interface EtudiantRef {
  id: number
  nom: string
  prenom: string
  matricule?: string
}

// One row of the case's single event stream. type='Note' rows are staff
// commentary (contenu = the note text); everything else is a system record.
export interface CaseEvent {
  id: number
  caseId: number
  type: string
  contenu: string | null
  isPrivate: boolean
  utilisateurNom: string
  creeLe: string
}

export type MeetingAttendance = 'Held' | 'Absent' | 'Cancelled'

export interface InterventionCase {
  id: number
  etudiantId: number
  filiereId: number
  motif: string
  priorite: string
  etat: string
  ownerId: number | null
  dueDate: string | null
  enRetard: boolean // derived server-side: past due & still open
  outcome: string | null
  resolutionSummary: string | null
  followUpDate: string | null
  meetingScheduledFor: string | null
  meetingLocation: string | null
  meetingAttendance: MeetingAttendance | null
  meetingHeldAt: string | null
  creeLe: string
  clotureLe: string | null
  etudiant?: EtudiantRef | null
  events?: CaseEvent[]
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
  scoreRisque: number       // 0–1 predicted risk
  moyenne: number | null
  absencesH: number
  resume: string            // one-line motif
  openCaseId: number | null
  signals: TriageSignal[]
}

export interface CaseCommunication {
  id: number
  caseId: number
  destinataire: string
  templateId: string
  sujet: string
  corps: string
  status: 'Draft' | 'Queued' | 'Sent' | 'Failed'
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

export type TeacherFollowUpColumn = 'A voir' | 'En suivi' | 'Traite'

export interface TeacherFollowUpCard {
  caseId: number
  etudiantId: number
  studentName: string
  motif: string
  priority: string
  column: TeacherFollowUpColumn
  lastAction: string | null
  creeLe: string
}

interface Paginated<T> { items: T[]; total: number; page: number; pageSize: number }

// ── Case workflow ────────────────────────────────────────────────────────────

// Allowed transitions, mirrored from the backend CaseWorkflow state machine so
// the UI only offers legal next states. The backend re-validates regardless.
export const CASE_TRANSITIONS: Record<string, string[]> = {
  Open:           ['InProgress'],
  InProgress:     ['WaitingStudent', 'Monitoring', 'Resolved'],
  WaitingStudent: ['InProgress', 'Monitoring'],
  Monitoring:     ['InProgress', 'Resolved'],
  Resolved:       ['Closed', 'InProgress'],
  Closed:         ['InProgress'],
}

// Outreach-flavoured labels: the four primary stages read as the action loop
// (À contacter → Email préparé → Entretien planifié → Entretien réalisé).
// Monitoring/Closed keep their operational names. Escalation is not a state —
// it's the derived enRetard flag rendered as a badge.
export const ETAT_LABELS: Record<string, string> = {
  Open: 'À contacter', InProgress: 'Email préparé', WaitingStudent: 'Entretien planifié',
  Monitoring: 'Suivi', Resolved: 'Entretien réalisé', Closed: 'Clôturé',
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

export const fetchTeacherFollowUps = () =>
  api.get<TeacherFollowUpCard[]>('/enseignant/suivi').then(r => r.data)

export const addTeacherObservation = (caseId: number, contenu: string) =>
  api.post(`/enseignant/suivi/${caseId}/observation`, { contenu })

export const markTeacherTreated = (caseId: number, contenu: string) =>
  api.post(`/enseignant/suivi/${caseId}/treated`, { contenu })

export const requestTeacherIntervention = (caseId: number, contenu: string) =>
  api.post(`/enseignant/suivi/${caseId}/request-intervention`, { contenu })

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

export const fetchNotes = (id: number) =>
  api.get<CaseEvent[]>(`/intervention-cases/${id}/notes`).then(r => r.data)
export const createNote = (id: number, body: { contenu: string; isPrivate: boolean }) =>
  api.post(`/intervention-cases/${id}/notes`, body)

export const fetchCommunications = (id: number) =>
  api.get<CaseCommunication[]>(`/intervention-cases/${id}/communications`).then(r => r.data)
export const sendCommunication = (id: number, templateId: string) =>
  api.post(`/intervention-cases/${id}/communications`, { templateId })
export const retryCommunication = (id: number, commId: number) =>
  api.post(`/intervention-cases/${id}/communications/${commId}/retry`)

// ── Student outreach (CaseOutreachController) ──────────────────────────────────

export interface SaveDraftBody { subject: string; body: string }
export interface ScheduleAndSendBody { scheduledFor: string; location: string }
export interface MeetingResultBody {
  attendance: MeetingAttendance
  heldAt?: string
  outcome?: string
  summary?: string
}

export const createOutreachDraft = (caseId: number, body: SaveDraftBody) =>
  api.post<CaseCommunication>(`/intervention-cases/${caseId}/outreach/draft`, body).then(r => r.data)
export const updateOutreachDraft = (caseId: number, commId: number, body: SaveDraftBody) =>
  api.put(`/intervention-cases/${caseId}/outreach/draft/${commId}`, body)
export const scheduleAndSendOutreach = (caseId: number, commId: number, body: ScheduleAndSendBody) =>
  api.post(`/intervention-cases/${caseId}/outreach/draft/${commId}/send`, body)
export const retryOutreach = (caseId: number, commId: number) =>
  api.post(`/intervention-cases/${caseId}/outreach/draft/${commId}/retry`)
export const recordMeetingResult = (caseId: number, body: MeetingResultBody) =>
  api.post(`/intervention-cases/${caseId}/outreach/meeting-result`, body)

export const fetchTemplates = () =>
  api.get<EmailTemplate[]>('/email-templates').then(r => r.data)

export interface FiliereRef { id: number; code: string; intitule?: string }
export const fetchFilieres = () =>
  api.get<Paginated<FiliereRef>>('/filieres?pageSize=100').then(r => r.data.items)

export interface InterventionKpis {
  triageQueue: number; openCases: number; unassignedCases: number
  escalatedCases: number; overdueCases: number; failedEmails: number
  needsContact: number; emailPrepared: number; meetingsScheduled: number; meetingsHeld: number
  duplicateCasesPrevented: number
  contactedEligiblePercent: number; emailDeliverySuccessPercent: number
  meetingHeldPercent: number; medianHoursRiskToEmail: number
}
export const fetchInterventionKpis = () =>
  api.get<InterventionKpis>('/dashboard/interventions').then(r => r.data)
