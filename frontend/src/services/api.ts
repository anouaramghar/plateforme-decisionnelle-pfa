import axios from 'axios'

// In-memory token store — never persisted to localStorage / sessionStorage
// (XSS hardening per CLAUDE.md). The trade-off is a page refresh logs the
// user out; acceptable for a PFA demo. Switch to httpOnly cookie if/when the
// backend sets one.
let authToken: string | null = null
let onUnauthorized: (() => void) | null = null

export function setAuthToken(token: string | null): void {
  authToken = token
}

export function setOnUnauthorized(handler: (() => void) | null): void {
  onUnauthorized = handler
}

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
})

api.interceptors.request.use((config) => {
  if (authToken) config.headers.Authorization = `Bearer ${authToken}`
  return config
})

api.interceptors.response.use(
  (res) => res,
  (err) => {
    if (err.response?.status === 401) {
      authToken = null
      onUnauthorized?.()
    }
    return Promise.reject(err)
  }
)
