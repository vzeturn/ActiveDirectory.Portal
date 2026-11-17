import { useQuery } from '@tanstack/react-query'
import { getHealthDetail } from '../../services/dashboard.service'
import type { HealthDetail } from '../../types'

export function useHealthCheck() {
  return useQuery<HealthDetail, Error>({
    queryKey: ['health'],
    queryFn: () => getHealthDetail(),
    staleTime: 10_000
  })
}
