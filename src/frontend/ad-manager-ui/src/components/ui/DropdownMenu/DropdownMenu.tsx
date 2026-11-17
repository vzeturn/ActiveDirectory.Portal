import type { ReactNode } from 'react'
import * as RadixDropdownMenu from '@radix-ui/react-dropdown-menu'
import styles from './DropdownMenu.module.css'

interface DropdownMenuItem {
  label: string
  onSelect: () => void
  danger?: boolean
  disabled?: boolean
}

interface DropdownMenuProps {
  trigger: ReactNode
  items: DropdownMenuItem[]
}

export function DropdownMenu({ trigger, items }: DropdownMenuProps) {
  return (
    <RadixDropdownMenu.Root>
      <RadixDropdownMenu.Trigger asChild>{trigger}</RadixDropdownMenu.Trigger>
      <RadixDropdownMenu.Portal>
        <RadixDropdownMenu.Content className={styles.content} align="end" sideOffset={4}>
          {items.map((item) => (
            <RadixDropdownMenu.Item
              key={item.label}
              className={item.danger ? `${styles.item} ${styles.itemDanger}` : styles.item}
              disabled={item.disabled}
              onSelect={item.onSelect}
            >
              {item.label}
            </RadixDropdownMenu.Item>
          ))}
        </RadixDropdownMenu.Content>
      </RadixDropdownMenu.Portal>
    </RadixDropdownMenu.Root>
  )
}
