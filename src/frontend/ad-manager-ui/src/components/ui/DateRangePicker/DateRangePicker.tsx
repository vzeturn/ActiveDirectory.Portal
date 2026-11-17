import { Input } from '../Input/Input'
import styles from './DateRangePicker.module.css'

export interface DateRange {
  from?: string
  to?: string
}

interface DateRangePickerProps {
  value: DateRange
  onChange: (value: DateRange) => void
  disabled?: boolean
}

export function DateRangePicker({ value, onChange, disabled }: DateRangePickerProps) {
  return (
    <div className={styles.range}>
      <Input
        type="date"
        label="From"
        value={value.from ?? ''}
        max={value.to}
        onChange={(e) => onChange({ ...value, from: e.target.value || undefined })}
        disabled={disabled}
      />
      <span className={styles.separator} aria-hidden="true">
        &ndash;
      </span>
      <Input
        type="date"
        label="To"
        value={value.to ?? ''}
        min={value.from}
        onChange={(e) => onChange({ ...value, to: e.target.value || undefined })}
        disabled={disabled}
      />
    </div>
  )
}
