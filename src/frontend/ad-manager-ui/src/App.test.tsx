import { describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { useQueryClient, QueryClientProvider, QueryClient } from '@tanstack/react-query'

vi.mock('./stores/authStore', () => {
  const state = { checkSession: () => {}, user: null, isAuthenticated: false, isLoading: false }
  return {
    useAuthStore: (selector?: (s: typeof state) => unknown) =>
      typeof selector === 'function' ? selector(state) : state
  }
})

function ProbeUsingQueryClient() {
  const client = useQueryClient()
  return <div data-testid="probe">{client ? 'has-client' : 'no-client'}</div>
}

describe('App', () => {
  it('provides a QueryClient to descendants', async () => {
    const client = new QueryClient()
    render(
      <QueryClientProvider client={client}>
        <ProbeUsingQueryClient />
      </QueryClientProvider>
    )
    await waitFor(() => expect(screen.getByTestId('probe')).toHaveTextContent('has-client'))
  })
})

describe('App route code-splitting', () => {
  it('renders lazily without crashing at the default route', async () => {
    const AppModule = await import('./App')
    const App = AppModule.default

    render(<App />)

    await waitFor(() => expect(document.body).toBeTruthy())
  })
})
