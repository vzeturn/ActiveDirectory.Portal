import { useMutation, useQueryClient } from '@tanstack/react-query'
import * as userService from '../../services/user.service'
import type { AdScope } from '../../types'

export function useToggleUserAccount(action: 'enable' | 'disable' | 'unlock') {
  const queryClient = useQueryClient()
  const fn =
    action === 'enable' ? userService.enableUser : action === 'disable' ? userService.disableUser : userService.unlockUser

  return useMutation<string, Error, { username: string; scope: AdScope }>({
    mutationFn: ({ username, scope }) => fn(username, scope),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users', 'search'] })
    }
  })
}

export function useResetPassword() {
  return useMutation<string, Error, { username: string; newPassword?: string; requireChange: boolean; scope: AdScope }>({
    mutationFn: ({ username, newPassword, requireChange, scope }) =>
      userService.resetPassword(username, newPassword, requireChange, scope)
  })
}

export function useAddUserToGroups() {
  const queryClient = useQueryClient()
  return useMutation<string, Error, { username: string; groupNames: string[]; scope: AdScope }>({
    mutationFn: ({ username, groupNames, scope }) => userService.addUserToGroups(username, groupNames, scope),
    onSuccess: (_data, { username, scope }) => {
      queryClient.invalidateQueries({ queryKey: ['users', username, 'groups', scope] })
      queryClient.invalidateQueries({ queryKey: ['groups', 'search'] })
    }
  })
}

export function useRemoveUserFromGroup() {
  const queryClient = useQueryClient()
  return useMutation<string, Error, { username: string; groupName: string; scope: AdScope }>({
    mutationFn: ({ username, groupName, scope }) => userService.removeUserFromGroup(username, groupName, scope),
    onSuccess: (_data, { username, scope }) => {
      queryClient.invalidateQueries({ queryKey: ['users', username, 'groups', scope] })
      queryClient.invalidateQueries({ queryKey: ['groups', 'search'] })
    }
  })
}
