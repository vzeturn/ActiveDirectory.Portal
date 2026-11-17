import { useQuery } from '@tanstack/react-query'
import * as groupService from '../../services/group.service'
import type { AdGroup, AdScope } from '../../types'

export function useGroupsSearch(query: string, limit: number, scope: AdScope, options?: { enabled?: boolean }) {
  return useQuery<AdGroup[], Error>({
    queryKey: ['groups', 'search', { query, limit, scope }],
    queryFn: () => groupService.searchGroups(query, limit, scope),
    enabled: options?.enabled
  })
}

export function useGroupDetail(groupName: string | undefined, scope: AdScope, options?: { enabled?: boolean }) {
  return useQuery<AdGroup, Error>({
    queryKey: ['groups', groupName, scope],
    queryFn: () => groupService.getGroup(groupName as string, scope),
    enabled: (options?.enabled ?? true) && Boolean(groupName)
  })
}
