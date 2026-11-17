import * as RadixCheckbox from '@radix-ui/react-checkbox'
import { Check } from 'lucide-react'
import styles from './Checkbox.module.css'

interface CheckboxProps {
  checked: boolean
  onChange: (checked: boolean) => void
  label?: string
  disabled?: boolean
}

export function Checkbox({ checked, onChange, label, disabled }: CheckboxProps) {
  return (
    <label className={styles.wrapper}>
      <RadixCheckbox.Root
        className={styles.input}
        checked={checked}
        disabled={disabled}
        onCheckedChange={(next) => onChange(next === true)}
      >
        <RadixCheckbox.Indicator className={styles.indicator}>
          <Check size={14} strokeWidth={3} aria-hidden="true" />
        </RadixCheckbox.Indicator>
      </RadixCheckbox.Root>
      {label && <span className={styles.label}>{label}</span>}
    </label>
  )
}
