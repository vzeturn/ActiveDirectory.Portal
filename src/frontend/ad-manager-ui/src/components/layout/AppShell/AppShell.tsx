import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Menu, X } from 'lucide-react'
import { Sidebar } from '../Sidebar/Sidebar'
import styles from './AppShell.module.css'

interface AppShellProps {
  children: ReactNode
}

export function AppShell({ children }: AppShellProps) {
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false)

  useEffect(() => {
    if (!isMobileNavOpen) return
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = ''
    }
  }, [isMobileNavOpen])

  return (
    <div className={styles.shell}>
      <button
        type="button"
        className={styles.menuToggle}
        onClick={() => setIsMobileNavOpen(true)}
        aria-label="Open navigation menu"
        aria-expanded={isMobileNavOpen}
      >
        <Menu size={20} aria-hidden="true" />
      </button>

      <div className={[styles.sidebarWrap, isMobileNavOpen ? styles.sidebarWrapOpen : ''].filter(Boolean).join(' ')}>
        <Sidebar
          mobileCloseButton={
            <button
              type="button"
              className={styles.closeToggle}
              onClick={() => setIsMobileNavOpen(false)}
              aria-label="Close navigation menu"
            >
              <X size={20} aria-hidden="true" />
            </button>
          }
          onNavigate={() => setIsMobileNavOpen(false)}
        />
      </div>

      {isMobileNavOpen && (
        <button
          type="button"
          className={styles.backdrop}
          onClick={() => setIsMobileNavOpen(false)}
          aria-label="Close navigation menu"
          tabIndex={-1}
        />
      )}

      <main className={styles.content}>
        <div className={styles.contentInner}>{children}</div>
      </main>
    </div>
  )
}
