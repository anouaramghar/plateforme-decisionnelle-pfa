import { test, expect, describe, vi } from "vitest";
import { createOutreachDraft, type OutreachInput } from "./outreach.js";

const validInput: OutreachInput = {
  firstName: "Sara",
  concernSummary: "Baisse des résultats ce semestre.",
  scheduledFor: "2026-07-01T10:00:00.000Z",
  location: "Salle B12",
};

describe("createOutreachDraft", () => {
  test("rejects output that exposes a risk score", async () => {
    const generate = vi.fn().mockResolvedValue({
      text: JSON.stringify({ subject: "Entretien", body: "Votre risque est 87%." }),
    });
    await expect(createOutreachDraft(validInput, generate)).rejects.toThrow("unsafe draft");
  });

  test("rejects a risk label", async () => {
    const generate = vi.fn().mockResolvedValue({
      text: JSON.stringify({ subject: "Entretien", body: "Vous êtes en haut risque." }),
    });
    await expect(createOutreachDraft(validInput, generate)).rejects.toThrow("unsafe draft");
  });

  test("rejects non-JSON output", async () => {
    const generate = vi.fn().mockResolvedValue({ text: "désolé, je ne peux pas." });
    await expect(createOutreachDraft(validInput, generate)).rejects.toThrow("invalid draft");
  });

  test("returns a short French draft", async () => {
    const generate = vi.fn().mockResolvedValue({
      text: JSON.stringify({ subject: "Invitation à un entretien", body: "Bonjour Sara…" }),
    });
    await expect(createOutreachDraft(validInput, generate)).resolves.toMatchObject({
      subject: expect.any(String),
      body: expect.any(String),
    });
  });

  test("tolerates a ```json fenced response", async () => {
    const generate = vi.fn().mockResolvedValue({
      text: "```json\n{ \"subject\": \"Invitation\", \"body\": \"Bonjour Sara, rencontrons-nous.\" }\n```",
    });
    await expect(createOutreachDraft(validInput, generate)).resolves.toMatchObject({
      subject: "Invitation",
    });
  });
});
