import { AsyncLocalStorage } from "node:async_hooks";
import { defineTool } from "@copilotkit/runtime/v2";
import { z } from "zod";
import { requireEnv } from "./config.js";

export const authStore = new AsyncLocalStorage<string>();

const BACKEND_URL = process.env.BACKEND_URL ?? "http://localhost:5135";
const AGENT_INTERNAL_TOKEN = requireEnv("AGENT_INTERNAL_TOKEN");

async function backendToolCall(name: string, args: unknown): Promise<unknown> {
  const token = authStore.getStore() ?? "";
  const res = await fetch(`${BACKEND_URL}/api/copilot/tool/${name}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
      "X-Internal-Token": AGENT_INTERNAL_TOKEN,
    },
    body: JSON.stringify({ args }),
  });
  if (!res.ok) return { ok: false, error: `Backend error ${res.status}` };
  return res.json();
}

export const serverTools = [
  defineTool({
    name: "get_student",
    description:
      "Fetch a student's profile and academic stats by matricule (e.g. E10001). " +
      "Returns name, filière, niveau, average score, absences, and risk score.",
    parameters: z.object({
      matricule: z.string().describe("Student matricule, e.g. E10001"),
    }),
    execute: async ({ matricule }) => {
      const result = await backendToolCall("get_student", { matricule });
      return JSON.stringify(result);
    },
  }),

  defineTool({
    name: "list_at_risk",
    description:
      "List students whose risk score is above a threshold. " +
      "Optionally filter by filière (EPSI, IA, ROC, IRSI, GINF) or niveau (CP1, CP2, CI1, CI2, CI3).",
    parameters: z.object({
      threshold: z
        .number()
        .min(0)
        .max(1)
        .optional()
        .describe("Minimum risk score (0.0–1.0). Default 0.6."),
      filiere: z.string().optional().describe("Filière code filter (optional)"),
      niveau: z.string().optional().describe("Academic level filter (optional)"),
    }),
    execute: async ({ threshold = 0.6, filiere, niveau }) => {
      const result = await backendToolCall("list_at_risk", {
        threshold,
        filiere,
        niveau,
      });
      return JSON.stringify(result);
    },
  }),

  defineTool({
    name: "query_dw",
    description:
      "Answer a natural-language question about aggregated student data using the data warehouse. " +
      "Examples: 'How many students are enrolled?', 'Average score by filière', 'Modules with highest failure rate'.",
    parameters: z.object({
      question: z
        .string()
        .describe("Natural language question about student data."),
    }),
    execute: async ({ question }) => {
      const result = await backendToolCall("query_dw", { question });
      return JSON.stringify(result);
    },
  }),

  defineTool({
    name: "draft_alert",
    description:
      "Draft an alert for a student that requires human confirmation before being sent. " +
      "This tool does NOT send the alert — it creates a draft that the user must approve in the UI. " +
      "Requires matricule, severity (low/medium/high), and a message_fr explaining the reason in French.",
    parameters: z.object({
      matricule: z.string().describe("Student matricule, e.g. E10001"),
      severity: z
        .enum(["low", "medium", "high"])
        .describe("Alert severity level"),
      message_fr: z
        .string()
        .describe("Alert message in French explaining the reason"),
    }),
    execute: async ({ matricule, severity, message_fr }) => {
      const result = await backendToolCall("draft_alert", {
        matricule,
        severity,
        message_fr,
      });
      return JSON.stringify(result);
    },
  }),

  defineTool({
    name: "explain_risk",
    description:
      "Explain why a specific student has their current risk score by listing key factors: " +
      "their grades per module, absence count, failed modules, and overall average.",
    parameters: z.object({
      matricule: z.string().describe("Student matricule to explain risk for"),
    }),
    execute: async ({ matricule }) => {
      const result = await backendToolCall("explain_risk", { matricule });
      return JSON.stringify(result);
    },
  }),
];
