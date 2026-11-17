import { useDashboardSummary } from '../hooks/queries/useDashboardQuery'
import { Skeleton } from '../components/ui/Skeleton/Skeleton'
import { Badge } from '../components/ui/Badge/Badge'
import { Alert } from '../components/ui/Alert/Alert'
import { EmptyState } from '../components/ui/EmptyState/EmptyState'
import styles from './Page.module.css'

export function DashboardPage() {
  const { data: summary, isLoading: loading, isError, error } = useDashboardSummary('ou')

  if (loading) {
    return (
      <>
        <div className={styles.header}>
          <h1 className={styles.title}>Dashboard</h1>
        </div>
        <div className={styles.cardRow}>
          <div className={styles.card}>
            <Skeleton variant="text" width="60%" />
            <Skeleton variant="rect" width="40%" height="2em" />
          </div>
          <div className={styles.card}>
            <Skeleton variant="text" width="60%" />
            <Skeleton variant="rect" width="40%" height="2em" />
          </div>
        </div>
        <div className={styles.section}>
          <h2 className={styles.sectionHeading}>Recent activity</h2>
          <ul className={styles.activityList}>
            {Array.from({ length: 3 }).map((_, i) => (
              <li key={i} className={styles.activityItem}>
                <Skeleton variant="text" width="100%" />
              </li>
            ))}
          </ul>
        </div>
      </>
    )
  }

  return (
    <>
      <div className={styles.header}>
        <h1 className={styles.title}>Dashboard</h1>
      </div>

      {isError && <Alert variant="error">{error?.message ?? 'Failed to load dashboard.'}</Alert>}

      {summary && (
        <>
          <div className={styles.cardRow}>
            <div className={styles.card}>
              <span className={styles.cardLabel}>Locked-out accounts</span>
              <span className={styles.metric}>{summary.lockedOutCount}</span>
            </div>
            <div className={styles.card}>
              <span className={styles.cardLabel}>Passwords expiring soon</span>
              <span className={styles.metric}>{summary.expiringSoonCount}</span>
            </div>
          </div>

          <div className={styles.section}>
            <h2 className={styles.sectionHeading}>Recent activity</h2>
            {summary.recentActivity.length === 0 ? (
              <EmptyState title="No recent activity" description="Activity will appear here as it occurs." />
            ) : (
              <ul className={styles.activityList}>
                {summary.recentActivity.map((entry, i) => (
                  <li key={`${entry.timestamp}-${entry.actor}-${entry.operation}-${i}`} className={styles.activityItem}>
                    <span className={styles.activityTime}>{new Date(entry.timestamp).toLocaleString()}</span>
                    <span>{entry.actor}</span>
                    <span>{entry.operation}</span>
                    <span>{entry.target}</span>
                    <Badge variant={entry.result ? 'success' : 'danger'}>{entry.result ? 'Success' : 'Failure'}</Badge>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </>
      )}
    </>
  )
}
