// AI-assisted outreach drafting with a deterministic fallback.
//
// The backend endpoint (/api/outreach/draft) generates a personalised French
// invitation email using EmailTemplates.RenderMeeting. It is OPTIONAL: any
// failure (network, bad shape) returns the deterministic fallback so the staff
// member always has a usable draft.

import { api } from './api'

export interface OutreachDraftInput {
  firstName: string
  concernSummary: string
  scheduledFor: string // ISO date-time
  location: string
}

export interface OutreachDraft {
  subject: string
  body: string
}

export function fallbackOutreachDraft(input: OutreachDraftInput): OutreachDraft {
  const when = new Date(input.scheduledFor).toLocaleString('fr-FR', { dateStyle: 'long', timeStyle: 'short' })
  return {
    subject: 'Invitation à un entretien — ENIAD',
    body: `Bonjour ${input.firstName},\n\nNous souhaitons vous rencontrer afin de faire le point sur votre parcours et vous proposer un accompagnement adapté.\n\nRendez-vous : ${when}\nLieu : ${input.location}\n\nCordialement,\nL'équipe pédagogique ENIAD`,
  }
}

export async function generateOutreachDraft(input: OutreachDraftInput): Promise<OutreachDraft> {
  try {
    const data = await api.post<OutreachDraft>('/outreach/draft', input).then(r => r.data)
    if (data && typeof data.subject === 'string' && data.subject.length > 0 &&
        typeof data.body === 'string' && data.body.length > 0) {
      return { subject: data.subject, body: data.body }
    }
    return fallbackOutreachDraft(input)
  } catch {
    return fallbackOutreachDraft(input)
  }
}
