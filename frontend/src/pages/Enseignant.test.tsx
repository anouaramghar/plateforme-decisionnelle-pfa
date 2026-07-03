import { render, screen, within, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach, type Mock } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import Enseignant from './Enseignant'
import { api } from '../services/api'
import type { TeacherFollowUpCard } from '../services/interventions'

vi.mock('../context/AuthContext', () => ({
  useAuth: () => ({ user: { nom: 'Professeur' } }),
}))

vi.mock('../services/api', () => ({
  api: { get: vi.fn(), post: vi.fn() },
}))

const monModule = {
  moduleId: 1,
  moduleCode: 'MATH',
  moduleNom: 'Analyse',
  semestre: 'S1',
  coefficient: 2,
  filiereId: 1,
  filiereCode: 'GI',
  filiereIntitule: 'Genie informatique',
  niveau: '1A',
}

const followUps: TeacherFollowUpCard[] = [
  {
    caseId: 1,
    etudiantId: 10,
    studentName: 'Sara Amrani',
    motif: 'Absences repetees',
    priority: 'High',
    column: 'A voir',
    lastAction: 'Aucun contact',
    creeLe: '2026-06-29T09:00:00Z',
  },
  {
    caseId: 2,
    etudiantId: 11,
    studentName: 'Yassine Bennani',
    motif: 'Notes en baisse',
    priority: 'Medium',
    column: 'En suivi',
    lastAction: null,
    creeLe: '2026-06-29T10:00:00Z',
  },
  {
    caseId: 3,
    etudiantId: 12,
    studentName: 'Imane Cherkaoui',
    motif: 'Situation stabilisee',
    priority: 'Low',
    column: 'Traite',
    lastAction: 'Cloture',
    creeLe: '2026-06-29T11:00:00Z',
  },
]

function renderEnseignant() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <Enseignant />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('Enseignant follow-up board', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    ;(api.get as Mock).mockImplementation((url: string) => {
      if (url === '/enseignant/mon-module') return Promise.resolve({ data: monModule })
      if (url === '/enseignant/suivi') return Promise.resolve({ data: followUps })
      if (url === '/enseignant/mes-etudiants') return Promise.resolve({ data: [] })
      return Promise.resolve({ data: [] })
    })
    ;(api.post as Mock).mockResolvedValue({ data: {} })
  })

  it('renders heading "Suivi étudiants" and three regions', async () => {
    renderEnseignant()

    expect(await screen.findByRole('heading', { name: 'Suivi étudiants' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'À voir' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'En suivi' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Traité' })).toBeInTheDocument()
  })

  it('submits an observation with non-empty text', async () => {
    const user = userEvent.setup()
    renderEnseignant()

    const todo = await screen.findByRole('region', { name: 'À voir' })
    await user.click(await within(todo).findByRole('button', { name: 'Ajouter observation' }))
    await user.type(screen.getByLabelText('Observation'), 'Vu apres le cours')
    await user.click(screen.getByRole('button', { name: 'Enregistrer' }))

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/enseignant/suivi/1/observation', { contenu: 'Vu apres le cours' })
    })
  })

  it('submits an empty intervention request', async () => {
    const user = userEvent.setup()
    renderEnseignant()

    const suivi = await screen.findByRole('region', { name: 'En suivi' })
    await user.click(await within(suivi).findByRole('button', { name: 'Demander intervention' }))
    await user.click(screen.getByRole('button', { name: 'Enregistrer' }))

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/enseignant/suivi/2/request-intervention', { contenu: '' })
    })
  })

  it('does not show action buttons on Traité cards', async () => {
    renderEnseignant()

    const treated = await screen.findByRole('region', { name: 'Traité' })
    expect(await within(treated).findByText('Imane Cherkaoui')).toBeInTheDocument()
    expect(within(treated).queryByRole('button', { name: 'Ajouter observation' })).not.toBeInTheDocument()
    expect(within(treated).queryByRole('button', { name: 'Demander intervention' })).not.toBeInTheDocument()
    expect(within(treated).queryByRole('button', { name: 'Marquer traité' })).not.toBeInTheDocument()
  })
})
