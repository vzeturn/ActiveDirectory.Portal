import * as RadixToast from '@radix-ui/react-toast'
import { AlertTriangle, CheckCircle2, X } from 'lucide-react'
import { Tooltip } from '../Tooltip/Tooltip'
import type { ToastMessage } from './useToast'
import styles from './Toast.module.css'

interface ToastContainerProps {
  toasts: ToastMessage[]
  onDismiss: (id: string) => void
}

export function ToastContainer({ toasts, onDismiss }: ToastContainerProps) {
  return (
    <RadixToast.Provider swipeDirection="right">
      {toasts.map((t) => (
        <RadixToast.Root
          key={t.id}
          className={t.variant === 'success' ? styles.success : styles.error}
          onOpenChange={(open) => !open && onDismiss(t.id)}
        >
          {t.variant === 'success' ? (
            <CheckCircle2 size={18} className={styles.icon} aria-hidden="true" />
          ) : (
            <AlertTriangle size={18} className={styles.icon} aria-hidden="true" />
          )}
          <RadixToast.Description className={styles.text}>{t.text}</RadixToast.Description>
          <Tooltip label="Dismiss">
            <RadixToast.Close className={styles.dismiss} aria-label="Dismiss">
              <X size={16} aria-hidden="true" />
            </RadixToast.Close>
          </Tooltip>
        </RadixToast.Root>
      ))}
      <RadixToast.Viewport className={styles.container} />
    </RadixToast.Provider>
  )
}
