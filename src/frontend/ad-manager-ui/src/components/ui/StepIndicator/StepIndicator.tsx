import { Check } from 'lucide-react'
import styles from './StepIndicator.module.css'

export interface Step {
  label: string
  description?: string
}

interface StepIndicatorProps {
  steps: Step[]
  currentStep: number
}

export function StepIndicator({ steps, currentStep }: StepIndicatorProps) {
  return (
    <ol className={styles.steps} aria-label="Progress">
      {steps.map((step, index) => {
        const status = index < currentStep ? 'complete' : index === currentStep ? 'current' : 'upcoming'
        return (
          <li key={step.label} className={styles.step} aria-current={status === 'current' ? 'step' : undefined}>
            <span className={[styles.marker, styles[status]].join(' ')} aria-hidden="true">
              {status === 'complete' ? <Check size={14} /> : index + 1}
            </span>
            <span className={styles.labelGroup}>
              <span className={styles.label}>{step.label}</span>
              {step.description && <span className={styles.description}>{step.description}</span>}
            </span>
            {index < steps.length - 1 && <span className={styles.connector} aria-hidden="true" />}
          </li>
        )
      })}
    </ol>
  )
}
