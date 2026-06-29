import { describe, expect, test } from "vitest";
import { requireEnv } from "./config.js";

describe("runtime config", () => {
  test("requireEnv rejects missing or placeholder values", () => {
    expect(() => requireEnv("NVIDIA_NIM_API_KEY", undefined)).toThrow(/NVIDIA_NIM_API_KEY/);
    expect(() => requireEnv("AGENT_INTERNAL_TOKEN", "")).toThrow(/AGENT_INTERNAL_TOKEN/);
    expect(() => requireEnv("JWT_SECRET", "CHANGE_ME_SHARED_SECRET")).toThrow(/JWT_SECRET/);
  });

  test("requireEnv returns real values", () => {
    expect(requireEnv("AGENT_INTERNAL_TOKEN", "real-secret-123")).toBe("real-secret-123");
  });
});
