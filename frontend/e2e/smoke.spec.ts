import { test, expect, type Page } from '@playwright/test'

// Smoke tests — drive the critical auth + protected-route path end-to-end
// against a live stack. Covers the headline user journey:
//   login → dashboard → predictions → alerts
//
// NB: the frontend keeps the JWT in memory only (XSS hardening, see
// services/api.ts), so a full page reload logs the user out. We therefore
// navigate between pages via sidebar clicks (client-side routing) instead of
// page.goto() — which would reset the app and lose the token.
//
// Credentials: set E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD to the same values as
// ADMIN_SEED_EMAIL / ADMIN_SEED_PASSWORD in .env. The suite refuses to run
// without E2E_ADMIN_PASSWORD so a wrong default can't silently fail.

const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? 'admin@eniad.ma'
const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? ''

test.beforeAll(() => {
  if (!ADMIN_PASSWORD) {
    throw new Error(
      'E2E_ADMIN_PASSWORD is not set. Set it to the same value as ADMIN_SEED_PASSWORD ' +
        'in .env (and E2E_ADMIN_EMAIL if you changed the seed email), then re-run `npm run e2e`.',
    )
  }
})

async function loginAsAdmin(page: Page): Promise<void> {
  await page.goto('/login')
  await page.locator('input[type="email"]').fill(ADMIN_EMAIL)
  await page.locator('input[type="password"]').fill(ADMIN_PASSWORD)
  await page.getByRole('button', { name: /Se connecter/i }).click()
  // Admin home is /dashboard (see src/auth/roles.ts homeFor).
  await page.waitForURL('**/dashboard', { timeout: 20_000 })
}

test('unauthenticated visit redirects to /login', async ({ page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL(/\/login/)
})

test('login → dashboard → predictions → alerts', async ({ page }) => {
  await loginAsAdmin(page)
  await expect(page).toHaveURL(/\/dashboard/)
  // App shell mounted — sidebar nav present.
  await expect(page.getByRole('link', { name: /Tableau de bord/ })).toBeVisible()

  // Predictions page — navigate via sidebar (preserves the in-memory JWT).
  await page.getByRole('link', { name: /Prédictions ML/ }).click()
  await expect(page).toHaveURL(/\/predictions/)
  // Not bounced back to login.
  await expect(page.locator('input[type="email"]')).toHaveCount(0)

  // Alerts page.
  await page.getByRole('link', { name: /Alertes/ }).click()
  await expect(page).toHaveURL(/\/alerts/)
  await expect(page.locator('input[type="email"]')).toHaveCount(0)
})

// End-to-end outreach loop: alert → start intervention → prepare draft →
// schedule/send → record meeting → verify resolved. The AI draft endpoint
// (/api/outreach/draft) is stubbed so the test doesn't depend on the
// copilot-runtime. Backend integration tests cover real send-state behaviour.
test('outreach loop: alert → email → meeting → resolved', async ({ page }) => {
  // Stub the AI draft so the test is deterministic.
  await page.route('**/api/outreach/draft', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        subject: 'Invitation à un entretien — ENIAD',
        body: 'Bonjour, nous souhaitons vous rencontrer pour faire le point sur votre parcours.',
      }),
    })
  })

  await loginAsAdmin(page)

  // Navigate to the alerts page (triage tab).
  await page.getByRole('link', { name: /Alertes/ }).click()
  await expect(page).toHaveURL(/\/alerts/)

  // Find the first "Ouvrir un cas" button in the triage queue.
  const startButton = page.getByRole('button', { name: /Ouvrir un cas/i }).first()
  await expect(startButton).toBeVisible({ timeout: 10_000 })
  await startButton.click()

  // We should be on the case detail page.
  await expect(page).toHaveURL(/\/cases\/\d+/)

  // The outreach composer for an Open case asks for date + location first.
  await expect(page.getByText(/Préparer l.invitation/)).toBeVisible()
  const dateInput = page.getByLabel(/Date et heure/i)
  const locInput = page.getByLabel(/Lieu/i)
  await locInput.fill('Salle B12')
  // Fill the datetime-local with a date 2 days from now.
  const future = new Date(Date.now() + 2 * 24 * 60 * 60 * 1000)
  const pad = (n: number) => String(n).padStart(2, '0')
  const dt = `${future.getFullYear()}-${pad(future.getMonth() + 1)}-${pad(future.getDate())}T10:00`
  await dateInput.fill(dt)
  await page.getByRole('button', { name: /Préparer un brouillon/i }).click()

  // The draft form appears — verify and send.
  await expect(page.getByLabel(/Sujet/i)).toBeVisible({ timeout: 10_000 })
  await page.getByLabel(/Lieu/i).fill('Salle B12')
  await page.getByLabel(/Date et heure/i).fill(dt)
  await page.getByRole('button', { name: /Vérifier et envoyer/i }).click()

  // Confirmation dialog — confirm the send.
  await expect(page.getByRole('dialog', { name: /Confirmer l.envoi/i })).toBeVisible()
  await page.getByRole('button', { name: /Envoyer l.email/i }).click()

  // After the send, the case either moves to "Entretien planifié" (WaitingStudent)
  // or shows a failure with "Renvoyer". Wait for either outcome.
  const meetingSection = page.getByText(/Résultat de l.entretien/i)
  const retryButton = page.getByRole('button', { name: /Renvoyer/i })

  // If the send succeeded, the meeting outcome form appears.
  await expect(meetingSection.or(retryButton)).toBeVisible({ timeout: 15_000 })

  // If the send failed, retry once.
  if (await retryButton.isVisible()) {
    await retryButton.click()
    await expect(meetingSection).toBeVisible({ timeout: 15_000 })
  }

  // Record the meeting as Held.
  await page.getByLabel(/Présence/i).selectOption('Held')
  const heldAt = new Date()
  const heldDt = `${heldAt.getFullYear()}-${pad(heldAt.getMonth() + 1)}-${pad(heldAt.getDate())}T10:00`
  await page.getByLabel(/Heure réelle/i).fill(heldDt)
  await page.getByLabel(/Résultat/i).selectOption('Improved')
  await page.getByLabel(/Résumé/i).fill('Entretien réalisé avec succès.')
  await page.getByRole('button', { name: /Enregistrer l.entretien/i }).click()

  // The case should now show "Entretien réalisé" (Resolved).
  await expect(page.getByText(/Entretien réalisé/i)).toBeVisible({ timeout: 10_000 })
})
