import { useQuery } from '@tanstack/react-query'
import * as auditService from '../../services/audit.service'
import type { AuditFilters } from '../../services/audit.service'
import type { AuditEntry } from '../../types'

export function useAuditLogs(filters: AuditFilters, options?: { enabled?: boolean }) {
  return useQuery<AuditEntry[], Error>({
    queryKey: ['audit', filters],
    queryFn: () => auditService.queryAudit(filters),
    enabled: options?.enabled
  })
}

export function useAuditOperations() {
  return useQuery<string[], Error>({
    queryKey: ['audit', 'operations'],
    queryFn: () => auditService.getOperations()
  })
}
