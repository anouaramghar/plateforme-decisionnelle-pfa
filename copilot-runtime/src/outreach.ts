import { z } from "zod";

// Minimal, privacy-bounded input for AI drafting. NO grades, risk scores, or
// internal notes ever leave the cluster: only a first name, a short non-clinical
// concern summary, and the meeting logistics.
export const outreachInput = z.object({
  firstName: z.string().min(1).max(80),
  concernSummary: z.string().min(1).max(500),
  scheduledFor: z.string().datetime(),
  location: z.string().min(1).max(200),
});
export type OutreachInput = z.infer<typeof outreachInput>;

export const outreachOutput = z.object({
  subject: z.string().min(1).max(300),
  body: z.string().min(1).max(4000),
});
export type OutreachDraft = z.infer<typeof outreachOutput>;

// Defence-in-depth: reject any draft that leaks a percentage/score or a risk
// label even if the model ignores the prompt.
const UNSAFE = /\b\d{1,3}\s*%|score de risque|haut risque/i;

// Injected so tests run without a network call.
export type GenerateFn = (prompt: { system: string; user: string }) => Promise<{ text: string }>;

export function buildPrompt(input: OutreachInput): { system: string; user: string } {
  const when = new Date(input.scheduledFor).toLocaleString("fr-FR", {
    dateStyle: "long",
    timeStyle: "short",
  });
  const system =
    "Tu rédiges un email d'invitation à un entretien pour le personnel pédagogique de l'ENIAD. " +
    'Réponds UNIQUEMENT avec un objet JSON de la forme {"subject": string, "body": string}. ' +
    "Écris en français, de façon courte, bienveillante et encourageante. " +
    "N'inclus JAMAIS de score, de pourcentage, d'étiquette de risque, de note interne, " +
    "d'accusation ni de diagnostic. Invite simplement l'étudiant à un rendez-vous d'accompagnement.";
  const user =
    `Prénom de l'étudiant : ${input.firstName}\n` +
    `Contexte interne (ne pas citer mot pour mot) : ${input.concernSummary}\n` +
    `Date et heure du rendez-vous : ${when}\n` +
    `Lieu : ${input.location}`;
  return { system, user };
}

// Pull the first JSON object out of the model text, tolerating ```json fences.
function extractJson(text: string): string {
  const fenced = text.match(/```(?:json)?\s*([\s\S]*?)```/i);
  const candidate = fenced ? fenced[1] : text;
  const start = candidate.indexOf("{");
  const end = candidate.lastIndexOf("}");
  if (start === -1 || end === -1) return candidate.trim();
  return candidate.slice(start, end + 1);
}

/**
 * Pure drafting function. Validates input, asks `generate` for text, parses and
 * validates the JSON, and refuses unsafe output. Throws:
 *  - "invalid draft …" when the model returns non-JSON or the wrong shape
 *  - "unsafe draft …"  when the output exposes a score/percentage/risk label
 */
export async function createOutreachDraft(
  input: OutreachInput,
  generate: GenerateFn,
): Promise<OutreachDraft> {
  const parsedInput = outreachInput.parse(input);
  const { text } = await generate(buildPrompt(parsedInput));

  let json: unknown;
  try {
    json = JSON.parse(extractJson(text));
  } catch {
    throw new Error("invalid draft: model did not return JSON");
  }

  const parsed = outreachOutput.safeParse(json);
  if (!parsed.success) {
    throw new Error("invalid draft: model output did not match the expected shape");
  }
  if (UNSAFE.test(parsed.data.subject) || UNSAFE.test(parsed.data.body)) {
    throw new Error("unsafe draft: model output exposed prohibited content");
  }
  return parsed.data;
}
