import '@testing-library/jest-dom/vitest'
import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'

// vitest runs with globals: false, so @testing-library/react's auto-cleanup
// (which keys off global afterEach) doesn't fire. Wire it up here so every
// test file gets a clean DOM between runs without each one re-declaring it.
afterEach(() => {
  cleanup()
})

// jsdom (as of 29.x) does not implement <dialog>.showModal() / .close().
// Polyfill enough behaviour for the Modal component's useEffect to work:
// toggle the `open` property so the dialog becomes queryable.
if (typeof HTMLDialogElement !== 'undefined') {
  const proto = HTMLDialogElement.prototype as unknown as Record<string, unknown>
  if (typeof proto.showModal !== 'function') {
    proto.showModal = function (this: HTMLDialogElement) { this.open = true }
  }
  if (typeof proto.close !== 'function') {
    proto.close = function (this: HTMLDialogElement) { this.open = false }
  }
}
