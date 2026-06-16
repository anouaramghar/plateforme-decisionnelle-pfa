import { useEffect, useState } from 'react'
import { Outlet } from 'react-router-dom'
import { CopilotPopup } from '@copilotkit/react-core/v2'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { CommandPalette } from './CommandPalette'
import { CopilotPanel } from '../components/copilot/CopilotPanel'

export function AppShell() {
  const [cmdOpen, setCmdOpen] = useState(false)
  const [copilotOpen, setCopilotOpen] = useState(false)

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        setCmdOpen(o => !o)
      }
      // ⌘J / Ctrl+J toggles the Copilot panel
      if ((e.metaKey || e.ctrlKey) && e.key === 'j') {
        e.preventDefault()
        setCopilotOpen(o => !o)
      }
      if (e.key === 'Escape') {
        setCmdOpen(false)
        // CopilotPanel handles its own ESC internally
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  return (
    <div className="vh-full flex" style={{ background: 'var(--bg)' }}>
      <Sidebar />
      <div className="flex-1 flex flex-col min-w-0">
        <Topbar
          onCommandOpen={() => setCmdOpen(true)}
          onCopilotOpen={() => setCopilotOpen(true)}
          copilotActive={copilotOpen}
        />
        <div className="flex-1 overflow-y-auto scroll-thin">
          <div className="px-8 py-7 max-w-[1480px] mx-auto">
            <Outlet />
          </div>
        </div>
      </div>
      <CommandPalette open={cmdOpen} onClose={() => setCmdOpen(false)} />
      {copilotOpen && <CopilotPanel onClose={() => setCopilotOpen(false)} />}
      <CopilotPopup
        labels={{ welcomeMessageText: 'Bonjour ! Comment puis-je vous aider ?' }}
      />
    </div>
  )
}
