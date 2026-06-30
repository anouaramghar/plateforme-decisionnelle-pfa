import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import Cases from './Cases'
import type { InterventionCase } from '../services/interventions'

const mockNavigate = vi.fn()
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  }
})

const fetchCasesMock = vi.fn<() => Promise<InterventionCase[]>>()
const fetchFilieresMock = vi.fn<() => Promise<{ id: number; code: string }[]>>()
vi.mock('../services/interventions', async () => {
  const actual = await vi.importActual<typeof import('../services/interventions')>('../services/interventions')
  return {
    ...actual,
    fetchCases: () => fetchCasesMock(),
    fetchFilieres: () => fetchFilieresMock(),
  }
})

function buildCase(overrides: Partial<InterventionCase>): InterventionCase {
  return {
    id: 0,
    etudiantId: 0,
    filiereId: 1,
    motif: 'Motif',
    priorite: 'Medium',
    etat: 'Open',
    ownerId: null,
    dueDate: null,
    escaladeLe: null,
    outcome: null,
    resolutionSummary: null,
    followUpDate: null,
    meetingScheduledFor: null,
    meetingLocation: null,
    meetingAttendance: null,
    meetingHeldAt: null,
    creeLe: '2026-06-24T09:00:00Z',
    clotureLe: null,
    etudiant: { id: 0, prenom: 'Inconnu', nom: 'Inconnu' },
    ...overrides,
  } as InterventionCase
}

const SAMPLE: InterventionCase[] = [
  buildCase({ id: 11, etudiantId: 4, etat: 'Open', priorite: 'High', ownerId: 2, motif: 'Résultats en baisse', etudiant: { id: 4, prenom: 'Sara', nom: 'Amrani' } }),
  buildCase({ id: 12, etudiantId: 5, etat: 'InProgress', priorite: 'High', ownerId: 2, motif: 'Absences répétées', etudiant: { id: 5, prenom: 'Yassine', nom: 'Bennani' } }),
  buildCase({ id: 13, etudiantId: 6, etat: 'WaitingStudent', priorite: 'Critical', ownerId: 3, motif: 'Entretien planifié', meetingScheduledFor: '2026-07-01T10:00:00Z', etudiant: { id: 6, prenom: 'Imane', nom: 'Cherkaoui' } }),
  buildCase({ id: 14, etudiantId: 7, etat: 'Resolved', priorite: 'Medium', ownerId: 3, motif: 'Entretien réalisé', etudiant: { id: 7, prenom: 'Omar', nom: 'Tazi' } }),
  buildCase({ id: 15, etudiantId: 8, etat: 'Escalated', priorite: 'Critical', ownerId: null, motif: 'Escaladé au responsable', etudiant: { id: 8, prenom: 'Nadia', nom: 'Fassi' } }),
  buildCase({ id: 16, etudiantId: 9, etat: 'Closed', priorite: 'Low', ownerId: 2, motif: 'Suivi clôturé', etudiant: { id: 9, prenom: 'Karim', nom: 'Alaoui' } }),
]

