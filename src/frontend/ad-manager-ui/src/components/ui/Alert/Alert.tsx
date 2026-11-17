import type { ReactNode } from 'react'
import { AlertCircle, CheckCircle2 } from 'lucide-react'
import styles from './Alert.module.css'

export type AlertVariant = 'error' | 'success'

interface AlertProps {
  variant?: AlertVariant
  children: ReactNode
}

export function Alert({ variant = 'error', children }: AlertProps) {
  const Icon = variant === 'error' ? AlertCircle : CheckCircle2
  return (
    <div className={`${styles.alert} ${styles[variant]}`} role="alert">
      <Icon size={16} className={styles.icon} aria-hidden="true" />
      <span>{children}</span>
    </div>
  )
}
