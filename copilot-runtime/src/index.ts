import "dotenv/config";
import express from "express";
import { CopilotRuntime, BuiltInAgent } from "@copilotkit/runtime/v2";
import { createCopilotExpressHandler } from "@copilotkit/runtime/v2/express";

const agent = new BuiltInAgent({
  model: "anthropic/claude-sonnet-4-6",
  prompt:
    "Tu es ENIAD Copilot, un assistant d'aide à la décision pour le personnel " +
    "pédagogique de l'ENIAD (École Nationale d'Ingénieurs et d'Architectes, Berkane, Maroc). " +
    "Réponds en français, de façon concise et factuelle.",
});

const runtime = new CopilotRuntime({
  agents: { default: agent },
});

const app = express();

app.use(
  "/api/copilotkit",
  createCopilotExpressHandler({
    runtime,
    basePath: "/",
    mode: "single-route",
  }),
);

const port = Number(process.env.PORT ?? 4000);
app.listen(port, () => {
  console.log(`CopilotKit runtime → http://localhost:${port}/api/copilotkit`);
});
