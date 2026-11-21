import { useHealthCheck } from '../hooks/queries/useHealthQuery'
import { Skeleton } from '../components/ui/Skeleton/Skeleton'
import { Badge } from '../components/ui/Badge/Badge'
import { Alert } from '../components/ui/Alert/Alert'
import styles from './Page.module.css'

export function HealthPage() {
  const { data: health, isLoading: loading, isError, error } = useHealthCheck()

  if (loading) {
    return (
      <>
        <div className={styles.header}>
          <h1 className={styles.title}>System Health</h1>
        </div>
        <ul className={styles.healthList}>
          {Array.from({ length: 4 }).map((_, i) => (
            <li key={i} className={styles.healthItem}>
              <Skeleton variant="rect" width="15%" height="1.5em" />
              <Skeleton variant="text" width="60%" />
            </li>
          ))}
        </ul>
      </>
    )
  }

  const checks: [string, boolean][] = health
    ? [
        ['Active Directory reachable', health.adReachable],
        ['Secrets loaded', health.secretsLoaded],
        ['Audit database writable', health.auditDbWritable],
        ['RT alert hook configured', health.alertHookConfigured]
      ]
    : []

  return (
    <>
      <div className={styles.header}>
        <h1 className={styles.title}>System Health</h1>
      </div>

      {isError && <Alert variant="error">{error?.message ?? 'Failed to load health.'}</Alert>}

      {health && (
        <ul className={styles.healthList}>
          {checks.map(([label, ok]) => (
            <li key={label} className={styles.healthItem}>
              <Badge variant={ok ? 'success' : 'danger'}>{ok ? 'OK' : 'Not OK'}</Badge>
              {label}
            </li>
          ))}
        </ul>
      )}
    </>
  )
}
