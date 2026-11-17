import { useState } from 'react'
import type { AdScope } from '../types'
import { Modal } from './ui/Modal/Modal'
import { Button } from './ui/Button/Button'
import * as userService from '../services/user.service'
import styles from './BulkCsvModal.module.css'

interface PreviewRow {
  username: string
  displayName: string | null
  error: string | null
}

interface BulkCsvModalProps {
  open: boolean
  title: string
  scope: AdScope
  onClose: () => void
  onSubmit: (usernames: string[]) => Promise<void>
  disabled?: boolean
  disabledReason?: string
}

const PREVIEW_LIMIT = 50

export function BulkCsvModal({ open, title, scope, onClose, onSubmit, disabled, disabledReason }: BulkCsvModalProps) {
  const [rows, setRows] = useState<PreviewRow[]>([])
  const [submitting, setSubmitting] = useState(false)

  async function handleFile(file: File) {
    const text = await file.text()
    const lines = text.split(/\r?\n/).map((l) => l.trim()).filter(Boolean)
    const usernames = lines[0]?.toLowerCase() === 'username' ? lines.slice(1) : lines

    const preview = await Promise.all(
      usernames.slice(0, PREVIEW_LIMIT).map(async (username) => {
        try {
          const user = await userService.getUser(username, scope)
          return { username, displayName: user.displayName ?? null, error: null }
        } catch {
          return { username, displayName: null, error: 'Not found' }
        }
      })
    )
    const rest = usernames.slice(PREVIEW_LIMIT).map((username) => ({ username, displayName: null, error: null }))
    setRows([...preview, ...rest])
  }

  function handleClose() {
    setRows([])
    onClose()
  }

  return (
    <Modal
      open={open}
      title={title}
      onClose={handleClose}
      footer={
        <>
          <Button variant="secondary" onClick={handleClose}>
            Cancel
          </Button>
          <Button
            disabled={rows.length === 0 || submitting || disabled}
            loading={submitting}
            title={disabled ? disabledReason : undefined}
            onClick={async () => {
              setSubmitting(true)
              await onSubmit(rows.map((r) => r.username))
              setSubmitting(false)
              setRows([])
              onClose()
            }}
          >
            Submit ({rows.length})
          </Button>
        </>
      }
    >
      <input
        type="file"
        accept=".csv,text/csv"
        aria-label="Upload CSV file"
        disabled={disabled}
        title={disabled ? disabledReason : undefined}
        onChange={(e) => {
          const file = e.target.files?.[0]
          e.target.value = ''
          if (file) handleFile(file)
        }}
      />
      {rows.length > 0 && (
        <table className={styles.previewTable}>
          <thead>
            <tr>
              <th>Username</th>
              <th>Name</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.username}>
                <td>{r.username}</td>
                <td>{r.displayName ?? '—'}</td>
                <td>{r.error ?? 'OK'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Modal>
  )
}
