import { useCallback, useEffect, useState } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { CopilotSidebar, useCopilotChatConfiguration } from '@copilotkit/react-core/v2'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { CommandPalette } from './CommandPalette'
import { Icon } from '../components/ui/Icon'
import { useAuth } from '../context/AuthContext'

export function AppShell() {
  const [cmdOpen, setCmdOpen] = useState(false)
  const [navOpen, setNavOpen] = useState(false)
  const { pathname } = useLocation()
  const { copilotActive } = useAuth()
  const chatConfig = useCopilotChatConfiguration()
  const [localOpen, setLocalOpen] = useState(false)

  // Close the mobile nav drawer whenever the route changes.
  useEffect(() => { setNavOpen(false) }, [pathname])

  const copilotOpen = copilotActive ? (chatConfig?.isModalOpen ?? false) : localOpen
  const setCopilotOpen = useCallback((open: boolean | ((o: boolean) => boolean)) => {
    if (copilotActive) {
      if (chatConfig) {
        if (typeof open === 'function') {
          chatConfig.setModalOpen(open(chatConfig.isModalOpen))
        } else {
          chatConfig.setModalOpen(open)
        }
      }
    } else {
      if (typeof open === 'function') {
        setLocalOpen(o => open(o))
      } else {
        setLocalOpen(open)
      }
    }
  }, [chatConfig, copilotActive])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        setCmdOpen(o => !o)
      }
      if ((e.metaKey || e.ctrlKey) && e.key === 'j') {
        e.preventDefault()
        setCopilotOpen(o => !o)
      }
      if (e.key === 'Escape') {
        setCmdOpen(false)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [chatConfig, setCopilotOpen])

  return (
    <div className="vh-full flex" style={{ background: 'var(--bg)' }}>
      {/* Mobile drawer backdrop */}
      {navOpen && (
        <div
          className="fixed inset-0 z-40 lg:hidden"
          style={{ background: 'rgba(12,10,9,0.42)' }}
          onClick={() => setNavOpen(false)}
        />
      )}
      {/* Sidebar: static on desktop, off-canvas drawer under lg */}
      <div
        className={`fixed inset-y-0 left-0 z-50 transition-transform duration-200 lg:static lg:z-auto lg:translate-x-0 ${
          navOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'
        }`}
      >
        <Sidebar />
      </div>
      <div className="flex-1 flex flex-col min-w-0">
        <Topbar
          onCommandOpen={() => setCmdOpen(true)}
          onCopilotOpen={() => setCopilotOpen(o => !o)}
          onMenuOpen={() => setNavOpen(true)}
          copilotActive={copilotOpen}
        />
        <div className="flex-1 overflow-y-auto scroll-thin">
          <div className="app-content px-4 sm:px-6 lg:px-8 py-5 lg:py-7 max-w-[1480px] mx-auto">
            <Outlet />
          </div>
        </div>
      </div>
      <CommandPalette open={cmdOpen} onClose={() => setCmdOpen(false)} />
      {copilotActive ? (
        <CopilotSidebar
          agentId="default"
          defaultOpen={true}
          labels={{
            modalHeaderTitle: 'ENIAD Copilot',
            welcomeMessageText: 'Bonjour ! Interrogez les données étudiantes, les scores de risque et le DW en langage naturel.',
          }}
        />
      ) : copilotOpen ? (
        <div
          className="copilot-panel fixed right-0 top-0 bottom-0 w-80 flex flex-col z-50"
          style={{ background: 'var(--surface)', borderLeft: '1px solid var(--border)', color: 'var(--text)' }}
        >
          <div className="p-4 flex justify-between items-center" style={{ borderBottom: '1px solid var(--border)' }}>
            <h3 className="font-semibold text-lg">ENIAD Copilot</h3>
            <button
              onClick={() => setCopilotOpen(false)}
              className="btn btn-sm btn-ghost"
              aria-label="Fermer le panneau Copilot"
            >
              <Icon name="x" size={14} />
            </button>
          </div>
          <div className="flex-1 p-6 flex flex-col items-center justify-center text-center">
            <div className="mb-4" style={{ color: 'var(--accent-500)' }}>
              <Icon name="alert" size={40} />
            </div>
            <p className="font-medium mb-1">Copilot indisponible</p>
            <p className="text-sm" style={{ color: 'var(--text-3)' }}>
              La session de chat sécurisée n'a pas pu être établie. Les fonctionnalités d'intelligence artificielle sont temporairement désactivées.
            </p>
          </div>
        </div>
      ) : null}
    </div>
  )
}
