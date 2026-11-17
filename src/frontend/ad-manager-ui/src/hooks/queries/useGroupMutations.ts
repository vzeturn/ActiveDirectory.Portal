import { useMutation, useQueryClient } from '@tanstack/react-query'
import * as groupService from '../../services/group.service'
import type { AdScope } from '../../types'

export function useCreateGroup() {
  const queryClient = useQueryClient()
  return useMutation<string, Error, { name: string; description: string | undefined; scope: AdScope }>({
    mutationFn: ({ name, description, scope }) => groupService.createGroup(name, description, scope),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['groups', 'search'] })
  })
}

export function useDeleteGroup() {
  const queryClient = useQueryClient()
  return useMutation<string, Error, { name: string; scope: AdScope }>({
    mutationFn: ({ name, scope }) => groupService.deleteGroup(name, scope),
    onSuccess: (_data, { name, scope }) => {
      queryClient.invalidateQueries({ queryKey: ['groups', name, scope] })
      queryClient.invalidateQueries({ queryKey: ['groups', 'search'] })
    }
  })
}

export function useUpdateGroupDescription() {
  const queryClient = useQueryClient()
  return useMutation<string, Error, { name: string; description: string | undefined; scope: AdScope }>({
    mutationFn: ({ name, description, scope }) => groupService.updateGroupDescription(name, description, scope),
    onSuccess: (_data, { name, scope }) => {
      queryClient.invalidateQueries({ queryKey: ['groups', name, scope] })
      queryClient.invalidateQueries({ queryKey: ['groups', 'search'] })
    }
  })
}

export function useAddGroupMembers() {
  const queryClient = useQueryClient()
  return useMutation<unknown, Error, { groupName: string; usernames: string[]; scope: AdScope }>({
    mutationFn: ({ groupName, usernames, scope }) => groupService.addGroupMembers(groupName, usernames, scope),
    onSuccess: (_data, { groupName, scope }) => {
      queryClient.invalidateQueries({ queryKey: ['groups', groupName, scope] })
      queryClient.invalidateQueries({ queryKey: ['groups', 'search'] })
    }
  })
}

export function useRemoveGroupMember() {
  const queryClient = useQueryClient()
  return useMutation<string, Error, { groupName: string; username: string; scope: AdScope }>({
    mutationFn: ({ groupName, username, scope }) => groupService.removeGroupMember(groupName, username, scope),
    onSuccess: (_data, { groupName, scope }) => {
      queryClient.invalidateQueries({ queryKey: ['groups', groupName, scope] })
      queryClient.invalidateQueries({ queryKey: ['groups', 'search'] })
    }
  })
}
