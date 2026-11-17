import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as savedSearchService from '../../services/savedSearch.service'
import type { SavedSearch } from '../../types'

export function useSavedSearches() {
  return useQuery<SavedSearch[], Error>({
    queryKey: ['saved-searches'],
    queryFn: savedSearchService.listSavedSearches
  })
}

export function useCreateSavedSearch() {
  const queryClient = useQueryClient()
  return useMutation<SavedSearch, Error, { name: string; filterJson: string }>({
    mutationFn: ({ name, filterJson }) => savedSearchService.createSavedSearch(name, filterJson),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['saved-searches'] })
    }
  })
}

export function useDeleteSavedSearch() {
  const queryClient = useQueryClient()
  return useMutation<string, Error, string>({
    mutationFn: (id) => savedSearchService.deleteSavedSearch(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['saved-searches'] })
    }
  })
}
