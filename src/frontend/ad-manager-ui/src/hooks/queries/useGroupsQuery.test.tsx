import { describe, expect, it, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useGroupDetail, useGroupsSearch } from './useGroupsQuery'
import { useRemoveGroupMember } from './useGroupMutations'
import * as groupService from '../../services/group.service'
import type { AdGroup } from '../../types'

vi.mock('../../services/group.service')

function makeWrapper(client: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}

const sampleGroup: AdGroup = { name: 'Engineers', memberCount: 1, members: [], parentGroups: [] }

describe('useGroupsSearch', () => {
  beforeEach(() => vi.clearAllMocks())

  it('fetches groups for a query', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    vi.mocked(groupService.searchGroups).mockResolvedValue([sampleGroup])

    const { result } = renderHook(() => useGroupsSearch('eng', 100, 'ou'), { wrapper: makeWrapper(client) })

    await waitFor(() => expect(result.current.data).toEqual([sampleGroup]))
  })
})

describe('useGroupDetail', () => {
  beforeEach(() => vi.clearAllMocks())

  it('fetches a single group by name', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    vi.mocked(groupService.getGroup).mockResolvedValue(sampleGroup)

    const { result } = renderHook(() => useGroupDetail('Engineers', 'ou'), { wrapper: makeWrapper(client) })

    await waitFor(() => expect(result.current.data).toEqual(sampleGroup))
  })
})

describe('useRemoveGroupMember', () => {
  beforeEach(() => vi.clearAllMocks())

  it('invalidates the group detail key and the groups search key on success', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    vi.mocked(groupService.removeGroupMember).mockResolvedValue('ok')

    const { result } = renderHook(() => useRemoveGroupMember(), { wrapper: makeWrapper(client) })

    result.current.mutate({ groupName: 'Engineers', username: 'jdoe', scope: 'ou' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['groups', 'Engineers', 'ou'] })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['groups', 'search'] })
  })
})
