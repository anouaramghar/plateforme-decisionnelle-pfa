import { useCallback, useEffect, useState } from 'react'
import { Outlet } from 'react-router-dom'
import { CopilotSidebar, useCopilotChatConfiguration } from '@copilotkit/react-core/v2'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { CommandPalette } from './CommandPalette'
import { useAuth } from '../context/AuthContext'

export function AppShell() {
  const [cmdOpen, setCmdOpen] = useState(false)
  const { copilotActive } = useAuth()
  const chatConfig = useCopilotChatConfiguration()
  const [localOpen, setLocalOpen] = useState(false)

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
      <Sidebar />
      <div className="flex-1 flex flex-col min-w-0">
        <Topbar
          onCommandOpen={() => setCmdOpen(true)}
          onCopilotOpen={() => setCopilotOpen(o => !o)}
          copilotActive={copilotOpen}
        />
        <div className="flex-1 overflow-y-auto scroll-thin">
          <div className="px-8 py-7 max-w-[1480px] mx-auto">
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
        <div className="copilot-panel fixed right-0 top-0 bottom-0 w-80 bg-white border-l shadow-lg flex flex-col z-50">
          <div className="p-4 border-b flex justify-between items-center">
            <h3 className="font-semibold text-lg">ENIAD Copilot</h3>
            <button onClick={() => setCopilotOpen(false)} className="text-gray-500 hover:text-gray-700">✕</button>
          </div>
          <div className="flex-1 p-6 flex flex-col items-center justify-center text-center">
            <div className="text-amber-500 mb-4">
              <svg className="w-12 h-12 mx-auto" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
            </div>
            <p className="text-gray-900 font-medium mb-1">Copilot indisponible</p>
            <p className="text-gray-500 text-sm">
              La session de chat sécurisée n'a pas pu être établie. Les fonctionnalités d'intelligence artificielle sont temporairement désactivées.
            </p>
          </div>
        </div>
      ) : null}
    </div>
  )
}
