import { useQuery } from '@tanstack/react-query'
import { getDashboardSummary } from '../../services/dashboard.service'
import type { AdScope, DashboardSummary } from '../../types'

export function useDashboardSummary(scope: AdScope) {
  return useQuery<DashboardSummary, Error>({
    queryKey: ['dashboard', scope],
    queryFn: () => getDashboardSummary(scope)
  })
}
