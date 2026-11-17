import { useId } from 'react'
import type { ReactNode } from 'react'
import styles from './Tabs.module.css'

export interface TabItem {
  value: string
  label: string
  icon?: ReactNode
}

interface TabsProps {
  items: TabItem[]
  value: string
  onChange: (value: string) => void
}

export function Tabs({ items, value, onChange }: TabsProps) {
  const baseId = useId()

  return (
    <div className={styles.tabs} role="tablist">
      {items.map((item) => {
        const selected = item.value === value
        return (
          <button
            key={item.value}
            type="button"
            role="tab"
            id={`${baseId}-tab-${item.value}`}
            aria-selected={selected}
            aria-controls={`${baseId}-panel-${item.value}`}
            className={[styles.tab, selected ? styles.tabActive : ''].filter(Boolean).join(' ')}
            onClick={() => onChange(item.value)}
          >
            {item.icon && (
              <span className={styles.iconSlot} aria-hidden="true">
                {item.icon}
              </span>
            )}
            {item.label}
          </button>
        )
      })}
    </div>
  )
}
