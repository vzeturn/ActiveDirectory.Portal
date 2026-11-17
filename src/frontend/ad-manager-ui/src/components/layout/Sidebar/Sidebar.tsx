import type { ReactNode } from 'react'
import { NavLink, useNavigate } from 'react-router-dom'
import { LayoutDashboard, Users, UsersRound, ScrollText, HeartPulse, LogOut, Moon, Sun, MonitorCog, ShieldCheck } from 'lucide-react'
import { useAuthStore } from '../../../stores/authStore'
import { useTheme } from '../../../hooks/useTheme'
import { Tooltip } from '../../ui/Tooltip/Tooltip'
import styles from './Sidebar.module.css'

const NAV_ITEMS = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/users', label: 'Users', icon: Users },
  { to: '/groups', label: 'Groups', icon: UsersRound },
  { to: '/audit', label: 'Audit Log', icon: ScrollText }
]

function nextTheme(theme: string): 'light' | 'dark' | 'system' {
  if (theme === 'system') return 'light'
  if (theme === 'light') return 'dark'
  return 'system'
}

interface SidebarProps {
  mobileCloseButton?: ReactNode
  onNavigate?: () => void
}

export function Sidebar({ mobileCloseButton, onNavigate }: SidebarProps = {}) {
  const user = useAuthStore((s) => s.user)
  const logout = useAuthStore((s) => s.logout)
  const navigate = useNavigate()
  const { theme, setTheme } = useTheme()

  async function handleSignOut() {
    await logout()
    navigate('/login')
  }

  const initials = (user?.displayName ?? user?.username ?? '?')
    .trim()
    .split(/\s+/)
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

  return (
    <aside className={styles.sidebar}>
      <div className={styles.brand}>
        <span className={styles.brandLabel}>
          <span className={styles.brandIcon}>
            <ShieldCheck size={18} aria-hidden="true" />
          </span>
          AD Manager
        </span>
        {mobileCloseButton}
      </div>

      <nav className={styles.nav} aria-label="Primary">
        {NAV_ITEMS.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            onClick={onNavigate}
            className={({ isActive }) => [styles.navLink, isActive ? styles.navLinkActive : ''].filter(Boolean).join(' ')}
          >
            <Icon size={18} aria-hidden="true" />
            {label}
          </NavLink>
        ))}
        {user?.isAdmin && (
          <NavLink
            to="/health"
            onClick={onNavigate}
            className={({ isActive }) => [styles.navLink, isActive ? styles.navLinkActive : ''].filter(Boolean).join(' ')}
          >
            <HeartPulse size={18} aria-hidden="true" />
            System Health
          </NavLink>
        )}
      </nav>

      <div className={styles.footer}>
        <div className={styles.userRow}>
          <span className={styles.avatar}>{initials}</span>
          <div className={styles.userInfo}>
            <span className={styles.userName}>{user?.displayName ?? user?.username ?? 'Unknown'}</span>
            <span className={styles.userMeta}>{user?.isAdmin ? 'Administrator' : user?.username}</span>
          </div>
        </div>
        <div className={styles.actionsRow}>
          <button
            className={styles.signOutButton}
            onClick={handleSignOut}
          >
            <LogOut size={16} aria-hidden="true" />
            Sign out
          </button>
          <Tooltip label={`Switch theme (current: ${theme})`}>
            <button
              className={styles.themeToggle}
              onClick={() => setTheme(nextTheme(theme))}
              aria-label={`Switch theme (current: ${theme})`}
            >
              {theme === 'dark' ? (
                <Moon size={16} aria-hidden="true" />
              ) : theme === 'light' ? (
                <Sun size={16} aria-hidden="true" />
              ) : (
                <MonitorCog size={16} aria-hidden="true" />
              )}
            </button>
          </Tooltip>
        </div>
      </div>
    </aside>
  )
}
