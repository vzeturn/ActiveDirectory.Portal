import { isAxiosError } from 'axios'
import { apiClient } from './api'
import type { ApiResponse, DashboardSummary, HealthDetail, AdScope } from '../types'

function unwrap(err: unknown, fallback: string): never {
  if (isAxiosError<ApiResponse<unknown>>(err) && err.response?.data?.error) {
    throw new Error(err.response.data.error)
  }
  throw err instanceof Error ? err : new Error(fallback)
}

export async function getDashboardSummary(scope: AdScope = 'ou'): Promise<DashboardSummary> {
  try {
    const { data } = await apiClient.get<ApiResponse<DashboardSummary>>('/dashboard/summary', { params: { scope } })
    if (!data.success || !data.data) throw new Error(data.error ?? 'Failed to load dashboard.')
    return data.data
  } catch (err) {
    unwrap(err, 'Failed to load dashboard.')
  }
}

export async function getHealthDetail(): Promise<HealthDetail> {
  try {
    const { data } = await apiClient.get<ApiResponse<HealthDetail>>('/health/detail')
    if (!data.success || !data.data) throw new Error(data.error ?? 'Failed to load health.')
    return data.data
  } catch (err) {
    unwrap(err, 'Failed to load health.')
  }
}
