import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MembershipPicker } from './MembershipPicker'

const OPTIONS = [
  { value: 'Sales', label: 'Sales' },
  { value: 'IT-Admins', label: 'IT-Admins' }
]

function renderPicker(overrides: Partial<Parameters<typeof MembershipPicker>[0]> = {}) {
  const props = {
    members: ['it-admins'],
    onSearch: vi.fn().mockResolvedValue(OPTIONS),
    onAdd: vi.fn().mockResolvedValue(undefined),
    onRemove: vi.fn().mockResolvedValue(undefined),
    noun: 'group',
    ...overrides
  }
  render(<MembershipPicker {...props} />)
  fireEvent.change(screen.getByRole('searchbox', { name: 'Search groups' }), { target: { value: 'sa' } })
  return props
}

describe('MembershipPicker', () => {
  it('prompts for a longer query before searching', () => {
    render(<MembershipPicker members={[]} onSearch={vi.fn()} onAdd={vi.fn()} onRemove={vi.fn()} noun="group" />)
    expect(screen.getByText('Type at least 2 characters to search groups.')).toBeInTheDocument()
  })

  it('offers Add for non-members and Remove for existing members (case-insensitive)', async () => {
    renderPicker()
    expect(await screen.findByRole('button', { name: 'Add Sales' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Remove IT-Admins' })).toBeInTheDocument()
  })

  it('calls onAdd and replaces the button with an Added confirmation', async () => {
    const props = renderPicker()
    fireEvent.click(await screen.findByRole('button', { name: 'Add Sales' }))
    expect(props.onAdd).toHaveBeenCalledWith(OPTIONS[0])
    expect(await screen.findByLabelText('Sales added')).toHaveTextContent('Added')
    expect(screen.queryByRole('button', { name: 'Add Sales' })).not.toBeInTheDocument()
  })

  it('calls onRemove for a member and confirms removal', async () => {
    const props = renderPicker()
    fireEvent.click(await screen.findByRole('button', { name: 'Remove IT-Admins' }))
    expect(props.onRemove).toHaveBeenCalledWith(OPTIONS[1])
    expect(await screen.findByLabelText('IT-Admins removed')).toBeInTheDocument()
  })

  it('shows the API error on the row and lets the user retry', async () => {
    const onAdd = vi.fn().mockRejectedValueOnce(new Error('Insufficient rights.')).mockResolvedValueOnce(undefined)
    renderPicker({ onAdd })
    fireEvent.click(await screen.findByRole('button', { name: 'Add Sales' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Insufficient rights.')

    const retry = screen.getByRole('button', { name: 'Add Sales' })
    expect(retry).toHaveTextContent('Retry')
    fireEvent.click(retry)
    expect(await screen.findByLabelText('Sales added')).toBeInTheDocument()
    expect(onAdd).toHaveBeenCalledTimes(2)
  })

  it('shows a retryable error when the search itself fails', async () => {
    const onSearch = vi.fn().mockRejectedValueOnce(new Error('Directory unavailable')).mockResolvedValueOnce(OPTIONS)
    renderPicker({ onSearch })
    expect(await screen.findByRole('alert')).toHaveTextContent('Directory unavailable')
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(await screen.findByRole('button', { name: 'Add Sales' })).toBeInTheDocument()
  })

  it('disables actions for read-only users', async () => {
    const props = renderPicker({ disabled: true, disabledReason: 'View only' })
    const add = await screen.findByRole('button', { name: 'Add Sales' })
    expect(add).toBeDisabled()
    expect(add).toHaveAttribute('title', 'View only')
    fireEvent.click(add)
    await waitFor(() => expect(props.onAdd).not.toHaveBeenCalled())
  })
})
