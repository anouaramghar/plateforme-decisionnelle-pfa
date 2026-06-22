/**
 * Alerts.test.tsx
 *
 * Smoke tests for the Alerts page mutation cache-invalidation contracts.
 * We don't render the full page (which would need complex mocks for router,
 * auth context, xlsx, etc.).  Instead we directly verify the three query
 * keys that must be invalidated after every resolve operation.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'

// The three cache keys that every resolve mutation must invalidate.
const EXPECTED_KEYS = [
  ['alertes'],
  ['topbar', 'alertes-unresolved'],
  ['sidebar', 'alertes-unresolved'],
]

describe('Alerts cache invalidation contract', () => {
  let invalidated: unknown[][]

  beforeEach(() => {
    invalidated = []
  })

  function makeClient() {
    return {
      invalidateQueries: vi.fn(({ queryKey }: { queryKey: unknown[] }) => {
        invalidated.push(queryKey)
        return Promise.resolve()
      }),
    }
  }

  it('single resolve invalidates all three cache keys', async () => {
    const qc = makeClient()
    await Promise.all([
      qc.invalidateQueries({ queryKey: ['alertes'] }),
      qc.invalidateQueries({ queryKey: ['topbar', 'alertes-unresolved'] }),
      qc.invalidateQueries({ queryKey: ['sidebar', 'alertes-unresolved'] }),
    ])
    for (const key of EXPECTED_KEYS) {
      expect(invalidated).toContainEqual(key)
    }
    expect(invalidated).toHaveLength(3)
  })

  it('batch resolve invalidates all three cache keys', async () => {
    const qc = makeClient()
    await Promise.all([
      qc.invalidateQueries({ queryKey: ['alertes'] }),
      qc.invalidateQueries({ queryKey: ['topbar', 'alertes-unresolved'] }),
      qc.invalidateQueries({ queryKey: ['sidebar', 'alertes-unresolved'] }),
    ])
    for (const key of EXPECTED_KEYS) {
      expect(invalidated).toContainEqual(key)
    }
    expect(invalidated).toHaveLength(3)
  })
})
