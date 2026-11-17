import { isAxiosError } from 'axios'
import { apiClient } from './api'
import type { ApiResponse, ApiMessageResponse, SavedSearch } from '../types'

function unwrap(err: unknown, fallback: string): never {
  if (isAxiosError<ApiResponse<unknown>>(err) && err.response?.data?.error) {
    throw new Error(err.response.data.error)
  }
  throw err instanceof Error ? err : new Error(fallback)
}

export async function listSavedSearches(): Promise<SavedSearch[]> {
  try {
    const { data } = await apiClient.get<ApiResponse<SavedSearch[]>>('/saved-searches')
    if (!data.success) throw new Error(data.error ?? 'Failed to load saved searches.')
    return data.data ?? []
  } catch (err) {
    unwrap(err, 'Failed to load saved searches.')
  }
}

export async function createSavedSearch(name: string, filterJson: string): Promise<SavedSearch> {
  try {
    const { data } = await apiClient.post<ApiResponse<SavedSearch>>('/saved-searches', { name, filterJson })
    if (!data.success || !data.data) throw new Error(data.error ?? 'Failed to save search.')
    return data.data
  } catch (err) {
    unwrap(err, 'Failed to save search.')
  }
}

export async function deleteSavedSearch(id: string): Promise<string> {
  try {
    const { data } = await apiClient.delete<ApiMessageResponse>(`/saved-searches/${encodeURIComponent(id)}`)
    if (!data.success) throw new Error(data.error ?? 'Failed to delete saved search.')
    return data.message ?? ''
  } catch (err) {
    unwrap(err, 'Failed to delete saved search.')
  }
}
