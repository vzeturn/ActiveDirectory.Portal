import { useQuery } from '@tanstack/react-query'
import * as userService from '../../services/user.service'
import type { UserSearchFilters } from '../../services/user.service'
import type { AdScope, AdUser } from '../../types'

export function useUsersSearch(
  query: string,
  limit: number,
  scope: AdScope,
  options?: { enabled?: boolean; filters?: UserSearchFilters }
) {
  return useQuery<AdUser[], Error>({
    queryKey: ['users', 'search', { query, limit, scope, filters: options?.filters }],
    queryFn: () => userService.searchUsers(query, limit, scope, options?.filters),
    enabled: options?.enabled
  })
}

export function useUserGroups(username: string | undefined, scope: AdScope, options?: { enabled?: boolean }) {
  return useQuery<string[], Error>({
    queryKey: ['users', username, 'groups', scope],
    queryFn: () => userService.getUserGroups(username as string, scope),
    enabled: (options?.enabled ?? true) && Boolean(username)
  })
}
