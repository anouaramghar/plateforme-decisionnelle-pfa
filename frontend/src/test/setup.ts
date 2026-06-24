import '@testing-library/jest-dom/vitest'
import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'

// vitest runs with globals: false, so @testing-library/react's auto-cleanup
// (which keys off global afterEach) doesn't fire. Wire it up here so every
// test file gets a clean DOM between runs without each one re-declaring it.
afterEach(() => {
  cleanup()
})
