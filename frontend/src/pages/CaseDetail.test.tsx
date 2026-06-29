import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { OutreachComposer } from '../components/interventions/OutreachComposer'
import { MeetingOutcomeForm } from '../components/interventions/MeetingOutcomeForm'
import type { InterventionCase, CaseCommunication } from '../services/interventions'

// Mock the outreach service functions. The components call these; we assert
// they were called with the right payload and never on partial input.
vi.mock('../services/interventions', async () => {
  const actual = await vi.importActual<typeof import('../services/interventions')>('../services/interventions')
  return {
    ...actual,
    createOutreachDraft: vi.fn().mockResolvedValue({ id: 77, status: 'Draft' } as CaseCommunication),
    updateOutreachDraft: vi.fn().mockResolvedValue({}),
    scheduleAndSendOutreach: vi.fn().mockResolvedValue({}),
    retryOutreach: vi.fn().mockResolvedValue({}),
    recordMeetingResult: vi.fn().mockResolvedValue({}),
  }
})

// Mock the AI draft generator so no network call is made.
vi.mock('../services/outreachDraft', () => ({
  generateOutreachDraft: vi.fn().mockResolvedValue({ subject: 'Invitation à un entretien', body: 'Bonjour Sara…' }),
  fallbackOutreachDraft: vi.fn().mockReturnValue({ subject: 'Invitation à un entretien', body: 'Bonjour Sara…' }),
}))

import {
  scheduleAndSendOutreach,
  recordMeetingResult,
} from '../services/interventions'

const baseCase: InterventionCase = {
  id: 12, etudiantId: 4, filiereId: 1, motif: 'Résultats en baisse', priorite: 'High',
  etat: 'InProgress', ownerId: 2, dueDate: null, escaladeLe: null, outcome: null,
  resolutionSummary: null, followUpDate: null, meetingScheduledFor: null,
  meetingLocation: null, meetingAttendance: null, meetingHeldAt: null,
  creeLe: '2026-06-24T09:00:00Z', clotureLe: null,
  etudiant: { id: 4, prenom: 'Sara', nom: 'Amrani' },
} as InterventionCase

const draftCommunication: CaseCommunication = {
  id: 7, caseId: 12, destinataire: 'sara@eniad.ma', templateId: 'student_outreach',
  sujet: 'Invitation', corps: 'Bonjour, rendez-vous à l’ENIAD.',
  status: 'Draft', erreur: null, creeLe: '2026-06-24T10:00:00Z', envoyeLe: null,
}

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('OutreachComposer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('requires review before sending an editable draft', async () => {
    const user = userEvent.setup()
    renderWithProviders(
      <OutreachComposer intervention={baseCase} communications={[draftCommunication]} canManage onChanged={vi.fn()} />,
    )
    await user.clear(screen.getByLabelText('Sujet'))
    await user.type(screen.getByLabelText('Sujet'), 'Entretien ENIAD')
    await user.type(screen.getByLabelText('Date et heure'), '2026-07-01T10:00')
    await user.type(screen.getByLabelText('Lieu'), 'Salle B12')
    await user.click(screen.getByRole('button', { name: 'Marquer comme envoyé' }))

    // The confirmation dialog shows the reviewed subject and blocks the send
    // until the user explicitly confirms.
    expect(screen.getByRole('dialog', { name: 'Confirmer' })).toHaveTextContent('Entretien ENIAD')
    expect(scheduleAndSendOutreach).not.toHaveBeenCalled()

    await user.click(within(screen.getByRole('dialog', { name: 'Confirmer' })).getByRole('button', { name: 'Marquer comme envoyé' }))
    expect(scheduleAndSendOutreach).toHaveBeenCalledWith(
      12,
      draftCommunication.id,
      expect.objectContaining({ location: 'Salle B12' }),
    )
  })

  it('preserves a failed email and exposes explicit retry', async () => {
    renderWithProviders(
      <OutreachComposer
        intervention={baseCase}
        communications={[{ ...draftCommunication, status: 'Failed', erreur: 'SMTP indisponible' }]}
        canManage
        onChanged={vi.fn()}
      />,
    )
    expect(screen.getByText('SMTP indisponible')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Renvoyer' })).toBeEnabled()
  })

  it('shows verification state for Queued and blocks retry', async () => {
    renderWithProviders(
      <OutreachComposer
        intervention={baseCase}
        communications={[{ ...draftCommunication, status: 'Queued' }]}
        canManage
        onChanged={vi.fn()}
      />,
    )
    expect(screen.getByText('Envoi en cours')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Renvoyer' })).not.toBeInTheDocument()
  })

  it('renders outreach actions read-only for Enseignant', async () => {
    renderWithProviders(
      <OutreachComposer intervention={baseCase} communications={[draftCommunication]} canManage={false} onChanged={vi.fn()} />,
    )
    expect(screen.getByText(/Brouillon en attente de validation/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Envoyer l’email' })).not.toBeInTheDocument()
  })

  it('shows the sent email and meeting details after successful delivery', async () => {
    const sent = {
      ...draftCommunication,
      status: 'Sent' as const,
      envoyeLe: '2026-06-24T11:00:00Z',
    }
    renderWithProviders(
      <OutreachComposer
        intervention={{ ...baseCase, etat: 'WaitingStudent', meetingScheduledFor: '2026-07-01T10:00:00Z', meetingLocation: 'Salle B12' }}
        communications={[sent]}
        canManage
        onChanged={vi.fn()}
      />,
    )
    expect(screen.getByText(/Invitation/)).toBeInTheDocument()
    expect(screen.getByText(/Salle B12/)).toBeInTheDocument()
  })
})

describe('MeetingOutcomeForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('submits a held meeting outcome', async () => {
    const user = userEvent.setup()
    renderWithProviders(<MeetingOutcomeForm caseId={12} canManage onChanged={vi.fn()} />)
    await user.selectOptions(screen.getByLabelText('Présence'), 'Held')
    await user.type(screen.getByLabelText('Heure réelle'), '2026-06-24T10:00')
    await user.selectOptions(screen.getByLabelText('Résultat'), 'Improved')
    await user.type(screen.getByLabelText('Résumé'), 'L’étudiante a participé.')
    await user.click(screen.getByRole('button', { name: 'Enregistrer l’entretien' }))
    expect(recordMeetingResult).toHaveBeenCalledWith(
      12,
      expect.objectContaining({ attendance: 'Held', outcome: 'Improved' }),
    )
  })

  it('does not require held time or outcome for Absent', async () => {
    const user = userEvent.setup()
    renderWithProviders(<MeetingOutcomeForm caseId={12} canManage onChanged={vi.fn()} />)
    await user.selectOptions(screen.getByLabelText('Présence'), 'Absent')
    await user.click(screen.getByRole('button', { name: 'Enregistrer l’entretien' }))
    expect(recordMeetingResult).toHaveBeenCalledWith(12, expect.objectContaining({ attendance: 'Absent' }))
  })

  it('is read-only when canManage is false', async () => {
    renderWithProviders(<MeetingOutcomeForm caseId={12} canManage={false} onChanged={vi.fn()} />)
    expect(screen.queryByRole('button', { name: 'Enregistrer l’entretien' })).not.toBeInTheDocument()
  })
})

