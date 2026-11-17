import { describe, expect, it, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useUserGroups, useUsersSearch } from './useUsersQuery'
import * as userService from '../../services/user.service'
import type { AdUser } from '../../types'

vi.mock('../../services/user.service')

function wrapper(client: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}

const sampleUser: AdUser = {
  username: 'jdoe',
  displayName: 'Jane Doe',
  enabled: true,
  isLockedOut: false
} as AdUser

describe('useUsersSearch', () => {
  beforeEach(() => vi.clearAllMocks())

  it('dedupes two renders with the same key within staleTime', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, retry: false } } })
    vi.mocked(userService.searchUsers).mockResolvedValue([sampleUser])

    const { result, rerender } = renderHook(() => useUsersSearch('jane', 100, 'ou'), {
      wrapper: wrapper(client)
    })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    rerender()
    await waitFor(() => expect(result.current.data).toEqual([sampleUser]))

    expect(userService.searchUsers).toHaveBeenCalledTimes(1)
  })
})

describe('useUserGroups', () => {
  beforeEach(() => vi.clearAllMocks())

  it('does not fetch when disabled', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    vi.mocked(userService.getUserGroups).mockResolvedValue(['Engineers'])

    renderHook(() => useUserGroups(undefined, 'ou', { enabled: false }), {
      wrapper: wrapper(client)
    })

    await new Promise((r) => setTimeout(r, 10))
    expect(userService.getUserGroups).not.toHaveBeenCalled()
  })

  it('fetches groups for the given username when enabled', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    vi.mocked(userService.getUserGroups).mockResolvedValue(['Engineers'])

    const { result } = renderHook(() => useUserGroups('jdoe', 'ou', { enabled: true }), {
      wrapper: wrapper(client)
    })

    await waitFor(() => expect(result.current.data).toEqual(['Engineers']))
    expect(userService.getUserGroups).toHaveBeenCalledWith('jdoe', 'ou')
  })
})
