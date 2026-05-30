import { useState, useRef, useEffect, useCallback } from 'react'
import { Icon } from '../ui/Icon'
import { Pill } from '../ui/Pill'
import { api, getAuthToken } from '../../services/api'

// ── Domain types ─────────────────────────────────────────────────────────────

interface UserMessage {
  id: string
  role: 'user'
  text: string
}

interface ToolEvent {
  callId: string
  name: string
  args?: Record<string, unknown>
  // populated when tool_result arrives
  resolved: boolean
  ok?: boolean
  summary?: string
  error?: string
}

interface ConfirmData {
  draftId: number
  preview: {
    student_name?: string
    matricule?: string
    severity?: string
    message?: string
    expires_at?: string
  }
  tool: string
  state: 'pending' | 'confirmed' | 'dismissed'
}

interface AssistantMessage {
  id: string
  role: 'assistant'
  text: string
  done: boolean
  error?: string
  tools: ToolEvent[]
  confirm?: ConfirmData
}

type ChatMessage = UserMessage | AssistantMessage

// ── SSE stream reader ─────────────────────────────────────────────────────────

async function* readSseStream(
  response: Response,
): AsyncGenerator<{ event: string; data: Record<string, unknown> }> {
  if (!response.body) return
  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })

    const blocks = buffer.split('\n\n')
    buffer = blocks.pop() ?? ''

    for (const block of blocks) {
      let event = ''
      let dataStr = ''
      for (const line of block.split('\n')) {
        if (line.startsWith('event: ')) event = line.slice(7).trim()
        if (line.startsWith('data: ')) dataStr = line.slice(6).trim()
      }
      if (!event || !dataStr) continue
      try {
        yield { event, data: JSON.parse(dataStr) as Record<string, unknown> }
      } catch {
        // skip malformed SSE frame
      }
    }
  }
}

// ── Panel component ───────────────────────────────────────────────────────────

interface CopilotPanelProps {
  onClose: () => void
}

