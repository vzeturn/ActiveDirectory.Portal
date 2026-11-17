import type { ReactNode } from 'react'
import { MoreVertical } from 'lucide-react'
import { EmptyState } from '../EmptyState/EmptyState'
import { Skeleton } from '../Skeleton/Skeleton'
import { DropdownMenu } from '../DropdownMenu/DropdownMenu'
import { Tooltip } from '../Tooltip/Tooltip'
import styles from './DataTable.module.css'

export interface DataTableColumn<T> {
  key: string
  header: string
  render: (row: T) => ReactNode
  width?: string
}

interface DataTableProps<T> {
  columns: DataTableColumn<T>[]
  rows: T[]
  rowKey: (row: T) => string
  onRowClick?: (row: T) => void
  loading?: boolean
  emptyTitle?: string
  emptyDescription?: string
  rowActions?: (row: T) => { label: string; onSelect: () => void; danger?: boolean; disabled?: boolean }[]
  density?: 'compact' | 'comfortable'
}

const SKELETON_ROW_COUNT = 8

export function DataTable<T>({
  columns,
  rows,
  rowKey,
  onRowClick,
  loading = false,
  emptyTitle = 'No results',
  emptyDescription,
  rowActions,
  density = 'comfortable'
}: DataTableProps<T>) {
  if (loading) {
    return (
      <div className={styles.wrapper}>
        <table className={styles.table}>
          <thead>
            <tr>
              {columns.map((col) => (
                <th key={col.key} style={col.width ? { width: col.width } : undefined}>
                  {col.header}
                </th>
              ))}
              {rowActions && <th className={styles.actionsHeader} aria-label="Row actions" />}
            </tr>
          </thead>
          <tbody>
            {Array.from({ length: SKELETON_ROW_COUNT }).map((_, i) => (
              <tr
                key={i}
                className={styles.skeletonRow}
                style={{
                  height: density === 'compact' ? 'var(--table-row-height-compact)' : 'var(--table-row-height-comfortable)'
                }}
              >
                {columns.map((col) => (
                  <td key={col.key}>
                    <Skeleton variant="text" className={styles.skeletonCell} />
                  </td>
                ))}
                {rowActions && <td />}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    )
  }

  if (rows.length === 0) {
    return (
      <div className={styles.wrapper}>
        <EmptyState title={emptyTitle} description={emptyDescription} />
      </div>
    )
  }

  return (
    <div className={styles.wrapper}>
      <table className={styles.table}>
        <thead>
          <tr>
            {columns.map((col) => (
              <th key={col.key} style={col.width ? { width: col.width } : undefined}>
                {col.header}
              </th>
            ))}
            {rowActions && <th className={styles.actionsHeader} aria-label="Row actions" />}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const key = rowKey(row)
            const clickable = Boolean(onRowClick)
            return (
              <tr
                key={key}
                className={[styles.row, clickable ? styles.rowClickable : ''].filter(Boolean).join(' ')}
                style={{
                  height: density === 'compact' ? 'var(--table-row-height-compact)' : 'var(--table-row-height-comfortable)'
                }}
                onClick={clickable ? () => onRowClick?.(row) : undefined}
                tabIndex={clickable ? 0 : undefined}
                role={clickable ? 'button' : undefined}
                onKeyDown={
                  clickable
                    ? (e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault()
                          onRowClick?.(row)
                        }
                      }
                    : undefined
                }
              >
                {columns.map((col) => (
                  <td key={col.key}>{col.render(row)}</td>
                ))}
                {rowActions && (
                  <td className={styles.actionsCell} onClick={(e) => e.stopPropagation()}>
                    <DropdownMenu
                      trigger={
                        <Tooltip label="More actions">
                          <button type="button" className={styles.actionsTrigger} aria-label="More actions">
                            <MoreVertical size={16} aria-hidden="true" />
                          </button>
                        </Tooltip>
                      }
                      items={rowActions(row)}
                    />
                  </td>
                )}
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
