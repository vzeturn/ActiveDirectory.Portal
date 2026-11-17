import { isAxiosError } from 'axios'
import { apiClient } from './api'
import type { ApiResponse, ApiMessageResponse, AdUser, AdScope, BulkOpItemResult } from '../types'
import type { ComboboxOption } from '../components/ui/Combobox/Combobox'

function unwrap(err: unknown, fallback: string): never {
  if (isAxiosError<ApiResponse<unknown>>(err) && err.response?.data?.error) {
    throw new Error(err.response.data.error)
  }
  throw err instanceof Error ? err : new Error(fallback)
}

export interface UserSearchFilters {
  ou?: string
  enabledOnly?: boolean
  lockedOnly?: boolean
  memberOf?: string
}

export async function searchUsers(
  query: string,
  limit = 100,
  scope: AdScope = 'ou',
  filters?: UserSearchFilters
): Promise<AdUser[]> {
  try {
    const { data } = await apiClient.get<ApiResponse<AdUser[]>>('/users/search', {
      params: {
        q: query,
        limit,
        scope,
        ou: filters?.ou || undefined,
        enabledOnly: filters?.enabledOnly || undefined,
        lockedOnly: filters?.lockedOnly || undefined,
        memberOf: filters?.memberOf || undefined
      }
    })
    if (!data.success) throw new Error(data.error ?? 'Search failed.')
    return data.data ?? []
  } catch (err) {
    unwrap(err, 'Search failed.')
  }
}

export async function getUser(username: string, scope: AdScope = 'ou'): Promise<AdUser> {
  try {
    const { data } = await apiClient.get<ApiResponse<AdUser>>(`/users/${encodeURIComponent(username)}`, { params: { scope } })
    if (!data.success || !data.data) throw new Error(data.error ?? 'User not found.')
    return data.data
  } catch (err) {
    unwrap(err, 'User not found.')
  }
}

async function toggleAccount(username: string, action: 'enable' | 'disable' | 'unlock', scope: AdScope): Promise<string> {
  try {
    const { data } = await apiClient.put<ApiMessageResponse>(`/users/${encodeURIComponent(username)}/${action}`, null, { params: { scope } })
    if (!data.success) throw new Error(data.error ?? `${action} failed.`)
    return data.message ?? ''
  } catch (err) {
    unwrap(err, `${action} failed.`)
  }
}

export const enableUser = (username: string, scope: AdScope = 'ou') => toggleAccount(username, 'enable', scope)
export const disableUser = (username: string, scope: AdScope = 'ou') => toggleAccount(username, 'disable', scope)
export const unlockUser = (username: string, scope: AdScope = 'ou') => toggleAccount(username, 'unlock', scope)

export async function resetPassword(
  username: string,
  newPassword: string | undefined,
  requireChange: boolean,
  scope: AdScope = 'ou'
): Promise<string> {
  try {
    const { data } = await apiClient.post<ApiResponse<string>>(
      `/users/${encodeURIComponent(username)}/reset-password`,
      { newPassword, requireChange },
      { params: { scope } }
    )
    if (!data.success || !data.data) throw new Error(data.error ?? 'Reset failed.')
    return data.data
  } catch (err) {
    unwrap(err, 'Reset failed.')
  }
}

export async function getUserGroups(username: string, scope: AdScope = 'ou'): Promise<string[]> {
  try {
    const { data } = await apiClient.get<ApiResponse<string[]>>(`/users/${encodeURIComponent(username)}/groups`, { params: { scope } })
    if (!data.success) throw new Error(data.error ?? 'Failed to load groups.')
    return data.data ?? []
  } catch (err) {
    unwrap(err, 'Failed to load groups.')
  }
}

export async function addUserToGroups(username: string, groupNames: string[], scope: AdScope = 'ou'): Promise<string> {
  try {
    const { data } = await apiClient.post<ApiMessageResponse>(
      `/users/${encodeURIComponent(username)}/groups`,
      { groupNames: groupNames.join(',') },
      { params: { scope } }
    )
    if (!data.success) throw new Error(data.error ?? 'Failed to add to groups.')
    return data.message ?? ''
  } catch (err) {
    unwrap(err, 'Failed to add to groups.')
  }
}

export async function removeUserFromGroup(username: string, groupName: string, scope: AdScope = 'ou'): Promise<string> {
  try {
    const { data } = await apiClient.delete<ApiMessageResponse>(
      `/users/${encodeURIComponent(username)}/groups/${groupName}`,
      { params: { scope } }
    )
    if (!data.success) throw new Error(data.error ?? 'Failed to remove from group.')
    return data.message ?? ''
  } catch (err) {
    unwrap(err, 'Failed to remove from group.')
  }
}

async function bulkOp(path: string, body: unknown, scope: AdScope): Promise<BulkOpItemResult[]> {
  try {
    const { data } = await apiClient.post<ApiResponse<BulkOpItemResult[]>>(path, body, { params: { scope } })
    if (!data.success) throw new Error(data.error ?? 'Bulk operation failed.')
    return data.data ?? []
  } catch (err) {
    unwrap(err, 'Bulk operation failed.')
  }
}

export const bulkEnable = (usernames: string[], scope: AdScope = 'ou') =>
  bulkOp('/users/bulk/enable', { usernames }, scope)

export const bulkDisable = (usernames: string[], scope: AdScope = 'ou') =>
  bulkOp('/users/bulk/disable', { usernames }, scope)

export const bulkUnlock = (usernames: string[], scope: AdScope = 'ou') =>
  bulkOp('/users/bulk/unlock', { usernames }, scope)

export const bulkAddToGroups = (usernames: string[], groupNames: string[], scope: AdScope = 'ou') =>
  bulkOp('/users/bulk/groups', { usernames, groupNames }, scope)

export const bulkResetPassword = (usernames: string[], requireChange: boolean, scope: AdScope = 'ou') =>
  bulkOp('/users/bulk/reset-password', { usernames, requireChange }, scope)

export async function searchUserOptions(query: string, scope: AdScope = 'ou'): Promise<ComboboxOption[]> {
  const users = await searchUsers(query, 20, scope)
  return users.map((u) => ({ value: u.username, label: u.displayName ? `${u.displayName} (${u.username})` : u.username }))
}
