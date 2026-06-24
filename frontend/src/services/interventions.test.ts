import { describe, it, expect, vi, beforeEach } from 'vitest'

vi.mock('./api', () => ({
  api: {
    get: vi.fn(() => Promise.resolve({ data: {} })),
    post: vi.fn(() => Promise.resolve({ data: {} })),
    put: vi.fn(() => Promise.resolve({ data: {} })),
    patch: vi.fn(() => Promise.resolve({ data: {} })),
  },
}))

import { api } from './api'
import {
  createOutreachDraft,
  updateOutreachDraft,
  scheduleAndSendOutreach,
  retryOutreach,
  recordMeetingResult,
} from './interventions'

beforeEach(() => {
  vi.clearAllMocks()
})

describe('outreach API contracts', () => {
  it('createOutreachDraft posts the draft body', async () => {
    await createOutreachDraft(12, { subject: 'Hi', body: 'Bonjour' })
    expect(api.post).toHaveBeenCalledWith('/intervention-cases/12/outreach/draft', { subject: 'Hi', body: 'Bonjour' })
  })

  it('updateOutreachDraft puts to the draft id', async () => {
    await updateOutreachDraft(12, 7, { subject: 'Hi', body: 'Bonjour' })
    expect(api.put).toHaveBeenCalledWith('/intervention-cases/12/outreach/draft/7', { subject: 'Hi', body: 'Bonjour' })
  })

  it('scheduleAndSendOutreach posts to the send route', async () => {
    const scheduledFor = '2026-07-01T10:00:00.000Z'
    await scheduleAndSendOutreach(12, 7, { scheduledFor, location: 'Salle B12' })
    expect(api.post).toHaveBeenCalledWith(
      '/intervention-cases/12/outreach/draft/7/send',
      { scheduledFor, location: 'Salle B12' },
    )
  })

  it('retryOutreach posts to the retry route', async () => {
    await retryOutreach(12, 7)
    expect(api.post).toHaveBeenCalledWith('/intervention-cases/12/outreach/draft/7/retry')
  })

  it('recordMeetingResult posts the meeting result', async () => {
    await recordMeetingResult(12, { attendance: 'Held', heldAt: '2026-07-01T10:00:00.000Z', outcome: 'Improved', summary: 'OK' })
    expect(api.post).toHaveBeenCalledWith(
      '/intervention-cases/12/outreach/meeting-result',
      { attendance: 'Held', heldAt: '2026-07-01T10:00:00.000Z', outcome: 'Improved', summary: 'OK' },
    )
  })
})
