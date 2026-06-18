import { AsyncLocalStorage } from "node:async_hooks";
import { defineTool } from "@copilotkit/runtime/v2";
import { z } from "zod";

export const authStore = new AsyncLocalStorage<string>();

const BACKEND_URL = process.env.BACKEND_URL ?? "http://localhost:5135";

async function backendGet(path: string): Promise<unknown> {
  const token = authStore.getStore() ?? "";
  const res = await fetch(`${BACKEND_URL}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) return { error: `Backend error ${res.status}` };
  return res.json();
}

async function backendPost(path: string, body: unknown): Promise<unknown> {
  const token = authStore.getStore() ?? "";
  const res = await fetch(`${BACKEND_URL}${path}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(body),
  });
  if (!res.ok) return { error: `Backend error ${res.status}` };
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
      const students = (await backendGet("/api/etudiants/with-stats")) as {
        matricule: string;
        nomComplet: string;
        filiereCode: string;
        niveau: string;
        moyenne: number;
        absences: number;
        scoreRisque: number;
        risque: string;
      }[];
      if (!Array.isArray(students)) return JSON.stringify(students);
      const student = students.find(
        (s) => s.matricule.toLowerCase() === matricule.toLowerCase()
      );
      if (!student) return JSON.stringify({ error: `Student ${matricule} not found` });
      return JSON.stringify({
        matricule: student.matricule,
        name: student.nomComplet,
        filiere: student.filiereCode,
        niveau: student.niveau,
        averageScore: student.moyenne,
        absences: student.absences,
        riskScore: student.scoreRisque,
        riskLevel: student.risque,
      });
    },
  }),

  defineTool({
    name: "list_at_risk",
    description:
      "List students whose risk score is above a threshold. " +
      "Optionally filter by filière (TCP, GI, IA, ROC, IRSI) or niveau (CP1, CP2, CI1, CI2, CI3).",
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
      const students = (await backendGet("/api/etudiants/with-stats")) as {
        matricule: string;
        nomComplet: string;
        filiereCode: string;
        niveau: string;
        moyenne: number;
        absences: number;
        scoreRisque: number;
        risque: string;
      }[];
      if (!Array.isArray(students)) return JSON.stringify(students);
      const filtered = students.filter((s) => {
        if (s.scoreRisque < threshold) return false;
        if (filiere && s.filiereCode !== filiere) return false;
        if (niveau && s.niveau !== niveau) return false;
        return true;
      });
      return JSON.stringify(
        filtered
          .sort((a, b) => b.scoreRisque - a.scoreRisque)
          .slice(0, 20)
          .map((s) => ({
            matricule: s.matricule,
            name: s.nomComplet,
            filiere: s.filiereCode,
            niveau: s.niveau,
            averageScore: s.moyenne,
            absences: s.absences,
            riskScore: s.scoreRisque,
          }))
      );
    },
  }),

  defineTool({
    name: "get_dashboard_summary",
    description:
      "Get aggregated dashboard statistics: total students, average score, success rate, " +
      "active alerts count, top at-risk students, and performance by filière.",
    parameters: z.object({}),
    execute: async () => {
      const data = await backendGet("/api/dashboard/summary");
      return JSON.stringify(data);
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
      const students = (await backendGet("/api/etudiants/with-stats")) as {
        id: number;
        matricule: string;
        nomComplet: string;
        filiereCode: string;
        niveau: string;
        moyenne: number;
        absences: number;
        modulesValides: number;
        modulesTotal: number;
        scoreRisque: number;
        risque: string;
      }[];
      if (!Array.isArray(students)) return JSON.stringify(students);
      const student = students.find(
        (s) => s.matricule.toLowerCase() === matricule.toLowerCase()
      );
      if (!student) return JSON.stringify({ error: `Student ${matricule} not found` });

      const notes = await backendGet(`/api/etudiants/${student.id}/notes`);
      return JSON.stringify({
        student: {
          matricule: student.matricule,
          name: student.nomComplet,
          filiere: student.filiereCode,
          niveau: student.niveau,
          averageScore: student.moyenne,
          absences: student.absences,
          modulesValidated: student.modulesValides,
          modulesTotal: student.modulesTotal,
          riskScore: student.scoreRisque,
          riskLevel: student.risque,
        },
        gradeDetails: notes,
      });
    },
  }),
];
