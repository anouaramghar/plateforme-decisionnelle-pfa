import "dotenv/config";
import express from "express";
import { CopilotRuntime, BuiltInAgent, createCopilotExpressHandler } from "@copilotkit/runtime/v2";
import { authStore, serverTools } from "./tools.js";

const agent = new BuiltInAgent({
  model: "anthropic/claude-sonnet-4-5",
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

// Capture JWT per-request so tool handlers can forward it to the backend.
app.use("/api/copilotkit", (req, _res, next) => {
  const token = (req.headers.authorization ?? "").replace(/^Bearer\s+/i, "");
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
