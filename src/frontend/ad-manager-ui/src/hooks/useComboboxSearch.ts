import { useEffect, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { useDebounce } from './useDebounce'

interface UseComboboxSearchOptions<T> {
  isOpen: boolean
  query: string
  onSearch: (query: string) => Promise<T[]>
  filterResults?: (results: T[]) => T[]
}

interface UseComboboxSearchResult<T> {
  options: T[]
  isSearching: boolean
  searchError: string | null
  retrySearch: () => void
  highlightedIndex: number
  setHighlightedIndex: (updater: number | ((prev: number) => number)) => void
  handleKeyDown: (e: KeyboardEvent, onActivate: (opt: T) => void, onEscape: () => void, activeList?: T[]) => void
}

export function useComboboxSearch<T>({
  isOpen,
  query,
  onSearch,
  filterResults
}: UseComboboxSearchOptions<T>): UseComboboxSearchResult<T> {
  const [options, setOptions] = useState<T[]>([])
  const [isSearching, setIsSearching] = useState(false)
  const [searchError, setSearchError] = useState<string | null>(null)
  const [retryNonce, setRetryNonce] = useState(0)
  const [highlightedIndex, setHighlightedIndex] = useState(-1)
  const debouncedQuery = useDebounce(query, 250)

  useEffect(() => {
    if (!isOpen || debouncedQuery.length < 2) {
      setOptions([])
      setIsSearching(false)
      setSearchError(null)
      return
    }
    let cancelled = false
    setIsSearching(true)
    setSearchError(null)
    onSearch(debouncedQuery)
      .then((results) => {
        if (cancelled) return
        setOptions(filterResults ? filterResults(results) : results)
      })
      .catch((err: unknown) => {
        if (cancelled) return
        setOptions([])
        setSearchError(err instanceof Error ? err.message : 'Search failed.')
      })
      .finally(() => {
        if (!cancelled) setIsSearching(false)
      })
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedQuery, isOpen, onSearch, retryNonce])

  useEffect(() => {
    setHighlightedIndex(-1)
  }, [options, isOpen])

  const handleKeyDown = (
    e: KeyboardEvent,
    onActivate: (opt: T) => void,
    onEscape: () => void,
    activeList: T[] = options
  ) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      if (activeList.length > 0) {
        setHighlightedIndex((i) => Math.min(i + 1, activeList.length - 1))
      }
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      if (activeList.length > 0) {
        setHighlightedIndex((i) => Math.max(i - 1, 0))
      }
    } else if (e.key === 'Enter') {
      if (highlightedIndex >= 0 && activeList[highlightedIndex]) {
        e.preventDefault()
        onActivate(activeList[highlightedIndex])
      }
    } else if (e.key === 'Escape') {
      onEscape()
    }
  }

  const retrySearch = () => setRetryNonce((n) => n + 1)

  return { options, isSearching, searchError, retrySearch, highlightedIndex, setHighlightedIndex, handleKeyDown }
}