export function CopilotPanel({ onClose }: CopilotPanelProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState('')
  const [streaming, setStreaming] = useState(false)
  const [sessionId, setSessionId] = useState<string | undefined>()
  const bottomRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLTextAreaElement>(null)
  const abortRef = useRef<AbortController | null>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  useEffect(() => {
    inputRef.current?.focus()
  }, [])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !streaming) onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose, streaming])

  const setAssistant = useCallback(
    (id: string, updater: (prev: AssistantMessage) => AssistantMessage) => {
      setMessages(prev =>
        prev.map(m => (m.id === id && m.role === 'assistant' ? updater(m as AssistantMessage) : m)),
      )
    },
    [],
  )

  const sendMessage = useCallback(
    async (text: string) => {
      if (!text.trim() || streaming) return
      const token = getAuthToken()
      if (!token) return

      const userMsgId = crypto.randomUUID()
      const assistantMsgId = crypto.randomUUID()

      setInput('')
      setStreaming(true)
      setMessages(prev => [
        ...prev,
        { id: userMsgId, role: 'user', text } as UserMessage,
        { id: assistantMsgId, role: 'assistant', text: '', done: false, tools: [] } as AssistantMessage,
      ])

      const ctrl = new AbortController()
      abortRef.current = ctrl

      try {
        const baseUrl = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
        const resp = await fetch(`${baseUrl}/copilot/chat`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({ message: text, sessionId }),
          signal: ctrl.signal,
        })

        if (!resp.ok) {
          throw new Error(`HTTP ${resp.status}`)
        }

        for await (const { event, data } of readSseStream(resp)) {
          if (ctrl.signal.aborted) break

          if (event === 'token') {
            setAssistant(assistantMsgId, am => ({
              ...am,
              text: am.text + ((data.text as string) ?? ''),
            }))
          } else if (event === 'tool_call') {
            setAssistant(assistantMsgId, am => ({
              ...am,
              tools: [
                ...am.tools,
                {
                  callId: data.call_id as string,
                  name: data.name as string,
                  args: data.args as Record<string, unknown> | undefined,
                  resolved: false,
                },
              ],
            }))
          } else if (event === 'tool_result') {
            setAssistant(assistantMsgId, am => ({
              ...am,
              tools: am.tools.map(t =>
                t.callId === (data.call_id as string)
                  ? {
                      ...t,
                      resolved: true,
                      ok: data.ok as boolean,
                      summary: data.summary as string | undefined,
                      error: data.error as string | undefined,
                    }
                  : t,
              ),
            }))
          } else if (event === 'confirm_request') {
            setAssistant(assistantMsgId, am => ({
              ...am,
              confirm: {
                draftId: data.draft_id as number,
                preview: data.preview as ConfirmData['preview'],
                tool: data.tool as string,
                state: 'pending',
              },
            }))
          } else if (event === 'done') {
            setAssistant(assistantMsgId, am => ({ ...am, done: true }))
            // Extract session_id if ever added to done event
            if (data.session_id) setSessionId(data.session_id as string)
          } else if (event === 'error') {
            setAssistant(assistantMsgId, am => ({
              ...am,
              done: true,
              error: (data.message as string) ?? 'Erreur inconnue',
            }))
          }
        }
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return
        setAssistant(assistantMsgId, am => ({
          ...am,
          done: true,
          error: 'Impossible de joindre le Copilot. Vérifiez la connexion.',
        }))
      } finally {
        setStreaming(false)
        abortRef.current = null
      }
    },
    [streaming, sessionId, setAssistant],
  )

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      void sendMessage(input)
    }
  }

  const handleStop = () => {
    abortRef.current?.abort()
    setStreaming(false)
  }

  const handleConfirm = async (assistantMsgId: string, draftId: number) => {
    try {
      await api.post('/copilot/confirm', { draftId })
      setAssistant(assistantMsgId, am => ({
        ...am,
        confirm: am.confirm ? { ...am.confirm, state: 'confirmed' } : undefined,
      }))
    } catch {
      // leave card in pending state, user can retry
    }
  }

  const handleDismiss = (assistantMsgId: string) => {
    setAssistant(assistantMsgId, am => ({
      ...am,
      confirm: am.confirm ? { ...am.confirm, state: 'dismissed' } : undefined,
    }))
  }

  return (
    <div
      className="fixed inset-0 z-40"
      onClick={onClose}
      style={{ background: 'rgba(12,10,9,0.28)' }}
    >
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label="ENIAD Copilot"
        onClick={e => e.stopPropagation()}
        className="copilot-panel absolute right-0 top-0 bottom-0 flex flex-col"
        style={{
          width: 480,
          background: 'var(--surface)',
          borderLeft: '1px solid var(--border)',
          boxShadow: '-12px 0 40px rgba(0,0,0,0.10)',
        }}
      >
        {/* ── Header ── */}
        <div
          className="flex items-center gap-3 px-4 flex-shrink-0"
          style={{ height: 56, borderBottom: '1px solid var(--border)', background: 'var(--surface-2)' }}
        >
          <div
            className="w-8 h-8 rounded-xl flex items-center justify-center flex-shrink-0"
            style={{
              background: 'linear-gradient(135deg, var(--accent-500), var(--accent-700))',
              color: '#fff',
              boxShadow: 'inset 0 1px 0 rgba(255,255,255,.18), 0 2px 8px rgba(249,115,22,.3)',
            }}
          >
            <Icon name="brain" size={16} />
          </div>
          <div className="flex-1 min-w-0">
            <div className="text-[13px] font-semibold tracking-tight flex items-center gap-2">
              ENIAD Copilot
              <span className="flex items-center gap-1 text-[10.5px] font-normal" style={{ color: 'var(--ok)' }}>
                <span className="pill-dot live-dot" style={{ background: 'var(--ok)' }} />
                En ligne
              </span>
            </div>
            <div className="cap mt-0.5">Aide à la décision · 5 outils disponibles</div>
          </div>
          <button onClick={onClose} aria-label="Fermer" className="btn btn-sm btn-ghost">
            <Icon name="x" size={14} />
          </button>
        </div>

        {/* ── Messages ── */}
        <div className="flex-1 overflow-y-auto scroll-thin px-4 py-4 space-y-4 min-h-0">
          {messages.length === 0 && (
            <EmptyState onSelect={text => void sendMessage(text)} />
          )}
          {messages.map(msg =>
            msg.role === 'user' ? (
              <UserBubble key={msg.id} msg={msg as UserMessage} />
            ) : (
              <AssistantBubble
                key={msg.id}
                msg={msg as AssistantMessage}
                onConfirm={draftId => void handleConfirm(msg.id, draftId)}
                onDismiss={() => handleDismiss(msg.id)}
              />
            ),
          )}
          <div ref={bottomRef} />
        </div>

        {/* ── Input ── */}
        <div
          className="flex-shrink-0 px-4 pt-3 pb-4"
          style={{ borderTop: '1px solid var(--border)' }}
        >
          <div
            className="flex items-end gap-2 rounded-xl px-3 py-2.5"
            style={{ background: 'var(--surface-2)', border: '1px solid var(--border-2)' }}
          >
            <textarea
              ref={inputRef}
              value={input}
              onChange={e => setInput(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Posez une question sur les étudiants, les risques…"
              disabled={streaming}
              rows={1}
              className="flex-1 resize-none bg-transparent text-[13px] outline-none"
              style={{
                color: 'var(--text)',
                maxHeight: 120,
                overflow: 'auto',
                lineHeight: 1.55,
                fontFamily: 'inherit',
              }}
            />
            {streaming ? (
              <button
                onClick={handleStop}
                className="flex-shrink-0 w-7 h-7 rounded-lg flex items-center justify-center transition"
                title="Arrêter"
                style={{ background: 'var(--bad)', color: '#fff' }}
              >
                <Icon name="stop" size={11} strokeWidth={0} style={{ fill: 'currentColor' }} />
              </button>
            ) : (
              <button
                onClick={() => void sendMessage(input)}
                disabled={!input.trim()}
                className="flex-shrink-0 w-7 h-7 rounded-lg flex items-center justify-center transition"
                style={{
                  background: input.trim() ? 'var(--accent-500)' : 'var(--surface-3)',
                  color: input.trim() ? '#fff' : 'var(--text-4)',
                }}
              >
                <Icon name="send" size={12} strokeWidth={1.8} />
              </button>
            )}
          </div>
          <div className="mt-2 flex items-center justify-between px-0.5">
            <span className="cap text-[10.5px]">↵ Envoyer · ⇧↵ Saut de ligne</span>
            {streaming && (
              <span className="cap text-[10.5px] flex items-center gap-1" style={{ color: 'var(--accent-500)' }}>
                <span className="pill-dot live-dot" style={{ background: 'var(--accent-500)' }} />
                Génération en cours…
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

// ── Sub-components ────────────────────────────────────────────────────────────

function EmptyState({ onSelect }: { onSelect: (text: string) => void }) {
  const starters = [
    'Donne-moi le profil de l\'étudiant E10001',
    'Liste les étudiants avec un risque élevé (≥ 0.65)',
    'Explique le risque de l\'étudiant E10001',
    'Combien d\'étudiants sont enregistrés dans le DW ?',
  ]
  return (
    <div className="flex flex-col items-center text-center pt-6 pb-2 px-2">
      <div
        className="w-14 h-14 rounded-2xl flex items-center justify-center mb-5"
        style={{
          background: 'linear-gradient(135deg, var(--accent-50), var(--accent-100))',
          color: 'var(--accent-600)',
          border: '1px solid color-mix(in oklch, var(--accent-300) 35%, transparent)',
        }}
      >
        <Icon name="brain" size={24} />
      </div>
      <div className="text-[15px] font-semibold tracking-tight mb-1.5">ENIAD Copilot</div>
      <p className="text-[12.5px] max-w-[300px] mb-6 leading-relaxed" style={{ color: 'var(--text-3)' }}>
        Interrogez les données étudiantes, les scores de risque et l'entrepôt de données en langage naturel.
      </p>
      <div className="w-full space-y-2">
        <div className="cap text-left mb-1.5">Essayez par exemple :</div>
        {starters.map(s => (
          <button
            key={s}
            onClick={() => onSelect(s)}
            className="w-full text-left px-3 py-2.5 rounded-lg text-[12.5px] transition hover:border-stone-300"
            style={{
              background: 'var(--surface-2)',
              border: '1px solid var(--border)',
              color: 'var(--text-2)',
              lineHeight: 1.4,
            }}
          >
            <span style={{ color: 'var(--text-4)', marginRight: 6 }}>›</span>
            {s}
          </button>
        ))}
      </div>
    </div>
  )
}

function UserBubble({ msg }: { msg: UserMessage }) {
  return (
    <div className="flex justify-end">
      <div
        className="max-w-[340px] px-3.5 py-2.5 rounded-2xl rounded-tr-sm text-[13px] leading-relaxed"
        style={{ background: 'var(--accent-500)', color: '#fff' }}
      >
        {msg.text}
      </div>
    </div>
  )
}

function AssistantBubble({
  msg,
  onConfirm,
  onDismiss,
}: {
  msg: AssistantMessage
  onConfirm: (draftId: number) => void
  onDismiss: () => void
}) {
  const hasText = msg.text.length > 0
  const isThinking = !msg.done && !hasText && msg.tools.length === 0 && !msg.error

  return (
    <div className="flex items-start gap-2.5">
      {/* Avatar */}
      <div
        className="w-7 h-7 rounded-lg flex items-center justify-center flex-shrink-0 mt-0.5"
        style={{
          background: 'linear-gradient(135deg, var(--accent-500), var(--accent-700))',
          color: '#fff',
        }}
      >
        <Icon name="brain" size={12} />
      </div>

      <div className="flex-1 min-w-0 space-y-2">
        {/* Tool chips */}
        {msg.tools.map(tool => (
          <ToolChip key={tool.callId} tool={tool} />
        ))}

        {/* Confirm card */}
        {msg.confirm && (
          <ConfirmCard
            data={msg.confirm}
            onConfirm={() => onConfirm(msg.confirm!.draftId)}
            onDismiss={onDismiss}
          />
        )}

        {/* Text or thinking */}
        {(hasText || isThinking) && (
          <div
            className="max-w-[370px] px-3.5 py-2.5 rounded-2xl rounded-tl-sm text-[13px] leading-relaxed"
            style={{
              background: 'var(--surface-2)',
              border: '1px solid var(--border)',
              color: 'var(--text)',
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-word',
            }}
          >
            {isThinking ? (
              <ThinkingDots />
            ) : (
              <>
                {msg.text}
                {!msg.done && (
                  <span
                    className="ml-0.5 inline-block w-0.5 h-3.5 align-text-bottom rounded-full"
                    style={{ background: 'var(--accent-500)', animation: 'liveDot 0.9s ease-in-out infinite' }}
                  />
                )}
              </>
            )}
          </div>
        )}

        {/* Error */}
        {msg.error && (
          <div
            className="max-w-[370px] px-3 py-2 rounded-lg text-[12.5px] leading-relaxed"
            style={{
              background: 'color-mix(in oklch, var(--bad) 8%, transparent)',
              color: 'var(--bad)',
              border: '1px solid color-mix(in oklch, var(--bad) 18%, transparent)',
            }}
          >
            {msg.error}
          </div>
        )}
      </div>
    </div>
  )
}

function ToolChip({ tool }: { tool: ToolEvent }) {
  const label = tool.name.replace(/_/g, ' ')

  const argPreview = tool.args
    ? Object.entries(tool.args)
        .slice(0, 2)
        .map(([, v]) => String(v))
        .join(', ')
    : ''

  if (!tool.resolved) {
    return (
      <div
        className="flex items-center gap-2 px-2.5 py-1.5 rounded-lg text-[11.5px] max-w-[370px]"
        style={{ background: 'var(--surface-2)', border: '1px solid var(--border)', color: 'var(--text-3)' }}
      >
        <span className="pill-dot live-dot" style={{ background: 'var(--accent-500)', flexShrink: 0 }} />
        <span className="font-medium font-mono">{label}</span>
        {argPreview && <span className="truncate opacity-60">({argPreview})</span>}
      </div>
    )
  }

  return (
    <div
      className="flex items-center gap-2 px-2.5 py-1.5 rounded-lg text-[11.5px] max-w-[370px]"
      style={{
        background: tool.ok
          ? 'color-mix(in oklch, var(--ok) 8%, transparent)'
          : 'color-mix(in oklch, var(--bad) 8%, transparent)',
        border: tool.ok
          ? '1px solid color-mix(in oklch, var(--ok) 18%, transparent)'
          : '1px solid color-mix(in oklch, var(--bad) 18%, transparent)',
        color: tool.ok ? 'var(--ok)' : 'var(--bad)',
      }}
    >
      <Icon name={tool.ok ? 'check' : 'x'} size={11} strokeWidth={2.2} />
      <span className="font-medium font-mono">{label}</span>
      {!tool.ok && tool.error && (
        <span className="truncate max-w-[180px] opacity-75">{tool.error}</span>
      )}
    </div>
  )
}

function ConfirmCard({
  data,
  onConfirm,
  onDismiss,
}: {
  data: ConfirmData
  onConfirm: () => void
  onDismiss: () => void
}) {
  const [confirming, setConfirming] = useState(false)

  const severityLabel: Record<string, string> = { high: 'Haute', medium: 'Moyenne', low: 'Basse' }
  const severityTone: Record<string, 'bad' | 'warn' | 'info'> = { high: 'bad', medium: 'warn', low: 'info' }
  const tone = severityTone[data.preview.severity ?? 'medium'] ?? 'warn'
  const label = severityLabel[data.preview.severity ?? 'medium'] ?? data.preview.severity

  if (data.state === 'confirmed') {
    return (
      <div
        className="flex items-center gap-2 px-3 py-2.5 rounded-xl text-[12.5px] max-w-[370px]"
        style={{
          background: 'color-mix(in oklch, var(--ok) 8%, transparent)',
          border: '1px solid color-mix(in oklch, var(--ok) 18%, transparent)',
          color: 'var(--ok)',
        }}
      >
        <Icon name="check" size={13} strokeWidth={2.2} />
        Alerte créée pour {data.preview.student_name ?? data.preview.matricule}
      </div>
    )
  }

  if (data.state === 'dismissed') {
    return (
      <div
        className="px-3 py-2 rounded-lg text-[12px] cap max-w-[370px]"
        style={{ background: 'var(--surface-2)', color: 'var(--text-3)', border: '1px solid var(--border)' }}
      >
        Alerte annulée.
      </div>
    )
  }

  const handleConfirm = async () => {
    setConfirming(true)
    onConfirm()
    // optimistic — ConfirmCard will re-render with state=confirmed from parent
  }

  return (
    <div
      className="rounded-xl overflow-hidden max-w-[370px]"
      style={{ border: '1px solid var(--border)', background: 'var(--surface)' }}
    >
      <div
        className="px-3 py-2 flex items-center gap-2"
        style={{ background: 'var(--surface-2)', borderBottom: '1px solid var(--border)' }}
      >
        <Icon name="bell" size={12} style={{ color: 'var(--warn)' }} />
        <span className="text-[11.5px] font-medium">Brouillon d'alerte · confirmation requise</span>
        <span className="ml-auto cap text-[10.5px]" style={{ color: 'var(--text-4)' }}>
          expire dans 5 min
        </span>
      </div>
      <div className="p-3 space-y-2.5">
        <div className="flex items-center justify-between gap-2">
          <span className="text-[12.5px] font-semibold tracking-tight">
            {data.preview.student_name ?? data.preview.matricule}
          </span>
          <Pill tone={tone}>{label}</Pill>
        </div>
        {data.preview.message && (
          <p className="text-[12px] leading-relaxed" style={{ color: 'var(--text-2)' }}>
            {data.preview.message}
          </p>
        )}
        <div className="flex items-center gap-2 pt-0.5">
          <button
            className="btn btn-sm btn-accent flex-1"
            onClick={handleConfirm}
            disabled={confirming}
          >
            <Icon name="check" size={12} strokeWidth={2.2} />
            {confirming ? 'Envoi…' : 'Confirmer l\'envoi'}
          </button>
          <button className="btn btn-sm" onClick={onDismiss}>
            Annuler
          </button>
        </div>
      </div>
    </div>
  )
}

function ThinkingDots() {
  return (
    <span className="flex items-center gap-1 h-5">
      {[0, 1, 2].map(i => (
        <span
          key={i}
          className="think-dot inline-block w-1.5 h-1.5 rounded-full"
          style={{
            background: 'var(--text-3)',
            animationDelay: `${i * 0.18}s`,
          }}
        />
      ))}
    </span>
  )
}
