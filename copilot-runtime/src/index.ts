import "dotenv/config";
import express from "express";
import { CopilotRuntime, BuiltInAgent, createCopilotExpressHandler } from "@copilotkit/runtime/v2";
import { createOpenAI } from "@ai-sdk/openai";
import { authStore, serverTools } from "./tools.js";

const nim = createOpenAI({
  baseURL: process.env.NVIDIA_NIM_BASE_URL ?? "https://integrate.api.nvidia.com/v1",
  apiKey: process.env.NVIDIA_NIM_API_KEY,
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

import jwt from "jsonwebtoken";

// Capture JWT per-request, verify its signature, and run tool handlers within ALS.
app.use("/api/copilotkit", (req, res, next) => {
  if (req.method === "OPTIONS") return next();

  const token = (req.headers.authorization ?? "").replace(/^Bearer\s+/i, "");
  const secret = process.env.JWT_SECRET;

  // CopilotKit v2's discovery (/info) and streaming agent requests do NOT carry
  // the client's custom `headers`, so requiring a JWT on every request breaks the
  // client (401 → "Agent not found"). Verify the token WHEN one is present
  // (rejects tampering) and capture it for backend tool forwarding; otherwise let
  // the request through.
  // ponytail: dev-grade gate. The real security boundary is the backend tool
  // route ([Authorize] user JWT + internal token). For production, authenticate
  // the CopilotKit stream via an httpOnly cookie or a signed query param instead.
  if (token && secret) {
    try {
      const opts: jwt.VerifyOptions = { algorithms: ["HS256"] };
      if (process.env.JWT_ISSUER) opts.issuer = process.env.JWT_ISSUER;
      if (process.env.JWT_AUDIENCE) opts.audience = process.env.JWT_AUDIENCE;
      jwt.verify(token, secret, opts);
    } catch (err: any) {
      console.error("JWT validation error:", err.message);
      return res.status(401).json({ error: "Invalid token signature" });
    }
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

const port = Number(process.env.PORT ?? 4000);
app.listen(port, () => {
  console.log(`CopilotKit runtime → http://localhost:${port}/api/copilotkit`);
});
