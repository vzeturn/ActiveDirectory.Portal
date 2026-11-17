import type { ReactNode } from 'react'
import { Inbox } from 'lucide-react'
import styles from './EmptyState.module.css'

interface EmptyStateProps {
  icon?: ReactNode
  title: string
  description?: string
}

export function EmptyState({ icon, title, description }: EmptyStateProps) {
  return (
    <div className={styles.empty}>
      <span className={styles.icon}>{icon ?? <Inbox size={40} strokeWidth={1.5} aria-hidden="true" />}</span>
      <span className={styles.title}>{title}</span>
      {description && <span className={styles.description}>{description}</span>}
    </div>
  )
}
