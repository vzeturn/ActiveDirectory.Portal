import { isAxiosError } from 'axios'
import { apiClient } from './api'
import type { ApiResponse, ApiMessageResponse, UserInfo } from '../types'

export async function login(username: string, password: string): Promise<UserInfo> {
  try {
    const { data } = await apiClient.post<ApiResponse<UserInfo>>('/auth/login', { username, password })
    if (!data.success || !data.data) throw new Error(data.error ?? 'Login failed.')
    return data.data
  } catch (err) {
    if (isAxiosError<ApiResponse<UserInfo>>(err) && err.response?.data?.error) {
      throw new Error(err.response.data.error)
    }
    throw err
  }
}

export async function logout(): Promise<void> {
  await apiClient.post<ApiMessageResponse>('/auth/logout')
}

export async function me(): Promise<UserInfo> {
  const { data } = await apiClient.get<ApiResponse<UserInfo>>('/auth/me')
  if (!data.success || !data.data) throw new Error(data.error ?? 'Not authenticated.')
  return data.data
}
