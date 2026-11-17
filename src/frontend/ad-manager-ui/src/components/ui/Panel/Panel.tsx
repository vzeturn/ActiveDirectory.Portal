import type { ReactNode } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import { X } from 'lucide-react'
import { Tooltip } from '../Tooltip/Tooltip'
import styles from './Panel.module.css'

interface PanelProps {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
}

/** Radix popovers (Select, Combobox, MultiSelect) and modals opened from the
 * panel render in separate portals outside this Dialog's DOM tree. Without this
 * guard, opening one registers as an "outside" interaction (a focus move counts
 * too) and closes the panel underneath it, dropping the context it depends on. */
function isInteractionInsideLayeredPortal(event: Event): boolean {
  const target = event.target as Node | null
  if (!target) return false
  return !!(target as Element).closest?.(
    '[data-radix-popper-content-wrapper], [role="listbox"], [role="combobox"], [role="dialog"], [role="alertdialog"]'
  )
}

export function Panel({ open, onClose, title, children }: PanelProps) {
  return (
    <Dialog.Root open={open} onOpenChange={(next) => !next && onClose()} modal={false}>
      <Dialog.Portal>
        <div className={styles.backdrop} onClick={onClose} aria-hidden="true" />
        <Dialog.Content
          className={styles.panel}
          aria-describedby={undefined}
          onInteractOutside={(e) => {
            if (isInteractionInsideLayeredPortal(e)) e.preventDefault()
          }}
        >
          <div className={styles.header}>
            <Dialog.Title className={styles.title}>{title}</Dialog.Title>
            <Tooltip label="Close panel">
              <Dialog.Close asChild>
                <button type="button" className={styles.closeButton} aria-label="Close panel">
                  <X size={18} aria-hidden="true" />
                </button>
              </Dialog.Close>
            </Tooltip>
          </div>
          <div className={styles.body}>{children}</div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
