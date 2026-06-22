import { useQuery } from '@tanstack/react-query'
import { api } from './api'

interface PaginatedResult<T> { items: T[]; total: number }

/**
 * Shared unresolved-alert count for the sidebar + topbar badges. A single query
 * key means both consumers dedupe onto one `/alertes` poll every 30s instead of
 * firing the same request twice. Invalidating ['alertes'] (the list) also
 * refreshes this by prefix match.
 *
 * Reads the server-side `total` for unresolved alerts (pageSize=1) rather than
 * counting a fetched page — the API clamps pages to 100, so counting items
 * silently undercounts once there are more than 100 unresolved alerts.
 */
export function useUnresolvedAlertCount(enabled: boolean) {
  return useQuery({
    queryKey: ['alertes', 'unresolved-count'],
    queryFn: async () => {
      const res = await api.get<PaginatedResult<unknown>>('/alertes?resolue=false&pageSize=1')
      return res.data.total
    },
    enabled,
    refetchInterval: 30_000,
    staleTime: 25_000,
  })
}
