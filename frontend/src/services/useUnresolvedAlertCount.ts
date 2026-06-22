import { useQuery } from '@tanstack/react-query'
import { api } from './api'

interface AlerteRow { resolue: boolean }
interface PaginatedResult<T> { items: T[]; total: number }

/**
 * Shared unresolved-alert count for the sidebar + topbar badges. A single query
 * key means both consumers dedupe onto one `/alertes` poll every 30s instead of
 * firing the same request twice. Invalidating ['alertes'] (the list) also
 * refreshes this by prefix match.
 */
export function useUnresolvedAlertCount(enabled: boolean) {
  return useQuery({
    queryKey: ['alertes', 'unresolved-count'],
    queryFn: async () => {
      const res = await api.get<PaginatedResult<AlerteRow>>('/alertes?pageSize=200')
      return res.data.items.filter(a => !a.resolue).length
    },
    enabled,
    refetchInterval: 30_000,
    staleTime: 25_000,
  })
}
