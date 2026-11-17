import { useId, useMemo, useState } from 'react'
import { Check, Search } from 'lucide-react'
import { useComboboxSearch } from '../../../hooks/useComboboxSearch'
import type { ComboboxOption } from '../Combobox/Combobox'
import { Button } from '../Button/Button'
import { Skeleton } from '../Skeleton/Skeleton'
import styles from './MembershipPicker.module.css'

type RowStatus =
  | { kind: 'pending'; action: 'add' | 'remove' }
  | { kind: 'added' }
  | { kind: 'removed' }
  | { kind: 'error'; message: string }

interface MembershipPickerProps {
  /** Current memberships, matched case-insensitively against option values. */
  members: readonly string[]
  onSearch: (query: string) => Promise<ComboboxOption[]>
  /** Must reject on failure; the rejection message is shown on the row. */
  onAdd: (opt: ComboboxOption) => Promise<unknown>
  onRemove: (opt: ComboboxOption) => Promise<unknown>
  /** Singular noun for copy and labels, e.g. "group" or "user". */
  noun: string
  placeholder?: string
  disabled?: boolean
  disabledReason?: string
}

const MIN_QUERY_LENGTH = 2
const SKELETON_ROWS = 3

function errorMessage(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback
}

export function MembershipPicker({
  members,
  onSearch,
  onAdd,
  onRemove,
  noun,
  placeholder,
  disabled,
  disabledReason
}: MembershipPickerProps) {
  const [query, setQuery] = useState('')
  const [rowState, setRowState] = useState<{ source: ComboboxOption[]; byValue: Record<string, RowStatus> }>({
    source: [],
    byValue: {}
  })
  const [announcement, setAnnouncement] = useState('')
  const listId = useId()

  const { options, isSearching, searchError, retrySearch, highlightedIndex, setHighlightedIndex, handleKeyDown } =
    useComboboxSearch<ComboboxOption>({ isOpen: true, query, onSearch })

  // Row feedback belongs to one result set; a new search starts clean and
  // reflects the refreshed membership (an added row now offers Remove).
  const statusByValue = rowState.source === options ? rowState.byValue : {}

  const memberSet = useMemo(() => new Set(members.map((m) => m.toLowerCase())), [members])
  const isMember = (value: string) => memberSet.has(value.toLowerCase())

  const setStatus = (value: string, status: RowStatus) =>
    setRowState((prev) => ({
      source: options,
      byValue: { ...(prev.source === options ? prev.byValue : {}), [value]: status }
    }))

  async function runAction(opt: ComboboxOption) {
    const current = statusByValue[opt.value]
    if (disabled || current?.kind === 'pending' || current?.kind === 'added' || current?.kind === 'removed') return

    const action = isMember(opt.value) ? 'remove' : 'add'
    setStatus(opt.value, { kind: 'pending', action })
    try {
      await (action === 'add' ? onAdd(opt) : onRemove(opt))
      setStatus(opt.value, { kind: action === 'add' ? 'added' : 'removed' })
      setAnnouncement(`${opt.label} ${action === 'add' ? 'added' : 'removed'}.`)
    } catch (err) {
      const message = errorMessage(err, `Could not ${action} ${opt.label}.`)
      setStatus(opt.value, { kind: 'error', message })
      setAnnouncement(message)
    }
  }

  const trimmed = query.trim()
  const showHint = trimmed.length < MIN_QUERY_LENGTH
  const showSkeleton = !showHint && isSearching && options.length === 0
  const showEmpty = !showHint && !isSearching && !searchError && options.length === 0

  return (
    <div className={styles.picker}>
      <div className={styles.searchField}>
        <Search size={16} className={styles.searchIcon} aria-hidden="true" />
        <input
          type="search"
          className={styles.input}
          value={query}
          placeholder={placeholder ?? `Search ${noun}s…`}
          aria-label={`Search ${noun}s`}
          aria-controls={listId}
          autoFocus
          autoComplete="off"
          spellCheck={false}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={(e) => {
            // Escape belongs to the dialog (close); only intercept list navigation.
            if (e.key === 'Escape') return
            handleKeyDown(e, runAction, () => {}, options)
          }}
        />
        {isSearching && options.length > 0 && <span className={styles.refreshing} aria-hidden="true" />}
      </div>

      <div className={styles.results} id={listId} aria-busy={isSearching}>
        {showHint && (
          <p className={styles.message}>Type at least {MIN_QUERY_LENGTH} characters to search {noun}s.</p>
        )}

        {showSkeleton && (
          <ul className={styles.list} aria-hidden="true">
            {Array.from({ length: SKELETON_ROWS }, (_, i) => (
              <li key={i} className={styles.row}>
                <Skeleton width="45%" />
                <Skeleton variant="rect" width={88} height={32} />
              </li>
            ))}
          </ul>
        )}

        {!showHint && searchError && (
          <div className={`${styles.message} ${styles.messageError}`} role="alert">
            <span>Search failed: {searchError}</span>
            <Button variant="secondary" size="sm" onClick={retrySearch}>
              Retry
            </Button>
          </div>
        )}

        {showEmpty && (
          <p className={styles.message}>
            No {noun}s match “{trimmed}”.
          </p>
        )}

        {!showHint && options.length > 0 && (
          <>
            <p className={styles.count} aria-live="polite">
              {options.length} {options.length === 1 ? 'result' : 'results'}
            </p>
            <ul className={styles.list}>
              {options.map((opt, index) => (
                <PickerRow
                  key={opt.value}
                  option={opt}
                  member={isMember(opt.value)}
                  status={statusByValue[opt.value]}
                  highlighted={index === highlightedIndex}
                  disabled={disabled}
                  disabledReason={disabledReason}
                  onHover={() => setHighlightedIndex(index)}
                  onAction={() => runAction(opt)}
                />
              ))}
            </ul>
          </>
        )}
      </div>

      <div className={styles.srOnly} aria-live="polite" role="status">
        {announcement}
      </div>
    </div>
  )
}

