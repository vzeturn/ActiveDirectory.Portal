import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { DashboardPage } from './DashboardPage'
import * as dashboardService from '../services/dashboard.service'
import type { DashboardSummary } from '../types'

vi.mock('../services/dashboard.service')

const sampleSummary: DashboardSummary = {
  lockedOutCount: 2,
  expiringSoonCount: 0,
  recentActivity: []
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <DashboardPage />
    </QueryClientProvider>
  )
}

describe('DashboardPage', () => {
  beforeEach(() => vi.clearAllMocks())

  it('renders summary counts once the query resolves', async () => {
    vi.mocked(dashboardService.getDashboardSummary).mockResolvedValue(sampleSummary)

    renderPage()

    await waitFor(() => expect(screen.getByText('2')).toBeInTheDocument())
  })

  it('renders an error message when the query fails', async () => {
    vi.mocked(dashboardService.getDashboardSummary).mockRejectedValue(new Error('AD unreachable'))

    renderPage()

    await waitFor(() => expect(screen.getByText('AD unreachable')).toBeInTheDocument())
  })
})
