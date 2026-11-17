import { useState } from 'react'
import type { ReactNode } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import { X } from 'lucide-react'
import { Tooltip } from '../Tooltip/Tooltip'
import { ModalBoundaryContext } from './ModalBoundaryContext'
import styles from './Modal.module.css'

interface ModalProps {
  open: boolean
  title: string
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
}

export function Modal({ open, title, onClose, children, footer }: ModalProps) {
  const [boundaryEl, setBoundaryEl] = useState<HTMLElement | null>(null)

  return (
    <Dialog.Root open={open} onOpenChange={(next) => !next && onClose()}>
      <Dialog.Portal>
        <Dialog.Overlay className={styles.overlay} />
        <Dialog.Content ref={setBoundaryEl} className={styles.dialog} aria-describedby={undefined}>
          <ModalBoundaryContext.Provider value={boundaryEl}>
            <div className={styles.header}>
              <Dialog.Title className={styles.title}>{title}</Dialog.Title>
              {/* Tooltip must wrap Dialog.Close, not the reverse: Close's Slot hands its
                  onClick to its direct child, and Tooltip does not forward props. */}
              <Tooltip label="Close">
                <Dialog.Close asChild>
                  <button type="button" className={styles.closeButton} aria-label="Close">
                    <X size={18} aria-hidden="true" />
                  </button>
                </Dialog.Close>
              </Tooltip>
            </div>
            <div className={styles.body}>{children}</div>
            {footer && <div className={styles.footer}>{footer}</div>}
          </ModalBoundaryContext.Provider>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
