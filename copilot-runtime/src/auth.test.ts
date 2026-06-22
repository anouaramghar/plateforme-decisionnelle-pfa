import { test, expect, describe, beforeAll } from "vitest";
import { parseCookies, verifyCopilotToken } from "./auth.js";
import jwt from "jsonwebtoken";

describe("auth tests", () => {
  beforeAll(() => {
    process.env.JWT_SECRET = "super-secret-key-1234567890-abcdef";
    process.env.JWT_ISSUER = "pfa_issuer";
    process.env.JWT_AUDIENCE = "pfa_audience";
  });

  test("parseCookies extracts correct values", () => {
    const header = "pfa_copilot_session=my-jwt-token; other_cookie=xyz";
    const cookies = parseCookies(header);
    expect(cookies.pfa_copilot_session).toBe("my-jwt-token");
    expect(cookies.other_cookie).toBe("xyz");
  });

  test("verifyCopilotToken succeeds on valid token", () => {
    const payload = { userId: 1, role: "Admin" };
    const token = jwt.sign(payload, process.env.JWT_SECRET!, {
      issuer: "pfa_issuer",
      audience: "pfa_audience",
      expiresIn: "15m",
    });

    const verified = verifyCopilotToken(token);
    expect(verified.userId).toBe(1);
    expect(verified.role).toBe("Admin");
  });

  test("verifyCopilotToken throws on invalid token", () => {
    expect(() => verifyCopilotToken("invalid-token")).toThrow();
  });
});
