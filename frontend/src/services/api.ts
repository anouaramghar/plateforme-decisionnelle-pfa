import axios, { AxiosError, AxiosRequestConfig } from 'axios'

// In-memory token store — never persisted to localStorage / sessionStorage
// (XSS hardening per CLAUDE.md). The trade-off is a page refresh logs the
// user out; acceptable for a PFA demo. Switch to httpOnly cookie if/when the
// backend sets one.
let authToken: string | null = null
let refreshToken: string | null = null
let onUnauthorized: (() => void) | null = null
let onTokenRefreshed: ((newToken: string, newRefresh: string) => void) | null = null

// Monotonic session id. Logout and login bump it so a refresh that was already
// in flight when the session changed cannot commit its result over the new one
// (or resurrect a token after logout).
let sessionGeneration = 0

export function bumpSession(): void {
  sessionGeneration += 1
}

export function setAuthToken(token: string | null): void {
  authToken = token
}

export function getAuthToken(): string | null {
  return authToken
}

export function setRefreshToken(token: string | null): void {
  refreshToken = token
}

export function getRefreshToken(): string | null {
  return refreshToken
}

export function setOnUnauthorized(handler: (() => void) | null): void {
  onUnauthorized = handler
}

export function setOnTokenRefreshed(
  handler: ((newToken: string, newRefresh: string) => void) | null,
): void {
  onTokenRefreshed = handler
}

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
})

api.interceptors.request.use((config) => {
  if (authToken) config.headers.Authorization = `Bearer ${authToken}`
  return config
})

// Single-flight refresh: while one refresh is pending, queue subsequent 401s
// onto the same promise so we don't fire N parallel /auth/refresh calls when
// the JWT expires mid-page.
let pendingRefresh: Promise<string | null> | null = null

async function refreshOnce(): Promise<string | null> {
  if (!refreshToken) return null
  if (pendingRefresh) return pendingRefresh

  const startGeneration = sessionGeneration
  pendingRefresh = (async () => {
    try {
      const res = await axios.post(
        `${api.defaults.baseURL}/auth/refresh`,
        { refreshToken },
        { headers: { 'Content-Type': 'application/json' } },
      )
      // If logout/login happened while this was in flight, drop the result so
      // it can't clobber the new session (or revive a logged-out one).
      if (sessionGeneration !== startGeneration) return null
      const newToken: string = res.data.token
      const newRefresh: string = res.data.refreshToken
      authToken    = newToken
      refreshToken = newRefresh
      onTokenRefreshed?.(newToken, newRefresh)
      return newToken
    } catch {
      return null
    } finally {
      pendingRefresh = null
    }
  })()

  return pendingRefresh
}

api.interceptors.response.use(
  (res) => res,
  async (err: AxiosError) => {
    const original = err.config as (AxiosRequestConfig & { _retry?: boolean }) | undefined

    // Only attempt refresh once per request, and never on the refresh endpoint
    // itself (otherwise a bad refresh token causes infinite recursion).
    const isRefreshCall = original?.url?.includes('/auth/refresh')

    if (err.response?.status === 401 && original && !original._retry && !isRefreshCall) {
      original._retry = true
      const newToken = await refreshOnce()
      if (newToken) {
        original.headers = { ...original.headers, Authorization: `Bearer ${newToken}` }
        return api.request(original)
      }
      // Refresh failed — wipe state and bounce to /login.
      authToken    = null
      refreshToken = null
      onUnauthorized?.()
    }

    return Promise.reject(err)
  },
)
