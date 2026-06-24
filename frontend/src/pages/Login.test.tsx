import { describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Login from './Login'
import { useAuth } from '../context/AuthContext'

vi.mock('../context/AuthContext', () => ({ useAuth: vi.fn() }))

describe('Login', () => {
  it('navigates after the authenticated user has been committed', async () => {
    const user = userEvent.setup()
    const login = vi.fn().mockResolvedValue('Responsable')
    let authenticatedUser: { email: string; role: string; nom: string } | null = null

    vi.mocked(useAuth).mockImplementation(() => ({
      token: authenticatedUser ? 'token' : null,
      user: authenticatedUser,
      copilotActive: false,
      login,
      logout: vi.fn(),
    }))

    const renderTree = () => (
      <MemoryRouter initialEntries={['/login']}>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/responsable" element={<div>Espace Responsable</div>} />
        </Routes>
      </MemoryRouter>
    )
    const view = render(renderTree())

    await user.type(screen.getByRole('textbox', { name: /adresse e-mail/i }), 'responsable@eniad.ma')
    const password = view.container.querySelector<HTMLInputElement>('input[name="password"]')
    expect(password).not.toBeNull()
    await user.type(password!, 'mot-de-passe')
    await user.click(screen.getByRole('button', { name: /se connecter/i }))

    await waitFor(() => expect(login).toHaveBeenCalledTimes(1))
    authenticatedUser = { email: 'responsable@eniad.ma', role: 'Responsable', nom: 'Responsable' }
    view.rerender(renderTree())

    expect(await screen.findByText('Espace Responsable')).toBeInTheDocument()
  })
})
