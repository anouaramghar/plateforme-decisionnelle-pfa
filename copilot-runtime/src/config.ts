const PLACEHOLDER_RE = /^(change_me|placeholder|your_|example|test-token-not-placeholder)/i;

export function requireEnv(name: string, value = process.env[name]): string {
  const trimmed = value?.trim() ?? "";
  if (!trimmed || PLACEHOLDER_RE.test(trimmed)) {
    throw new Error(`${name} is missing or still set to a placeholder value`);
  }
  return trimmed;
}
