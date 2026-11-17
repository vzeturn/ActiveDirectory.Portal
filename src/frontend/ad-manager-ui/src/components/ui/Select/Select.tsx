import { useId } from 'react'
import * as RadixSelect from '@radix-ui/react-select'
import { Check, ChevronDown } from 'lucide-react'
import styles from './Select.module.css'

export interface SelectOption {
  value: string
  label: string
}

interface SelectProps {
  label?: string
  value: string
  onValueChange: (value: string) => void
  options: SelectOption[]
  placeholder?: string
  disabled?: boolean
}

export function Select({ label, value, onValueChange, options, placeholder, disabled }: SelectProps) {
  const selectId = useId()

  return (
    <div className={styles.field}>
      {label && (
        <label className={styles.label} id={selectId}>
          {label}
        </label>
      )}
      <RadixSelect.Root value={value} onValueChange={onValueChange} disabled={disabled}>
        <RadixSelect.Trigger className={styles.select} aria-labelledby={label ? selectId : undefined}>
          <RadixSelect.Value placeholder={placeholder} />
          <RadixSelect.Icon className={styles.chevron}>
            <ChevronDown size={16} aria-hidden="true" />
          </RadixSelect.Icon>
        </RadixSelect.Trigger>
        <RadixSelect.Portal>
          <RadixSelect.Content className={styles.content} position="popper" sideOffset={4}>
            <RadixSelect.Viewport className={styles.viewport}>
              {options.map((opt) => (
                <RadixSelect.Item key={opt.value} value={opt.value} className={styles.item}>
                  <RadixSelect.ItemText>{opt.label}</RadixSelect.ItemText>
                  <RadixSelect.ItemIndicator className={styles.itemIndicator}>
                    <Check size={14} aria-hidden="true" />
                  </RadixSelect.ItemIndicator>
                </RadixSelect.Item>
              ))}
            </RadixSelect.Viewport>
          </RadixSelect.Content>
        </RadixSelect.Portal>
      </RadixSelect.Root>
    </div>
  )
}
