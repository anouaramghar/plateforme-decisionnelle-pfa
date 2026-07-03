import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { AppShell } from './AppShell'
import * as AuthContext from '../context/AuthContext'

vi.mock('./Sidebar', () => ({ Sidebar: () => <div>Sidebar</div> }))
vi.mock('./CommandPalette', () => ({
  CommandPalette: ({ open }: { open: boolean }) => open ? <div>Palette</div> : null,
}))
vi.mock('./Topbar', () => ({
  Topbar: ({ onCopilotOpen }: { onCopilotOpen: () => void }) => (
    <button type="button" onClick={onCopilotOpen}>Ouvrir ENIAD Copilot</button>
  ),
}))
vi.mock('@copilotkit/react-core/v2', () => ({
  CopilotChat: () => <div data-testid="copilot-chat">Chat</div>,
  useConfigureSuggestions: () => {},
}))

function renderShell(copilotActive = true) {
  vi.spyOn(AuthContext, 'useAuth').mockReturnValue({
    token: 'token',
    user: { email: 'admin@eniad.ma', role: 'Admin', nom: 'Admin' },
    copilotActive,
    login: vi.fn(),
    logout: vi.fn(),
  })
  return render(<MemoryRouter><AppShell /></MemoryRouter>)
}

describe('AppShell Copilot drawer', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('is closed by default and opens only from the launcher', async () => {
    const user = userEvent.setup()
    renderShell()

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Ouvrir ENIAD Copilot' }))

    expect(screen.getByRole('dialog', { name: 'ENIAD Copilot' })).toBeInTheDocument()
    expect(screen.getByTestId('copilot-chat')).toBeInTheDocument()
  })

  it('closes on Escape and restores focus to the launcher', async () => {
    const user = userEvent.setup()
    renderShell()
    const launcher = screen.getByRole('button', { name: 'Ouvrir ENIAD Copilot' })

    await user.click(launcher)
    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(launcher).toHaveFocus()
  })

  it('keeps the platform available when Copilot is unavailable', async () => {
    const user = userEvent.setup()
    renderShell(false)
    await user.click(screen.getByRole('button', { name: 'Ouvrir ENIAD Copilot' }))

    expect(screen.getByText('Copilot indisponible')).toBeInTheDocument()
    expect(screen.queryByTestId('copilot-chat')).not.toBeInTheDocument()
  })
})
