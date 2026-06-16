/**
 * Quick smoke test: confirms the Vite app loads with CopilotKit styles injected.
 * The popup itself is inside a ProtectedRoute — run with the backend up for full test.
 */
import { chromium } from 'playwright';
import { writeFileSync } from 'fs';
import { join } from 'path';

const VITE_URL = 'http://localhost:5173';
const ADMIN_EMAIL = process.env.ADMIN_EMAIL ?? 'admin@eniad.ma';
const ADMIN_PASS  = process.env.ADMIN_PASS  ?? 'Admin@ENIAD2025';
const BACKEND_UP  = process.env.BACKEND_UP  === '1';

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();
page.on('console', m => console.log('[browser]', m.type(), m.text()));

// ── Step 1: load the app ───────────────────────────────────────────────────
console.log('Loading', VITE_URL, '...');
await page.goto(VITE_URL, { waitUntil: 'networkidle' });
const title = await page.title();
console.log('Page title:', title);

// Confirm CopilotKit styles are injected (the provider loads its stylesheet)
const ckStyles = await page.evaluate(() =>
  [...document.styleSheets].some(s => {
    try { return s.href?.includes('copilotkit') || [...s.cssRules].some(r => r.cssText?.includes('copilotkit')); }
    catch { return false; }
  })
);
console.log('CopilotKit stylesheet injected:', ckStyles);

const shot1 = join(process.cwd(), 'screenshot-login.png');
await page.screenshot({ path: shot1, fullPage: false });
console.log('Screenshot →', shot1);

// ── Step 2: if backend is up, login and check for the popup ───────────────
if (BACKEND_UP) {
  console.log('\nBackend flag set — attempting login...');
  await page.fill('input[type="email"]', ADMIN_EMAIL);
  await page.fill('input[type="password"]', ADMIN_PASS);
  await page.click('button[type="submit"]');
  await page.waitForURL('**/dashboard', { timeout: 8000 });
  console.log('Logged in, on dashboard');

  // Wait for popup button to appear (CopilotKit mounts a toggle button)
  const popup = await page.waitForSelector(
    '[data-testid="copilotkit-popup-toggle"], button[aria-label*="opilot"], .copilotkit-popup-button, .copilot-popup',
    { timeout: 5000 }
  ).catch(() => null);

  console.log('CopilotPopup button found:', popup !== null);

  const shot2 = join(process.cwd(), 'screenshot-dashboard.png');
  await page.screenshot({ path: shot2, fullPage: false });
  console.log('Screenshot →', shot2);

  if (popup) {
    await popup.click();
    await page.waitForTimeout(800);
    const shot3 = join(process.cwd(), 'screenshot-popup-open.png');
    await page.screenshot({ path: shot3, fullPage: false });
    console.log('Screenshot (popup open) →', shot3);
  }
} else {
  console.log('\nBackend not running — skipping login. Set BACKEND_UP=1 to test the popup.');
  console.log('Start order: docker compose up  OR  dotnet run (backend) + npm run dev (copilot-runtime)');
}

await browser.close();
