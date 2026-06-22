import jwt from "jsonwebtoken";

export function parseCookies(cookieHeader: string | undefined): Record<string, string> {
  const list: Record<string, string> = {};
  if (!cookieHeader) return list;

  cookieHeader.split(";").forEach((cookie) => {
    const parts = cookie.split("=");
    const key = parts.shift();
    if (key) {
      list[key.trim()] = decodeURIComponent(parts.join("="));
    }
  });

  return list;
}

export function verifyCopilotToken(token: string): any {
  const secret = process.env.JWT_SECRET;
  if (!secret) {
    throw new Error("JWT_SECRET is not configured");
  }

  const opts: jwt.VerifyOptions = { algorithms: ["HS256"] };
  if (process.env.JWT_ISSUER) opts.issuer = process.env.JWT_ISSUER;
  if (process.env.JWT_AUDIENCE) opts.audience = process.env.JWT_AUDIENCE;

  return jwt.verify(token, secret, opts);
}
