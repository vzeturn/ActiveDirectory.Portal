import { useState } from 'react'
import { Filter } from 'lucide-react'
import * as auditService from '../services/audit.service'
import { useAuditLogs, useAuditOperations } from '../hooks/queries/useAuditQuery'
import { Input } from '../components/ui/Input/Input'
import { Select } from '../components/ui/Select/Select'
import { Button } from '../components/ui/Button/Button'
import { Alert } from '../components/ui/Alert/Alert'
import { Badge } from '../components/ui/Badge/Badge'
import { DataTable } from '../components/ui/Table/DataTable'
import type { DataTableColumn } from '../components/ui/Table/DataTable'
import type { AuditEntry } from '../types'
import styles from './Page.module.css'

export function AuditLogsPage() {
  const [actor, setActor] = useState('')
  const [operation, setOperation] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [filters, setFilters] = useState<{ actor?: string; operation?: string; from?: string; to?: string; page: number; pageSize: number } | null>(null)

  const { data: operations = [], isError: operationsError, error: operationsErrorObj } = useAuditOperations()
  const {
    data: entries = [],
    isLoading: loading,
    isError: entriesError,
    error: entriesErrorObj
  } = useAuditLogs(filters ?? { page: 1, pageSize: 50 }, { enabled: !!filters })
  const searched = !!filters

  const displayedError = entriesError
    ? entriesErrorObj?.message ?? 'Failed to load audit log.'
    : operationsError
      ? operationsErrorObj?.message ?? 'Failed to load operations.'
      : null

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setFilters({
      actor: actor || undefined,
      operation: operation || undefined,
      from: from || undefined,
      to: to || undefined,
      page: 1,
      pageSize: 50
    })
  }

  const columns: DataTableColumn<AuditEntry>[] = [
    { key: 'timestamp', header: 'Timestamp', render: (e) => <span className="tabular-nums">{new Date(e.timestamp).toLocaleString()}</span> },
    { key: 'actor', header: 'Actor', render: (e) => e.actor },
    { key: 'operation', header: 'Operation', render: (e) => e.operation },
    { key: 'target', header: 'Target', render: (e) => e.target },
    { key: 'result', header: 'Result', render: (e) => <Badge variant={e.result ? 'success' : 'danger'}>{e.result ? 'Success' : 'Failure'}</Badge> },
    { key: 'details', header: 'Details', render: (e) => e.details ?? '-' }
  ]

  return (
    <>
      <div className={styles.header}>
        <h1 className={styles.title}>Audit Log</h1>
      </div>

      {displayedError && <Alert variant="error">{displayedError}</Alert>}

      <form onSubmit={handleSearch} className={styles.searchBar}>
        <div className={styles.searchInput}>
          <Input label="Actor" value={actor} onChange={(e) => setActor(e.target.value)} placeholder="Filter by actor..." />
        </div>
        <div className={styles.searchInput}>
          <Select
            label="Operation"
            value={operation}
            onValueChange={setOperation}
            placeholder="All operations"
            options={operations.map((op) => ({ value: op, label: op }))}
          />
        </div>
        <div className={styles.searchInput}>
          <Input type="date" label="From" value={from} onChange={(e) => setFrom(e.target.value)} />
        </div>
        <div className={styles.searchInput}>
          <Input type="date" label="To" value={to} onChange={(e) => setTo(e.target.value)} />
        </div>
        <Button type="submit" loading={loading} icon={!loading ? <Filter size={16} aria-hidden="true" /> : undefined}>
          {loading ? 'Loading...' : 'Filter'}
        </Button>
        <a
          href={auditService.buildExportUrl({ from: from || undefined, to: to || undefined, actor: actor || undefined, operation: operation || undefined })}
          className={styles.exportLink}
        >
          Export CSV
        </a>
      </form>

      <DataTable
        columns={columns}
        rows={entries}
        rowKey={(e) => `${e.timestamp}-${e.actor}-${e.operation}-${e.target}`}
        loading={loading}
        emptyTitle={searched ? 'No audit entries found' : 'No audit entries loaded'}
        emptyDescription={searched ? 'Try different filters.' : 'Use the filters above to load audit log entries.'}
      />
    </>
  )
}
