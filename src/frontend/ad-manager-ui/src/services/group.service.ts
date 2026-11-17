import { isAxiosError } from 'axios'
import { apiClient } from './api'
import type { ApiResponse, ApiMessageResponse, AdGroup, AdScope, BulkOpItemResult } from '../types'
import type { ComboboxOption } from '../components/ui/Combobox/Combobox'

function unwrap(err: unknown, fallback: string): never {
  if (isAxiosError<ApiResponse<unknown>>(err) && err.response?.data?.error) {
    throw new Error(err.response.data.error)
  }
  throw err instanceof Error ? err : new Error(fallback)
}

export async function searchGroups(query: string, limit = 100, scope: AdScope = 'ou'): Promise<AdGroup[]> {
  try {
    const { data } = await apiClient.get<ApiResponse<AdGroup[]>>('/groups/search', { params: { q: query, limit, scope } })
    if (!data.success) throw new Error(data.error ?? 'Search failed.')
    return data.data ?? []
  } catch (err) {
    unwrap(err, 'Search failed.')
  }
}

export async function getGroup(groupName: string, scope: AdScope = 'ou'): Promise<AdGroup> {
  try {
    const { data } = await apiClient.get<ApiResponse<AdGroup>>(`/groups/${encodeURIComponent(groupName)}`, { params: { scope } })
    if (!data.success || !data.data) throw new Error(data.error ?? 'Group not found.')
    return data.data
  } catch (err) {
    unwrap(err, 'Group not found.')
  }
}

export async function createGroup(name: string, description: string | undefined, scope: AdScope = 'ou'): Promise<string> {
  try {
    const { data } = await apiClient.post<ApiMessageResponse>('/groups', { name, description }, { params: { scope } })
    if (!data.success) throw new Error(data.error ?? 'Failed to create group.')
    return data.message ?? ''
  } catch (err) {
    unwrap(err, 'Failed to create group.')
  }
}

export async function deleteGroup(name: string, scope: AdScope = 'ou'): Promise<string> {
  try {
    const { data } = await apiClient.delete<ApiMessageResponse>(`/groups/${encodeURIComponent(name)}`, { params: { scope } })
    if (!data.success) throw new Error(data.error ?? 'Failed to delete group.')
    return data.message ?? ''
  } catch (err) {
    unwrap(err, 'Failed to delete group.')
  }
}

export async function updateGroupDescription(name: string, description: string | undefined, scope: AdScope = 'ou'): Promise<string> {
  try {
    const { data } = await apiClient.put<ApiMessageResponse>(`/groups/${encodeURIComponent(name)}/description`, { description }, { params: { scope } })
    if (!data.success) throw new Error(data.error ?? 'Failed to update group.')
    return data.message ?? ''
  } catch (err) {
    unwrap(err, 'Failed to update group.')
  }
}

export async function addGroupMembers(groupName: string, usernames: string[], scope: AdScope = 'ou'): Promise<BulkOpItemResult[]> {
  try {
    const { data } = await apiClient.post<ApiResponse<BulkOpItemResult[]>>(
      `/groups/${encodeURIComponent(groupName)}/members`,
      { memberNames: usernames.join(',') },
      { params: { scope } }
    )
    if (!data.success) throw new Error(data.error ?? 'Failed to add members.')
    const results = data.data ?? []
    const failed = results.filter((r) => !r.success)
    if (failed.length > 0) {
      throw new Error(failed.map((r) => `${r.username}: ${r.error ?? 'failed'}`).join('; '))
    }
    return results
  } catch (err) {
    unwrap(err, 'Failed to add members.')
  }
}

export async function removeGroupMember(groupName: string, username: string, scope: AdScope = 'ou'): Promise<string> {
  try {
    const { data } = await apiClient.delete<ApiMessageResponse>(
      `/groups/${encodeURIComponent(groupName)}/members/${encodeURIComponent(username)}`,
      { params: { scope } }
    )
    if (!data.success) throw new Error(data.error ?? 'Failed to remove member.')
    return data.message ?? ''
  } catch (err) {
    unwrap(err, 'Failed to remove member.')
  }
}

export async function searchGroupOptions(query: string, scope: AdScope = 'ou'): Promise<ComboboxOption[]> {
  const groups = await searchGroups(query, 20, scope)
  return groups.map((g) => ({ value: g.name, label: g.name }))
}
