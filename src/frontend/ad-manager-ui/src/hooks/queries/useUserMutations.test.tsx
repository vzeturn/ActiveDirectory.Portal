import { describe, expect, it, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useAddUserToGroups, useToggleUserAccount } from './useUserMutations'
import * as userService from '../../services/user.service'

vi.mock('../../services/user.service')

function makeWrapper(client: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}

describe('useToggleUserAccount', () => {
  beforeEach(() => vi.clearAllMocks())

  it('invalidates users search cache on success', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    vi.mocked(userService.enableUser).mockResolvedValue('ok')

    const { result } = renderHook(() => useToggleUserAccount('enable'), {
      wrapper: makeWrapper(client)
    })

    result.current.mutate({ username: 'jdoe', scope: 'ou' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['users', 'search'] })
  })

  it('does not invalidate cache on failure', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    vi.mocked(userService.enableUser).mockRejectedValue(new Error('boom'))

    const { result } = renderHook(() => useToggleUserAccount('enable'), {
      wrapper: makeWrapper(client)
    })

    result.current.mutate({ username: 'jdoe', scope: 'ou' })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(invalidateSpy).not.toHaveBeenCalled()
  })
})

describe('useAddUserToGroups', () => {
  beforeEach(() => vi.clearAllMocks())

  it('invalidates both the user groups key and the groups search key on success', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    vi.mocked(userService.addUserToGroups).mockResolvedValue('ok')

    const { result } = renderHook(() => useAddUserToGroups(), {
      wrapper: makeWrapper(client)
    })

    result.current.mutate({ username: 'jdoe', groupNames: ['Engineers'], scope: 'ou' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['users', 'jdoe', 'groups', 'ou'] })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['groups', 'search'] })
  })
})
