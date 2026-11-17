import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { UsersPage } from './UsersPage'
import * as userService from '../services/user.service'
import * as groupService from '../services/group.service'
import * as savedSearchService from '../services/savedSearch.service'
import { useAuthStore } from '../stores/authStore'
import type { AdUser, SavedSearch } from '../types'

vi.mock('../services/user.service')
vi.mock('../services/group.service')
vi.mock('../services/savedSearch.service')
vi.mock('../stores/authStore')

// jsdom lacks these APIs that Radix UI's Select relies on for pointer interaction
beforeEach(() => {
  if (!Element.prototype.hasPointerCapture) {
    Element.prototype.hasPointerCapture = () => false
  }
  if (!Element.prototype.setPointerCapture) {
    Element.prototype.setPointerCapture = () => {}
  }
  if (!Element.prototype.releasePointerCapture) {
    Element.prototype.releasePointerCapture = () => {}
  }
  if (!Element.prototype.scrollIntoView) {
    Element.prototype.scrollIntoView = () => {}
  }
})

const sampleUser: AdUser = {
  username: 'jdoe',
  displayName: 'Jane Doe',
  enabled: true,
  isLockedOut: false
} as AdUser

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <UsersPage />
    </QueryClientProvider>
  )
}

describe('UsersPage group panel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useAuthStore).mockReturnValue({ user: { isAdmin: true } })
    vi.mocked(userService.searchUsers).mockResolvedValue([sampleUser])
    vi.mocked(savedSearchService.listSavedSearches).mockResolvedValue([])
  })

  it('shows skeleton rows while group membership is loading, then the groups', async () => {
    let resolveGroups: (groups: string[]) => void = () => {}
    vi.mocked(userService.getUserGroups).mockReturnValue(
      new Promise((resolve) => {
        resolveGroups = resolve
      })
    )
    vi.mocked(groupService.searchGroupOptions).mockResolvedValue([])

    renderPage()

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'jane' } })
    fireEvent.click(screen.getByRole('button', { name: /search/i }))

    await waitFor(() => expect(screen.getByText('Jane Doe')).toBeInTheDocument())
    fireEvent.click(screen.getByText('Jane Doe'))

    await waitFor(() => expect(screen.getAllByTestId('group-skeleton-row').length).toBeGreaterThan(0))

    resolveGroups(['Engineers'])

    await waitFor(() => expect(screen.getByText('Engineers')).toBeInTheDocument())
    expect(screen.queryAllByTestId('group-skeleton-row')).toHaveLength(0)
  })

  it('keeps the user panel open under the Manage groups modal so the picker has a user', async () => {
    vi.mocked(userService.getUserGroups).mockResolvedValue(['Engineers'])
    vi.mocked(groupService.searchGroupOptions).mockResolvedValue([])

    renderPage()

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'jane' } })
    fireEvent.click(screen.getByRole('button', { name: /search/i }))
    await waitFor(() => expect(screen.getByText('Jane Doe')).toBeInTheDocument())
    fireEvent.click(screen.getByText('Jane Doe'))
    fireEvent.click(await screen.findByRole('button', { name: /manage groups/i }))

    expect(await screen.findByRole('searchbox', { name: 'Search groups' })).toBeInTheDocument()
    expect(screen.getByRole('dialog', { name: /Manage groups · Jane Doe/ })).toBeInTheDocument()
  })

  it('keeps the selected user pinned to the scope they were found in, even if the scope selector changes afterward', async () => {
    vi.mocked(userService.getUserGroups).mockResolvedValue(['Engineers'])
    vi.mocked(groupService.searchGroupOptions).mockResolvedValue([])

    renderPage()

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'jane' } })
    fireEvent.click(screen.getByRole('button', { name: /search/i }))
    await waitFor(() => expect(screen.getByText('Jane Doe')).toBeInTheDocument())
    fireEvent.click(screen.getByText('Jane Doe'))
    await waitFor(() => expect(screen.getByText('Engineers')).toBeInTheDocument())

    vi.mocked(userService.getUserGroups).mockClear()

    fireEvent.click(screen.getByRole('combobox', { name: 'Scope' }))
    const domainOption = await screen.findByRole('option', { name: 'Domain scope' })
    fireEvent.pointerUp(domainOption)
    fireEvent.click(domainOption)

    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Scope' })).toHaveTextContent('Domain scope'))
    await waitFor(() => expect(userService.getUserGroups).toHaveBeenCalled())

    expect(userService.getUserGroups).not.toHaveBeenCalledWith('jdoe', 'domain')
  })

  it('shows an error, not a false empty state, when group membership fails to load', async () => {
    vi.mocked(userService.getUserGroups).mockRejectedValue(new Error('AD timeout'))
    vi.mocked(groupService.searchGroupOptions).mockResolvedValue([])

    renderPage()

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'jane' } })
    fireEvent.click(screen.getByRole('button', { name: /search/i }))

    await waitFor(() => expect(screen.getByText('Jane Doe')).toBeInTheDocument())
    fireEvent.click(screen.getByText('Jane Doe'))

    await waitFor(() => expect(screen.getByText('AD timeout')).toBeInTheDocument())
    expect(screen.queryByText('No group memberships')).not.toBeInTheDocument()
  })
})

describe('UsersPage advanced search and saved searches', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useAuthStore).mockReturnValue({ user: { isAdmin: true } })
    vi.mocked(userService.searchUsers).mockResolvedValue([sampleUser])
    vi.mocked(groupService.searchGroupOptions).mockResolvedValue([])
  })

  it('toggles the advanced filters panel and includes filter values in the search call', async () => {
    vi.mocked(savedSearchService.listSavedSearches).mockResolvedValue([])

    renderPage()

    fireEvent.click(screen.getByRole('button', { name: /advanced filters/i }))
    fireEvent.change(screen.getByLabelText('Organizational unit contains'), { target: { value: 'OU=Sales' } })
    fireEvent.click(screen.getByLabelText('Enabled only'))

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'jane' } })
    fireEvent.click(screen.getByRole('button', { name: /^search$/i }))

    await waitFor(() =>
      expect(userService.searchUsers).toHaveBeenCalledWith(
        'jane',
        100,
        'ou',
        expect.objectContaining({ ou: 'OU=Sales', enabledOnly: true })
      )
    )
  })

  it('lists saved searches and applies one when selected', async () => {
    const saved: SavedSearch = {
      id: 's1',
      owner: 'admin',
      name: 'Locked sales accounts',
      filterJson: JSON.stringify({ query: 'sales', scope: 'domain', lockedOnly: true }),
      createdAt: new Date().toISOString()
    }
    vi.mocked(savedSearchService.listSavedSearches).mockResolvedValue([saved])

    renderPage()

    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Saved searches' })).toBeInTheDocument())
    fireEvent.click(screen.getByRole('combobox', { name: 'Saved searches' }))
    const option = await screen.findByRole('option', { name: 'Locked sales accounts' })
    fireEvent.pointerUp(option)
    fireEvent.click(option)

    await waitFor(() =>
      expect(userService.searchUsers).toHaveBeenCalledWith(
        'sales',
        100,
        'domain',
        expect.objectContaining({ lockedOnly: true })
      )
    )
  })

  it('shows an error state when saved searches fail to load', async () => {
    vi.mocked(savedSearchService.listSavedSearches).mockRejectedValue(new Error('DB unavailable'))

    renderPage()

    await waitFor(() => expect(screen.getByText('Failed to load saved searches.')).toBeInTheDocument())
  })
})
