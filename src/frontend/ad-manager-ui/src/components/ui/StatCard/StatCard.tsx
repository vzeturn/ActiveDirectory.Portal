import type { ReactNode } from 'react'
import styles from './StatCard.module.css'

export type StatCardTone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger'

interface StatCardProps {
  label: string
  value: ReactNode
  icon?: ReactNode
  tone?: StatCardTone
  description?: string
}

export function StatCard({ label, value, icon, tone = 'neutral', description }: StatCardProps) {
  return (
    <div className={styles.card}>
      {icon && (
        <span className={[styles.iconSlot, styles[tone]].join(' ')} aria-hidden="true">
          {icon}
        </span>
      )}
      <div className={styles.body}>
        <span className={styles.label}>{label}</span>
        <span className={styles.value}>{value}</span>
        {description && <span className={styles.description}>{description}</span>}
      </div>
    </div>
  )
}
