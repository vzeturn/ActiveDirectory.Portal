import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { Modal } from './Modal'
import { Panel } from '../Panel/Panel'

describe('dialog close buttons', () => {
  it('Modal X button calls onClose', () => {
    const onClose = vi.fn()
    render(
      <Modal open title="Test" onClose={onClose}>
        body
      </Modal>
    )
    fireEvent.click(screen.getByRole('button', { name: 'Close' }))
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('Panel X button calls onClose', () => {
    const onClose = vi.fn()
    render(
      <Panel open title="Test" onClose={onClose}>
        body
      </Panel>
    )
    fireEvent.click(screen.getByRole('button', { name: 'Close panel' }))
    expect(onClose).toHaveBeenCalledTimes(1)
  })
})
