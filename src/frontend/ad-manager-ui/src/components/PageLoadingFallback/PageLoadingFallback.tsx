import styles from './PageLoadingFallback.module.css'

export function PageLoadingFallback() {
  return (
    <div className={styles.wrapper} role="status" aria-label="Loading page">
      <div className={styles.spinner} />
    </div>
  )
}
