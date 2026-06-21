import "dotenv/config";
import express from "express";
import { CopilotRuntime, BuiltInAgent, createCopilotExpressHandler } from "@copilotkit/runtime/v2";
import { createOpenAI } from "@ai-sdk/openai";
import { authStore, serverTools } from "./tools.js";

const nim = createOpenAI({
  baseURL: process.env.NVIDIA_NIM_BASE_URL ?? "https://integrate.api.nvidia.com/v1",
  apiKey: process.env.NVIDIA_NIM_API_KEY,
});

const agent = new BuiltInAgent({
  model: nim(process.env.COPILOT_MODEL_ROUTER ?? "meta/llama-3.3-70b-instruct"),
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
  const token = (req.headers.authorization ?? "").replace(/^Bearer\s+/i, "");
  if (!token) {
    return res.status(401).json({ error: "Missing authorization token" });
  }

  const secret = process.env.JWT_SECRET;
  if (!secret) {
    console.error("JWT_SECRET environment variable is not configured");
    return res.status(500).json({ error: "Server configuration error" });
  }

  try {
    // Pin the algorithm (rejects alg-confusion / "none") and validate issuer +
    // audience when configured, matching the backend's token validation.
    const opts: jwt.VerifyOptions = { algorithms: ["HS256"] };
    if (process.env.JWT_ISSUER) opts.issuer = process.env.JWT_ISSUER;
    if (process.env.JWT_AUDIENCE) opts.audience = process.env.JWT_AUDIENCE;
    jwt.verify(token, secret, opts);
    authStore.run(token, () => next());
  } catch (err: any) {
    console.error("JWT validation error:", err.message);
    return res.status(401).json({ error: "Invalid token signature" });
  }
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
