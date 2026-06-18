import { useEffect, useState } from 'react'
import { Outlet } from 'react-router-dom'
import { CopilotSidebar, useCopilotChatConfiguration } from '@copilotkit/react-core/v2'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { CommandPalette } from './CommandPalette'
import { CopilotPanel } from '../components/copilot/CopilotPanel'

export function AppShell() {
  const [cmdOpen, setCmdOpen] = useState(false)
  const [copilotOpen, setCopilotOpen] = useState(false)
  const chatConfig = useCopilotChatConfiguration()
  
  const copilotKitOpen = chatConfig?.isModalOpen ?? false
  const setCopilotKitOpen = (open: boolean | ((o: boolean) => boolean)) => {
    if (chatConfig) {
      if (typeof open === 'function') {
        chatConfig.setModalOpen(open(chatConfig.isModalOpen))
      } else {
        chatConfig.setModalOpen(open)
      }
    }
  }

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        setCmdOpen(o => !o)
      }
      if ((e.metaKey || e.ctrlKey) && !e.shiftKey && e.key === 'j') {
        e.preventDefault()
        setCopilotOpen(o => !o)
      }
      if ((e.metaKey || e.ctrlKey) && !e.shiftKey && e.key.toLowerCase() === 'm') {
        e.preventDefault()
        setCopilotKitOpen(o => !o)
      }
      if (e.key === 'Escape') {
        setCmdOpen(false)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [chatConfig])

  return (
    <div className="vh-full flex" style={{ background: 'var(--bg)' }}>
      <Sidebar />
      <div className="flex-1 flex flex-col min-w-0">
        <Topbar
          onCommandOpen={() => setCmdOpen(true)}
          onCopilotOpen={() => setCopilotOpen(true)}
          onCopilotKitOpen={() => setCopilotKitOpen(o => !o)}
          copilotActive={copilotOpen}
          copilotKitActive={copilotKitOpen}
        />
        <div className="flex-1 overflow-y-auto scroll-thin">
          <div className="px-8 py-7 max-w-[1480px] mx-auto">
            <Outlet />
          </div>
        </div>
      </div>
      <CommandPalette open={cmdOpen} onClose={() => setCmdOpen(false)} />
      {copilotOpen && <CopilotPanel onClose={() => setCopilotOpen(false)} />}
      <CopilotSidebar
        defaultOpen={false}
        labels={{
          modalHeaderTitle: 'ENIAD Copilot · CopilotKit',
          welcomeMessageText: 'Bonjour ! Interrogez les données étudiantes, les scores de risque et le DW en langage naturel.',
        }}
      />
    </div>
  )
}
