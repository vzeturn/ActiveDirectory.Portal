import type { CSSProperties } from 'react'
import styles from './Skeleton.module.css'

interface SkeletonProps {
  variant?: 'text' | 'rect' | 'circle'
  width?: string | number
  height?: string | number
  className?: string
}

export function Skeleton({ variant = 'text', width, height, className }: SkeletonProps) {
  const style: CSSProperties = {}
  if (width !== undefined) style.width = width
  if (height !== undefined) style.height = height

  return (
    <span
      className={[styles.skeleton, styles[variant], className].filter(Boolean).join(' ')}
      style={style}
      aria-hidden="true"
    />
  )
}