function renderCases() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <Cases />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('Cases intervention queue', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    fetchCasesMock.mockResolvedValue(SAMPLE)
    fetchFilieresMock.mockResolvedValue([
      { id: 1, code: 'GI' },
      { id: 2, code: 'IA' },
    ])
  })

  it('renders the four admin next-action columns', async () => {
    renderCases()
    expect(await screen.findByRole('heading', { name: 'Interventions' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Nouveau' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'A contacter' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Rendez-vous' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Resolu' })).toBeInTheDocument()
  })

  it('places each case in its next-action column', async () => {
    renderCases()
    expect(await screen.findByRole('heading', { name: 'Interventions' })).toBeInTheDocument()

    // Wait for the first case card to appear (cases load async via useQuery).
    expect(await screen.findByRole('button', { name: /Sara Amrani/ })).toBeInTheDocument()

    const contact = screen.getByRole('region', { name: /A contacter/ })
    expect(within(contact).getByRole('button', { name: /Sara Amrani/ })).toBeInTheDocument()
    expect(within(contact).getByRole('button', { name: /Yassine Bennani/ })).toBeInTheDocument()

    const scheduled = screen.getByRole('region', { name: /Rendez-vous/ })
    expect(within(scheduled).getByRole('button', { name: /Imane Cherkaoui/ })).toBeInTheDocument()

    const held = screen.getByRole('region', { name: /Resolu/ })
    expect(within(held).getByRole('button', { name: /Omar Tazi/ })).toBeInTheDocument()
    expect(within(held).getByRole('button', { name: /Karim Alaoui/ })).toBeInTheDocument()
  })

  it('keeps escalated active cases visible on the board', async () => {
    renderCases()
    expect(await screen.findByRole('button', { name: /Nadia Fassi/ })).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: /A contacter/ })).getByRole('button', { name: /Nadia Fassi/ })).toBeInTheDocument()
  })

  it('puts unassigned open cases in Nouveau and assigned open cases in A contacter', async () => {
    fetchCasesMock.mockResolvedValue([
      buildCase({ id: 21, etat: 'Open', ownerId: null, motif: 'Non assigne', etudiant: { id: 1, prenom: 'Ali', nom: 'Naji' } }),
      buildCase({ id: 22, etat: 'Open', ownerId: 2, motif: 'Assigne', etudiant: { id: 2, prenom: 'Mina', nom: 'Bali' } }),
    ])
    renderCases()
    expect(await screen.findByRole('button', { name: /Ali Naji/ })).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Nouveau' })).getByRole('button', { name: /Ali Naji/ })).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'A contacter' })).getByRole('button', { name: /Mina Bali/ })).toBeInTheDocument()
  })

  it('clicking a primary card navigates to /cases/:id', async () => {
    const user = userEvent.setup()
    renderCases()
    const card = await screen.findByRole('button', { name: /Sara Amrani/ })
    await user.click(card)
    expect(mockNavigate).toHaveBeenCalledWith('/cases/11')
  })

  it('filters primary cards by owner (assigné / non assigné)', async () => {
    const user = userEvent.setup()
    renderCases()
    await screen.findByRole('heading', { name: 'Interventions' })

    const ownerFilter = screen.getByLabelText(/Assigné/) as HTMLSelectElement
    await user.selectOptions(ownerFilter, 'unassigned')

    // Nadia Fassi (Escalated, unassigned) is in the secondary list.
    expect(screen.getByRole('button', { name: /Nadia Fassi/ })).toBeInTheDocument()
    // Sara Amrani (Open, assigned) is filtered out.
    expect(screen.queryByRole('button', { name: /Sara Amrani/ })).not.toBeInTheDocument()
  })

  it('filters primary cards by priority', async () => {
    const user = userEvent.setup()
    renderCases()
    await screen.findByRole('heading', { name: 'Interventions' })

    const priorityFilter = screen.getByLabelText(/Priorité/) as HTMLSelectElement
    await user.selectOptions(priorityFilter, 'Critical')

    // Imane Cherkaoui (Critical, WaitingStudent) is shown.
    expect(screen.getByRole('button', { name: /Imane Cherkaoui/ })).toBeInTheDocument()
    // Sara Amrani (High) is filtered out.
    expect(screen.queryByRole('button', { name: /Sara Amrani/ })).not.toBeInTheDocument()
  })

  it('filters primary cards by filière', async () => {
    const user = userEvent.setup()
    renderCases()
    // Wait for cases to load before applying the filter.
    await screen.findByRole('button', { name: /Sara Amrani/ })

    const filiereFilter = screen.getByLabelText(/Filière/) as HTMLSelectElement
    await user.selectOptions(filiereFilter, 'IA')
    expect(screen.queryByRole('button', { name: /Sara Amrani/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Yassine Bennani/ })).not.toBeInTheDocument()
  })
})
