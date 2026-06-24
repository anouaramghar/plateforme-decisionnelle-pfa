// AI-assisted outreach drafting with a deterministic fallback.
//
// The copilot-runtime (/api/outreach/draft) suggests a French invitation email.
// It is OPTIONAL: any failure (provider down, 401/422/503, timeout, bad shape)
// returns the deterministic fallback so the staff member always has a usable
// draft. The AI path only ever sees a first name + a short concern summary +
// the meeting logistics — never grades or risk scores.

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

function isValidDraft(d: unknown): d is OutreachDraft {
  return (
    typeof d === 'object' && d !== null &&
    typeof (d as OutreachDraft).subject === 'string' && (d as OutreachDraft).subject.length > 0 &&
    typeof (d as OutreachDraft).body === 'string' && (d as OutreachDraft).body.length > 0
  )
}

export async function generateOutreachDraft(input: OutreachDraftInput): Promise<OutreachDraft> {
  try {
    const res = await fetch('/api/outreach/draft', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input),
    })
    if (!res.ok) return fallbackOutreachDraft(input)
    const data = await res.json()
    return isValidDraft(data) ? { subject: data.subject, body: data.body } : fallbackOutreachDraft(input)
  } catch {
    return fallbackOutreachDraft(input)
  }
}
