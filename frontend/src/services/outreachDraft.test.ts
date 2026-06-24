import { describe, it, expect, vi, afterEach } from 'vitest'
import { generateOutreachDraft, fallbackOutreachDraft, type OutreachDraftInput } from './outreachDraft'

const input: OutreachDraftInput = {
  firstName: 'Sara',
  concernSummary: 'Baisse des résultats',
  scheduledFor: '2026-07-01T10:00:00.000Z',
  location: 'Salle B12',
}

afterEach(() => {
  vi.restoreAllMocks()
})

describe('fallbackOutreachDraft', () => {
  it('produces a French invitation with the name, location, and a subject', () => {
    const d = fallbackOutreachDraft(input)
    expect(d.subject).toMatch(/ENIAD/)
    expect(d.body).toContain('Sara')
    expect(d.body).toContain('Salle B12')
  })
})

describe('generateOutreachDraft', () => {
  it('returns the AI draft when the runtime responds with a valid one', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ subject: 'Invitation', body: 'Bonjour Sara, rencontrons-nous.' }),
    }))
    const d = await generateOutreachDraft(input)
    expect(d).toEqual({ subject: 'Invitation', body: 'Bonjour Sara, rencontrons-nous.' })
  })

  it('falls back when the runtime returns a non-OK status', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 503, json: async () => ({}) }))
    const d = await generateOutreachDraft(input)
    expect(d).toEqual(fallbackOutreachDraft(input))
  })

  it('falls back when the response shape is invalid', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ subject: '' }) }))
    const d = await generateOutreachDraft(input)
    expect(d).toEqual(fallbackOutreachDraft(input))
  })

  it('falls back when fetch rejects (network/timeout)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network')))
    const d = await generateOutreachDraft(input)
    expect(d).toEqual(fallbackOutreachDraft(input))
  })
})
