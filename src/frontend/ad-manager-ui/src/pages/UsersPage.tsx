import { useCallback, useState } from 'react'
import { Search, KeyRound, Copy, SlidersHorizontal, Save, Trash2 } from 'lucide-react'
import type { AdUser, AdScope, BulkOpItemResult, SavedUserFilters } from '../types'
import * as userService from '../services/user.service'
import * as groupService from '../services/group.service'
import { useUsersSearch, useUserGroups } from '../hooks/queries/useUsersQuery'
import { useToggleUserAccount, useResetPassword, useAddUserToGroups, useRemoveUserFromGroup } from '../hooks/queries/useUserMutations'
import { useSavedSearches, useCreateSavedSearch, useDeleteSavedSearch } from '../hooks/queries/useSavedSearchesQuery'
import { Skeleton } from '../components/ui/Skeleton/Skeleton'
import { Input } from '../components/ui/Input/Input'
import { Select } from '../components/ui/Select/Select'
import { Button } from '../components/ui/Button/Button'
import { Alert } from '../components/ui/Alert/Alert'
import { Badge } from '../components/ui/Badge/Badge'
import { Panel } from '../components/ui/Panel/Panel'
import { Checkbox } from '../components/ui/Checkbox/Checkbox'
import { EmptyState } from '../components/ui/EmptyState/EmptyState'
import { Modal } from '../components/ui/Modal/Modal'
import { MultiSelect } from '../components/ui/MultiSelect/MultiSelect'
import { MembershipPicker } from '../components/ui/MembershipPicker/MembershipPicker'
import { Tooltip } from '../components/ui/Tooltip/Tooltip'
import { useToast } from '../components/ui/Toast/useToast'
import { ToastContainer } from '../components/ui/Toast/Toast'
import { DataTable } from '../components/ui/Table/DataTable'
import type { DataTableColumn } from '../components/ui/Table/DataTable'
import { BulkCsvModal } from '../components/BulkCsvModal'
import { useAuthStore } from '../stores/authStore'
import styles from './Page.module.css'

const VIEW_ONLY_TITLE = 'View-only access'
const GROUP_SKELETON_ROWS = 4

function isSavedUserFilters(value: unknown): value is SavedUserFilters {
  if (typeof value !== 'object' || value === null) return false
  const v = value as Record<string, unknown>
  return typeof v.query === 'string' && (v.scope === 'ou' || v.scope === 'domain')
}

