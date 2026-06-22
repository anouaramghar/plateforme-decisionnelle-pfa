/// <reference types="vitest" />
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// `process` is a Node global at config-evaluation time; we don't pull in
// @types/node just for one call so we cast through unknown to keep TS happy.
const cwd = (globalThis as unknown as { process: { cwd(): string } }).process.cwd()

export default defineConfig(({ mode }) => {
  // Loads .env / .env.<mode> so devs can override the proxy target without
  // editing this file:  VITE_API_PROXY_TARGET=http://localhost:80 npm run dev
  const env = loadEnv(mode, cwd, '')

  return {
    plugins: [react()],
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      globals: true,
    },
    server: {
      port: 5173,
      proxy: {
        // Copilot runtime runs separately (`npm run dev` in copilot-runtime,
        // port 4000). Must be listed BEFORE '/api' so this more specific prefix
        // wins. Routing it same-origin lets the httpOnly session cookie flow.
        '/api/copilotkit': {
          target: env.VITE_COPILOT_PROXY_TARGET || 'http://localhost:4000',
          changeOrigin: true,
        },
        // Default target = the backend dev server (`dotnet run` listens on 5135).
        // Set VITE_API_PROXY_TARGET=http://localhost:80 when using the full docker stack.
        '/api': {
          target: env.VITE_API_PROXY_TARGET || 'http://localhost:5135',
          changeOrigin: true,
        },
      },
    },
  }
})
