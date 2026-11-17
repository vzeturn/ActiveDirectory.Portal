import { Navigate } from 'react-router-dom'
import { useAuthStore } from '../stores/authStore'
import { Spinner } from './ui/Spinner/Spinner'

const loadingStyle: React.CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  height: '100vh',
  gap: 'var(--space-3)',
  color: 'var(--color-text-muted)'
}

export function ProtectedRoute({ children, requireAdmin }: { children: React.ReactNode; requireAdmin?: boolean }) {
  const { isAuthenticated, isLoading, user } = useAuthStore()
  if (isLoading) {
    return (
      <div style={loadingStyle}>
        <Spinner size="lg" label="Checking session" />
      </div>
    )
  }
  if (!isAuthenticated) return <Navigate to="/login" replace />
  if (requireAdmin && !user?.isAdmin) return <Navigate to="/" replace />
  return <>{children}</>
}
