import { apiClient } from './api'
import type { ApiResponse, AuditEntry } from '../types'

export interface AuditFilters {
  from?: string
  to?: string
  actor?: string
  operation?: string
  page?: number
  pageSize?: number
}

export async function queryAudit(filters: AuditFilters): Promise<AuditEntry[]> {
  const { data } = await apiClient.get<ApiResponse<AuditEntry[]>>('/audit', { params: filters })
  if (!data.success) throw new Error(data.error ?? 'Failed to load audit log.')
  return data.data ?? []
}

export async function getOperations(): Promise<string[]> {
  const { data } = await apiClient.get<ApiResponse<string[]>>('/audit/operations')
  if (!data.success) throw new Error(data.error ?? 'Failed to load operations.')
  return data.data ?? []
}

export interface ExportAuditParams {
  from?: string
  to?: string
  actor?: string
  operation?: string
}

export function buildExportUrl(params: ExportAuditParams): string {
  const query = new URLSearchParams()
  if (params.from) query.set('from', params.from)
  if (params.to) query.set('to', params.to)
  if (params.actor) query.set('actor', params.actor)
  if (params.operation) query.set('operation', params.operation)
  return `${apiClient.defaults.baseURL}/audit/export?${query.toString()}`
}
