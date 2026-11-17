import type { ReactNode } from 'react'
import styles from './Badge.module.css'

export type BadgeVariant = 'success' | 'danger' | 'warning' | 'neutral'

interface BadgeProps {
  variant?: BadgeVariant
  children: ReactNode
}

export function Badge({ variant = 'neutral', children }: BadgeProps) {
  return (
    <span className={`${styles.badge} ${styles[variant]}`}>
      <span className={styles.dot} aria-hidden="true" />
      {children}
    </span>
  )
}
