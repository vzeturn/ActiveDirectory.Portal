import type { ReactElement } from 'react'
import * as RadixTooltip from '@radix-ui/react-tooltip'
import styles from './Tooltip.module.css'

interface TooltipProps {
  label: string
  children: ReactElement
}

export function Tooltip({ label, children }: TooltipProps) {
  return (
    <RadixTooltip.Provider delayDuration={300}>
      <RadixTooltip.Root>
        <RadixTooltip.Trigger asChild>{children}</RadixTooltip.Trigger>
        <RadixTooltip.Portal>
          <RadixTooltip.Content className={styles.content} sideOffset={6}>
            {label}
            <RadixTooltip.Arrow className={styles.arrow} />
          </RadixTooltip.Content>
        </RadixTooltip.Portal>
      </RadixTooltip.Root>
    </RadixTooltip.Provider>
  )
}
