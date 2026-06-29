import "dotenv/config";
import express from "express";
import { CopilotRuntime, BuiltInAgent, createCopilotExpressHandler } from "@copilotkit/runtime/v2";
import { createOpenAI } from "@ai-sdk/openai";
import { generateText } from "ai";
import { authStore, serverTools } from "./tools.js";
import { createOutreachDraft, outreachInput } from "./outreach.js";
import { requireEnv } from "./config.js";

const nvidiaApiKey = requireEnv("NVIDIA_NIM_API_KEY");
requireEnv("JWT_SECRET");
requireEnv("AGENT_INTERNAL_TOKEN");

const nim = createOpenAI({
  baseURL: process.env.NVIDIA_NIM_BASE_URL ?? "https://integrate.api.nvidia.com/v1",
  apiKey: nvidiaApiKey,
  // Force sequential tool calls. llama-3.3-70b on NIM otherwise emits parallel
  // tool calls that CopilotKit's agent loop fails to reassemble, throwing
  // AI_MissingToolResultsError. Inject parallel_tool_calls:false on tool requests.
  fetch: (async (url: string, options: RequestInit) => {
    if (options && typeof options.body === "string") {
      try {
        const body = JSON.parse(options.body);
        if (Array.isArray(body.tools) && body.tools.length > 0) {
          body.parallel_tool_calls = false;
          options = { ...options, body: JSON.stringify(body) };
        }
      } catch { /* non-JSON body — leave untouched */ }
    }
    return fetch(url, options);
  }) as typeof fetch,
});

const agent = new BuiltInAgent({
  // .chat() forces the OpenAI Chat Completions API (/v1/chat/completions).
  // Default nim(model) uses the Responses API (/v1/responses), which NVIDIA NIM
  // does not implement → 404 "Not Found".
  model: nim.chat(process.env.COPILOT_MODEL_ROUTER ?? "meta/llama-3.3-70b-instruct"),
  prompt:
    "Tu es ENIAD Copilot, un assistant d'aide à la décision pour le personnel " +
    "pédagogique de l'ENIAD (École Nationale d'Ingénieurs et d'Architectes, Berkane, Maroc). " +
    "Réponds en français, de façon concise et factuelle. " +
    "Tu as accès aux données des étudiants via les outils disponibles.",
  maxSteps: 10,
  tools: serverTools,
});

const runtime = new CopilotRuntime({
  agents: { default: agent },
});

const app = express();

const allowedOrigin = process.env.FRONTEND_URL ?? "http://localhost:5173";
app.use((req, res, next) => {
  res.setHeader("Access-Control-Allow-Origin", allowedOrigin);
  res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
  res.setHeader("Access-Control-Allow-Credentials", "true");
  if (req.method === "OPTIONS") return res.sendStatus(200);
  next();
});

// Unauthenticated liveness probe for the Docker healthcheck / depends_on.
app.get("/health", (_req, res) => {
  res.json({ status: "ok" });
});

import { parseCookies, verifyCopilotToken } from "./auth.js";

// Capture JWT per-request from httpOnly cookie, verify its signature, and run tool handlers within ALS.
app.use("/api/copilotkit", (req, res, next) => {
  if (req.method === "OPTIONS") return next();

  const cookies = parseCookies(req.headers.cookie);
  const token = cookies.pfa_copilot_session;

  if (!token) {
    return res.status(401).json({ error: "Missing session cookie" });
  }

  try {
    verifyCopilotToken(token);
  } catch (err: any) {
    console.error("JWT validation error:", err.message);
    return res.status(401).json({ error: "Invalid or expired session cookie" });
  }

  authStore.run(token, () => next());
});

app.use(
  "/api/copilotkit",
  createCopilotExpressHandler({
    runtime,
    basePath: "/",
    mode: "single-route",
  })
);

// ── Optional AI drafting for the student-outreach flow ──────────────────────
// Same httpOnly-cookie verification as /api/copilotkit. The backend remains
// authoritative for persistence/delivery; this only suggests draft text, and
// the frontend always has a deterministic French fallback if this is down.
app.use("/api/outreach", (req, res, next) => {
  if (req.method === "OPTIONS") return next();
  const cookies = parseCookies(req.headers.cookie);
  const token = cookies.pfa_copilot_session;
  if (!token) return res.status(401).json({ error: "Missing session cookie" });
  try {
    verifyCopilotToken(token);
  } catch (err: any) {
    console.error("JWT validation error:", err.message);
    return res.status(401).json({ error: "Invalid or expired session cookie" });
  }
  next();
});

app.post("/api/outreach/draft", express.json(), async (req, res) => {
  const parsed = outreachInput.safeParse(req.body);
  if (!parsed.success) return res.status(400).json({ error: "Invalid input" });

  try {
    const draft = await createOutreachDraft(parsed.data, async ({ system, user }) => {
      const { text } = await generateText({
        model: nim.chat(process.env.COPILOT_MODEL_ROUTER ?? "meta/llama-3.3-70b-instruct"),
        system,
        prompt: user,
      });
      return { text };
    });
    return res.json(draft);
  } catch (err: any) {
    const msg = String(err?.message ?? "");
    // Bad/unsafe model output → 422 (client falls back). Provider/network → 503.
    if (msg.startsWith("invalid draft") || msg.startsWith("unsafe draft")) {
      return res.status(422).json({ error: "Model returned invalid output" });
    }
    console.error("Outreach draft generation failed:", msg);
    return res.status(503).json({ error: "AI provider unavailable" });
  }
});

const port = Number(process.env.PORT ?? 4000);
app.listen(port, () => {
  console.log(`CopilotKit runtime → http://localhost:${port}/api/copilotkit`);
});