export function UsersPage() {
  const { user } = useAuthStore()
  const canWrite = user?.isAdmin === true
  const [query, setQuery] = useState('')
  const [scope, setScope] = useState<AdScope>('ou')
  const [searchArgs, setSearchArgs] = useState<{ query: string; scope: AdScope } | null>(null)
  const [selected, setSelected] = useState<AdUser | null>(null)
  const [selectedScope, setSelectedScope] = useState<AdScope>('ou')
  const [error, setError] = useState<string | null>(null)
  const [lastPassword, setLastPassword] = useState<string | null>(null)
  const [resetMode, setResetMode] = useState<'random' | 'manual'>('random')
  const [manualPassword, setManualPassword] = useState('')
  const [selectedUsernames, setSelectedUsernames] = useState<string[]>([])
  const searchGroups = useCallback((q: string) => groupService.searchGroupOptions(q, scope), [scope])
  const [bulkGroupModalOpen, setBulkGroupModalOpen] = useState(false)
  const [bulkGroupNames, setBulkGroupNames] = useState<string[]>([])
  const [csvResetOpen, setCsvResetOpen] = useState(false)
  const [addToGroupModalOpen, setAddToGroupModalOpen] = useState(false)
  const { toasts, show, dismiss } = useToast()

  const [filtersOpen, setFiltersOpen] = useState(false)
  const [ouFilter, setOuFilter] = useState('')
  const [enabledOnly, setEnabledOnly] = useState(false)
  const [lockedOnly, setLockedOnly] = useState(false)
  const [memberOfFilter, setMemberOfFilter] = useState('')
  const [selectedSavedSearchId, setSelectedSavedSearchId] = useState('')
  const [saveSearchModalOpen, setSaveSearchModalOpen] = useState(false)
  const [saveSearchName, setSaveSearchName] = useState('')
  const [saveSearchError, setSaveSearchError] = useState<string | null>(null)

  const { data: savedSearches = [], isError: savedSearchesIsError } = useSavedSearches()
  const createSavedSearchMutation = useCreateSavedSearch()
  const deleteSavedSearchMutation = useDeleteSavedSearch()

  const {
    data: results = [],
    isLoading: loading,
    isError: searchIsError,
    error: searchError
  } = useUsersSearch(searchArgs?.query ?? '', 100, searchArgs?.scope ?? scope, {
    enabled: !!searchArgs,
    filters: searchArgs
      ? {
          ou: ouFilter || undefined,
          enabledOnly: enabledOnly || undefined,
          lockedOnly: lockedOnly || undefined,
          memberOf: memberOfFilter || undefined
        }
      : undefined
  })
  const searched = !!searchArgs

  const {
    data: groups = [],
    isLoading: groupsLoading,
    isError: groupsIsError,
    error: groupsError,
    refetch: refetchGroups
  } = useUserGroups(selected?.username, selectedScope, { enabled: !!selected })

  const enableMutation = useToggleUserAccount('enable')
  const disableMutation = useToggleUserAccount('disable')
  const unlockMutation = useToggleUserAccount('unlock')
  const resetPasswordMutation = useResetPassword()
  const addToGroupsMutation = useAddUserToGroups()
  const removeFromGroupMutation = useRemoveUserFromGroup()

  const displayedError = error ?? (searchIsError ? searchError?.message ?? 'Search failed.' : null)

  function runSearch() {
    setError(null)
    setSearchArgs({ query, scope })
  }

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    runSearch()
  }

  function selectUser(user: AdUser) {
    setSelected(user)
    setSelectedScope(searchArgs?.scope ?? scope)
    setLastPassword(null)
  }

  async function runToggle(action: 'enable' | 'disable' | 'unlock', target?: AdUser) {
    const targetUser = target ?? selected
    if (!targetUser) return
    setError(null)
    const mutation = action === 'enable' ? enableMutation : action === 'disable' ? disableMutation : unlockMutation
    try {
      await mutation.mutateAsync({ username: targetUser.username, scope })
      const refreshed = await userService.getUser(targetUser.username, scope)
      setSelected((prev) => (prev?.username === refreshed.username ? refreshed : prev))
    } catch (err) {
      setError(err instanceof Error ? err.message : `${action} failed.`)
    }
  }

  async function handleResetPassword() {
    if (!selected) return
    setError(null)
    try {
      const password = await resetPasswordMutation.mutateAsync({
        username: selected.username,
        newPassword: resetMode === 'manual' ? manualPassword : undefined,
        requireChange: true,
        scope
      })
      setLastPassword(password)
      setResetMode('random')
      setManualPassword('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Reset failed.')
    }
  }

  // The picker surfaces per-row success/failure itself, so these just rethrow.
  const addUserToGroup = (opt: { value: string }) =>
    addToGroupsMutation.mutateAsync({ username: selected!.username, groupNames: [opt.value], scope })
  const removeUserFromGroup = (opt: { value: string }) =>
    removeFromGroupMutation.mutateAsync({ username: selected!.username, groupName: opt.value, scope })

  async function handleRemoveFromGroup(groupName: string) {
    if (!selected) return
    setError(null)
    try {
      await removeFromGroupMutation.mutateAsync({ username: selected.username, groupName, scope })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to remove from group.')
    }
  }

  function reportBulkResult(results: BulkOpItemResult[], verb: string) {
    const failed = results.filter((r) => !r.success)
    if (failed.length === 0) {
      show(`${results.length} user(s) ${verb}.`, 'success')
    } else {
      show(
        `${results.length - failed.length}/${results.length} ${verb}; ${failed.length} failed: ${failed.map((f) => f.username).join(', ')}`,
        'error'
      )
    }
    setSelectedUsernames([])
    runSearch()
  }

  function applySavedSearch(id: string) {
    setSelectedSavedSearchId(id)
    const saved = savedSearches.find((s) => s.id === id)
    if (!saved) return
    try {
      const parsed = JSON.parse(saved.filterJson)
      if (!isSavedUserFilters(parsed)) throw new Error('Invalid saved filter shape.')
      setQuery(parsed.query)
      setScope(parsed.scope)
      setOuFilter(parsed.ou ?? '')
      setEnabledOnly(parsed.enabledOnly ?? false)
      setLockedOnly(parsed.lockedOnly ?? false)
      setMemberOfFilter(parsed.memberOf ?? '')
      setFiltersOpen(true)
      setError(null)
      setSearchArgs({ query: parsed.query, scope: parsed.scope })
    } catch {
      setError('This saved search could not be loaded — it may be corrupted.')
    }
  }

  function openSaveSearchModal() {
    setSaveSearchError(null)
    setSaveSearchName('')
    setSaveSearchModalOpen(true)
  }

  async function handleSaveSearch() {
    if (!saveSearchName.trim()) {
      setSaveSearchError('Name is required.')
      return
    }
    setSaveSearchError(null)
    const filters: SavedUserFilters = {
      query,
      scope,
      ou: ouFilter || undefined,
      enabledOnly: enabledOnly || undefined,
      lockedOnly: lockedOnly || undefined,
      memberOf: memberOfFilter || undefined
    }
    try {
      await createSavedSearchMutation.mutateAsync({ name: saveSearchName.trim(), filterJson: JSON.stringify(filters) })
      show('Search saved.', 'success')
      setSaveSearchModalOpen(false)
    } catch (err) {
      setSaveSearchError(err instanceof Error ? err.message : 'Failed to save search.')
    }
  }

  async function handleDeleteSavedSearch(id: string) {
    try {
      await deleteSavedSearchMutation.mutateAsync(id)
      if (selectedSavedSearchId === id) setSelectedSavedSearchId('')
      show('Saved search deleted.', 'success')
    } catch (err) {
      show(err instanceof Error ? err.message : 'Failed to delete saved search.', 'error')
    }
  }

  function closePanel() {
    setSelected(null)
    setLastPassword(null)
    setResetMode('random')
    setManualPassword('')
  }

  const columns: DataTableColumn<AdUser>[] = [
    {
      key: 'select',
      header: '',
      width: '40px',
      render: (u) => (
        <div onClick={(e) => e.stopPropagation()}>
          <Checkbox
            checked={selectedUsernames.includes(u.username)}
            onChange={(checked) =>
              setSelectedUsernames((prev) => (checked ? [...prev, u.username] : prev.filter((x) => x !== u.username)))
            }
          />
        </div>
      )
    },
    {
      key: 'name',
      header: 'Name',
      render: (u) => u.displayName ?? u.username
    },
    {
      key: 'username',
      header: 'Username',
      render: (u) => u.username
    },
    {
      key: 'email',
      header: 'Email / Department',
      render: (u) => u.email ?? u.department ?? '-'
    },
    {
      key: 'status',
      header: 'Status',
      render: (u) => (
        <span className={styles.detailStatusRow}>
          <Badge variant={u.enabled ? 'success' : 'danger'}>{u.enabled ? 'Enabled' : 'Disabled'}</Badge>
          {u.isLockedOut && <Badge variant="warning">Locked</Badge>}
        </span>
      )
    }
  ]

  return (
    <>
      <div className={styles.header}>
        <h1 className={styles.title}>Users</h1>
      </div>

      {displayedError && !selected && <Alert variant="error">{displayedError}</Alert>}

      <form onSubmit={handleSearch} className={styles.searchBar}>
        <div className={styles.searchInput}>
          <Input
            label="Search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search users by name or username..."
            required
          />
        </div>
        <div className={styles.scopeSelect}>
          <Select
            label="Scope"
            value={scope}
            onValueChange={(v) => {
              setScope(v as AdScope)
              if (selected) refetchGroups()
            }}
            options={[
              { value: 'ou', label: 'OU scope' },
              { value: 'domain', label: 'Domain scope' },
            ]}
          />
        </div>
        <Button type="submit" loading={loading} icon={!loading ? <Search size={16} aria-hidden="true" /> : undefined}>
          {loading ? 'Searching...' : 'Search'}
        </Button>
        <Button
          type="button"
          variant="secondary"
          onClick={() => setCsvResetOpen(true)}
          disabled={!canWrite}
          title={!canWrite ? VIEW_ONLY_TITLE : undefined}
        >
          Bulk reset (CSV)…
        </Button>
      </form>

      <div className={styles.savedSearchRow}>
        <div className={styles.savedSearchSelect}>
          <Select
            label="Saved searches"
            value={selectedSavedSearchId}
            onValueChange={applySavedSearch}
            placeholder="Select a saved search…"
            options={savedSearches.map((s) => ({ value: s.id, label: s.name }))}
          />
        </div>
        {selectedSavedSearchId && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            icon={<Trash2 size={14} aria-hidden="true" />}
            onClick={() => handleDeleteSavedSearch(selectedSavedSearchId)}
            disabled={!canWrite}
            title={!canWrite ? VIEW_ONLY_TITLE : undefined}
          >
            Delete
          </Button>
        )}
        <Button
          type="button"
          variant="ghost"
          size="sm"
          icon={<SlidersHorizontal size={14} aria-hidden="true" />}
          onClick={() => setFiltersOpen((v) => !v)}
        >
          {filtersOpen ? 'Hide filters' : 'Advanced filters'}
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          icon={<Save size={14} aria-hidden="true" />}
          onClick={openSaveSearchModal}
          disabled={!canWrite || !query.trim()}
          title={!canWrite ? VIEW_ONLY_TITLE : undefined}
        >
          Save current filters…
        </Button>
      </div>

      {savedSearchesIsError && <Alert variant="error">Failed to load saved searches.</Alert>}

      {filtersOpen && (
        <div className={styles.filtersPanel}>
          <div className={styles.filterField}>
            <Input label="Organizational unit contains" value={ouFilter} onChange={(e) => setOuFilter(e.target.value)} placeholder="e.g. OU=Sales" />
          </div>
          <div className={styles.filterField}>
            <Input label="Member of group" value={memberOfFilter} onChange={(e) => setMemberOfFilter(e.target.value)} placeholder="Group name" />
          </div>
          <div className={styles.filterCheckboxes}>
            <Checkbox checked={enabledOnly} onChange={setEnabledOnly} label="Enabled only" />
            <Checkbox checked={lockedOnly} onChange={setLockedOnly} label="Locked only" />
          </div>
        </div>
      )}

      {selectedUsernames.length > 0 && (
        <div className={styles.bulkToolbar}>
          <span>{selectedUsernames.length} selected</span>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => userService.bulkEnable(selectedUsernames, scope).then((r) => reportBulkResult(r, 'enabled'))}
            disabled={!canWrite}
            title={!canWrite ? VIEW_ONLY_TITLE : undefined}
          >
            Enable
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => userService.bulkDisable(selectedUsernames, scope).then((r) => reportBulkResult(r, 'disabled'))}
            disabled={!canWrite}
            title={!canWrite ? VIEW_ONLY_TITLE : undefined}
          >
            Disable
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => userService.bulkUnlock(selectedUsernames, scope).then((r) => reportBulkResult(r, 'unlocked'))}
            disabled={!canWrite}
            title={!canWrite ? VIEW_ONLY_TITLE : undefined}
          >
            Unlock
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => setBulkGroupModalOpen(true)}
            disabled={!canWrite}
            title={!canWrite ? VIEW_ONLY_TITLE : undefined}
          >
            Add to group(s)…
          </Button>
        </div>
      )}

      <DataTable
        columns={columns}
        rows={results}
        rowKey={(u) => u.username}
        onRowClick={selectUser}
        loading={loading}
        emptyTitle={searched ? 'No users found' : 'Search for users'}
        emptyDescription={searched ? 'Try a different search term or scope.' : 'Enter a name or username above to get started.'}
        rowActions={(u) => [
          {
            label: 'Enable',
            onSelect: () => runToggle('enable', u),
            disabled: u.enabled || !canWrite
          },
          {
            label: 'Disable',
            onSelect: () => runToggle('disable', u),
            danger: true,
            disabled: !u.enabled || !canWrite
          },
          {
            label: 'Unlock',
            onSelect: () => runToggle('unlock', u),
            disabled: !u.isLockedOut || !canWrite
          }
        ]}
      />

      <Panel open={!!selected} onClose={closePanel} title={selected?.displayName ?? selected?.username ?? 'User detail'}>
        {selected && (
          <div className={styles.detailSection}>
            {displayedError && <Alert variant="error">{displayedError}</Alert>}

            <div className={styles.detailStatusRow}>
              <Badge variant={selected.enabled ? 'success' : 'danger'}>{selected.enabled ? 'Enabled' : 'Disabled'}</Badge>
              {selected.isLockedOut && <Badge variant="warning">Locked</Badge>}
            </div>

            <div className={styles.detailGrid}>
              <dl className={`${styles.dl} ${styles.detailColumn}`}>
                <dt className={styles.dt}>Username</dt>
                <dd className={styles.dd}>{selected.username}</dd>
                {selected.displayName && (
                  <>
                    <dt className={styles.dt}>Display Name</dt>
                    <dd className={styles.dd}>{selected.displayName}</dd>
                  </>
                )}
                {selected.title && (
                  <>
                    <dt className={styles.dt}>Title</dt>
                    <dd className={styles.dd}>{selected.title}</dd>
                  </>
                )}
                {selected.department && (
                  <>
                    <dt className={styles.dt}>Department</dt>
                    <dd className={styles.dd}>{selected.department}</dd>
                  </>
                )}
                {selected.manager && (
                  <>
                    <dt className={styles.dt}>Manager</dt>
                    <dd className={styles.dd}>{selected.manager}</dd>
                  </>
                )}
                {selected.company && (
                  <>
                    <dt className={styles.dt}>Company</dt>
                    <dd className={styles.dd}>{selected.company}</dd>
                  </>
                )}
                {selected.email && (
                  <>
                    <dt className={styles.dt}>Email</dt>
                    <dd className={styles.dd}>{selected.email}</dd>
                  </>
                )}
                {selected.phone && (
                  <>
                    <dt className={styles.dt}>Phone</dt>
                    <dd className={styles.dd}>{selected.phone}</dd>
                  </>
                )}
                {selected.mobile && (
                  <>
                    <dt className={styles.dt}>Mobile</dt>
                    <dd className={styles.dd}>{selected.mobile}</dd>
                  </>
                )}
              </dl>
              <dl className={`${styles.dl} ${styles.detailColumn}`}>
                {selected.organizationalUnit && (
                  <>
                    <dt className={styles.dt}>OU</dt>
                    <dd className={styles.dd}>{selected.organizationalUnit}</dd>
                  </>
                )}
                {selected.created && (
                  <>
                    <dt className={styles.dt}>Created</dt>
                    <dd className={styles.dd}>{new Date(selected.created).toLocaleString()}</dd>
                  </>
                )}
                {selected.modified && (
                  <>
                    <dt className={styles.dt}>Modified</dt>
                    <dd className={styles.dd}>{new Date(selected.modified).toLocaleString()}</dd>
                  </>
                )}
                {selected.lastLogon && (
                  <>
                    <dt className={styles.dt}>Last Logon</dt>
                    <dd className={styles.dd}>{new Date(selected.lastLogon).toLocaleString()}</dd>
                  </>
                )}
                {selected.passwordLastSet && (
                  <>
                    <dt className={styles.dt}>Password Last Set</dt>
                    <dd className={styles.dd}>{new Date(selected.passwordLastSet).toLocaleString()}</dd>
                  </>
                )}
                {selected.description && (
                  <>
                    <dt className={styles.dt}>Description</dt>
                    <dd className={styles.dd}>{selected.description}</dd>
                  </>
                )}
              </dl>
            </div>

            <div className={styles.actionsRow}>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => runToggle('enable')}
                disabled={selected.enabled || !canWrite}
                title={!canWrite ? VIEW_ONLY_TITLE : undefined}
              >
                Enable
              </Button>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => runToggle('disable')}
                disabled={!selected.enabled || !canWrite}
                title={!canWrite ? VIEW_ONLY_TITLE : undefined}
              >
                Disable
              </Button>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => runToggle('unlock')}
                disabled={!selected.isLockedOut || !canWrite}
                title={!canWrite ? VIEW_ONLY_TITLE : undefined}
              >
                Unlock
              </Button>
            </div>

            <div className={styles.resetPasswordForm}>
              <div className={styles.radioGroup}>
                <label>
                  <input
                    type="radio"
                    name="resetMode"
                    checked={resetMode === 'random'}
                    onChange={() => setResetMode('random')}
                    disabled={!canWrite}
                    title={!canWrite ? VIEW_ONLY_TITLE : undefined}
                  />
                  Random (strong)
                </label>
                <label>
                  <input
                    type="radio"
                    name="resetMode"
                    checked={resetMode === 'manual'}
                    onChange={() => setResetMode('manual')}
                    disabled={!canWrite}
                    title={!canWrite ? VIEW_ONLY_TITLE : undefined}
                  />
                  Manual
                </label>
              </div>
              {resetMode === 'manual' && (
                <Input
                  type="password"
                  value={manualPassword}
                  onChange={(e) => setManualPassword(e.target.value)}
                  placeholder="Enter new password"
                  disabled={!canWrite}
                  title={!canWrite ? VIEW_ONLY_TITLE : undefined}
                />
              )}
              <Button
                variant="danger"
                size="sm"
                onClick={handleResetPassword}
                icon={<KeyRound size={14} aria-hidden="true" />}
                disabled={!canWrite}
                title={!canWrite ? VIEW_ONLY_TITLE : undefined}
              >
                Reset Password
              </Button>
            </div>

            {lastPassword && (
              <div className={styles.passwordBlock}>
                <Alert variant="success">New password (shown once — copy now)</Alert>
                <div className={styles.detailStatusRow}>
                  <code className={styles.passwordCode}>{lastPassword}</code>
                  <Button
                    variant="secondary"
                    size="sm"
                    icon={<Copy size={14} aria-hidden="true" />}
                    onClick={() => navigator.clipboard.writeText(lastPassword ?? '')}
                  >
                    Copy
                  </Button>
                </div>
              </div>
            )}

            <div>
              <div className={styles.detailStatusRow}>
                <div className={styles.subheading}>Group Membership</div>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => setAddToGroupModalOpen(true)}
                  disabled={!canWrite}
                  title={!canWrite ? VIEW_ONLY_TITLE : undefined}
                >
                  Manage groups…
                </Button>
              </div>
              {groupsLoading ? (
                <ul className={styles.groupList}>
                  {Array.from({ length: GROUP_SKELETON_ROWS }).map((_, i) => (
                    <li key={i} className={styles.memberItem} data-testid="group-skeleton-row">
                      <Skeleton variant="text" width="70%" />
                    </li>
                  ))}
                </ul>
              ) : groupsIsError ? (
                <Alert variant="error">{groupsError?.message ?? 'Failed to load group memberships.'}</Alert>
              ) : groups.length > 0 ? (
                <ul className={styles.groupList}>
                  {groups.map((g) => (
                    <li key={g} className={styles.memberItem}>
                      {g}
                      <Tooltip label={`Remove from ${g}`}>
                        <button
                          type="button"
                          onClick={() => handleRemoveFromGroup(g)}
                          aria-label={`Remove from ${g}`}
                          disabled={!canWrite}
                          title={!canWrite ? VIEW_ONLY_TITLE : undefined}
                        >
                          &times;
                        </button>
                      </Tooltip>
                    </li>
                  ))}
                </ul>
              ) : (
                <EmptyState title="No group memberships" description="This user does not belong to any groups." />
              )}
            </div>
          </div>
        )}
      </Panel>

      <Modal
        open={addToGroupModalOpen && !!selected}
        title={`Manage groups · ${selected?.displayName || selected?.username || ''}`}
        onClose={() => setAddToGroupModalOpen(false)}
        footer={
          <Button variant="secondary" onClick={() => setAddToGroupModalOpen(false)}>
            Done
          </Button>
        }
      >
        {selected && (
          <MembershipPicker
            members={groups}
            onSearch={searchGroups}
            onAdd={addUserToGroup}
            onRemove={removeUserFromGroup}
            noun="group"
            disabled={!canWrite}
            disabledReason={VIEW_ONLY_TITLE}
          />
        )}
      </Modal>

      <Modal
        open={bulkGroupModalOpen}
        title="Add users to group(s)"
        onClose={() => setBulkGroupModalOpen(false)}
        footer={
          <>
            <Button variant="secondary" onClick={() => setBulkGroupModalOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={async () => {
                const bulkResults = await userService.bulkAddToGroups(selectedUsernames, bulkGroupNames, scope)
                reportBulkResult(bulkResults, 'added to group(s)')
                setBulkGroupModalOpen(false)
                setBulkGroupNames([])
              }}
              disabled={!canWrite}
              title={!canWrite ? VIEW_ONLY_TITLE : undefined}
            >
              Add
            </Button>
          </>
        }
      >
        <MultiSelect
          selected={bulkGroupNames}
          onChange={setBulkGroupNames}
          onSearch={searchGroups}
          placeholder="Search groups…"
          disabled={!canWrite}
          title={!canWrite ? VIEW_ONLY_TITLE : undefined}
        />
      </Modal>
      <Modal
        open={saveSearchModalOpen}
        title="Save search"
        onClose={() => setSaveSearchModalOpen(false)}
        footer={
          <>
            <Button variant="secondary" onClick={() => setSaveSearchModalOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleSaveSearch} loading={createSavedSearchMutation.isPending}>
              Save
            </Button>
          </>
        }
      >
        {saveSearchError && <Alert variant="error">{saveSearchError}</Alert>}
        <Input
          label="Name"
          value={saveSearchName}
          onChange={(e) => setSaveSearchName(e.target.value)}
          placeholder="e.g. Locked sales accounts"
          required
        />
      </Modal>

      <BulkCsvModal
        open={csvResetOpen}
        title="Bulk password reset"
        scope={scope}
        onClose={() => setCsvResetOpen(false)}
        onSubmit={async (usernames) => {
          const bulkResults = await userService.bulkResetPassword(usernames, true, scope)
          reportBulkResult(bulkResults, 'reset')
        }}
        disabled={!canWrite}
        disabledReason={VIEW_ONLY_TITLE}
      />
      <ToastContainer toasts={toasts} onDismiss={dismiss} />
    </>
  )
}