interface PickerRowProps {
  option: ComboboxOption
  member: boolean
  status: RowStatus | undefined
  highlighted: boolean
  disabled?: boolean
  disabledReason?: string
  onHover: () => void
  onAction: () => void
}

function PickerRow({ option, member, status, highlighted, disabled, disabledReason, onHover, onAction }: PickerRowProps) {
  const errorId = useId()
  const done = status?.kind === 'added' || status?.kind === 'removed'
  // While an add/remove is in flight or just finished, the badge reflects the
  // outcome rather than the (not yet refetched) server membership.
  const showMemberBadge = status?.kind === 'added' || (member && status?.kind !== 'removed')

  return (
    <li
      className={[styles.rowItem, highlighted && styles.rowHighlighted].filter(Boolean).join(' ')}
      onMouseEnter={onHover}
    >
      <div className={styles.row}>
        <div className={styles.label}>
          <span className={styles.labelText} title={option.label}>
            {option.label}
          </span>
          {showMemberBadge && <span className={styles.memberTag}>Member</span>}
        </div>

        {done ? (
          <span className={styles.doneAction} aria-label={`${option.label} ${status.kind}`}>
            <Check size={16} aria-hidden="true" />
            {status.kind === 'added' ? 'Added' : 'Removed'}
          </span>
        ) : (
          <Button
            size="sm"
            variant={member ? 'secondary' : 'primary'}
            className={[styles.action, member && styles.removeAction].filter(Boolean).join(' ')}
            loading={status?.kind === 'pending'}
            disabled={disabled}
            title={disabled ? disabledReason : undefined}
            aria-label={`${member ? 'Remove' : 'Add'} ${option.label}`}
            aria-describedby={status?.kind === 'error' ? errorId : undefined}
            onClick={onAction}
          >
            {status?.kind === 'pending'
              ? status.action === 'add'
                ? 'Adding'
                : 'Removing'
              : status?.kind === 'error'
                ? 'Retry'
                : member
                  ? 'Remove'
                  : 'Add'}
          </Button>
        )}
      </div>

      {status?.kind === 'error' && (
        <p id={errorId} className={styles.rowError} role="alert">
          {status.message}
        </p>
      )}
    </li>
  )
}
