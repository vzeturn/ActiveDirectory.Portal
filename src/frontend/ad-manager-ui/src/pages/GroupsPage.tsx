import { useCallback, useMemo, useState } from 'react'
import { Search } from 'lucide-react'
import type { AdScope } from '../types'
import * as userService from '../services/user.service'
import { useGroupDetail, useGroupsSearch } from '../hooks/queries/useGroupsQuery'
import {
  useAddGroupMembers,
  useCreateGroup,
  useDeleteGroup,
  useRemoveGroupMember,
  useUpdateGroupDescription
} from '../hooks/queries/useGroupMutations'
import { Input } from '../components/ui/Input/Input'
import { Select } from '../components/ui/Select/Select'
import { Button } from '../components/ui/Button/Button'
import { Alert } from '../components/ui/Alert/Alert'
import { Panel } from '../components/ui/Panel/Panel'
import { Modal } from '../components/ui/Modal/Modal'
import { EmptyState } from '../components/ui/EmptyState/EmptyState'
import { MembershipPicker } from '../components/ui/MembershipPicker/MembershipPicker'
import { Tooltip } from '../components/ui/Tooltip/Tooltip'
import { useToast } from '../components/ui/Toast/useToast'
import { ToastContainer } from '../components/ui/Toast/Toast'
import { DataTable } from '../components/ui/Table/DataTable'
import type { DataTableColumn } from '../components/ui/Table/DataTable'
import type { AdGroup } from '../types'
import { useAuthStore } from '../stores/authStore'
import styles from './Page.module.css'

const GROUP_NAME_PATTERN = /^[^\s"/\\[\]:;|=,+*?<>@]{1,64}$/
const VIEW_ONLY_TITLE = 'View-only access'

export function GroupsPage() {
  const { user } = useAuthStore()
  const canWrite = user?.isAdmin === true
  const [query, setQuery] = useState('')
  const [scope, setScope] = useState<AdScope>('ou')
  const [searchArgs, setSearchArgs] = useState<{ query: string; scope: AdScope } | null>(null)
  const [selectedName, setSelectedName] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [createModalOpen, setCreateModalOpen] = useState(false)
  const [newGroupName, setNewGroupName] = useState('')
  const [newGroupDescription, setNewGroupDescription] = useState('')
  const [editingDescription, setEditingDescription] = useState(false)
  const [descriptionDraft, setDescriptionDraft] = useState('')
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false)
  const [addMembersModalOpen, setAddMembersModalOpen] = useState(false)
  const { toasts, show, dismiss } = useToast()
  const searchUsers = useCallback((q: string) => userService.searchUserOptions(q, scope), [scope])

  const {
    data: results = [],
    isLoading: loading,
    isError: searchIsError,
    error: searchError
  } = useGroupsSearch(searchArgs?.query ?? '', 100, searchArgs?.scope ?? scope, { enabled: !!searchArgs })
  const searched = !!searchArgs

  const {
    data: selected,
    isLoading: membersLoading
  } = useGroupDetail(selectedName ?? undefined, scope, { enabled: !!selectedName })
  const memberUsernames = useMemo(() => selected?.members.map((m) => m.username) ?? [], [selected])

  const createMutation = useCreateGroup()
  const deleteMutation = useDeleteGroup()
  const updateDescriptionMutation = useUpdateGroupDescription()
  const addMembersMutation = useAddGroupMembers()
  const removeMemberMutation = useRemoveGroupMember()

  const displayedError = error ?? (searchIsError ? searchError?.message ?? 'Search failed.' : null)

  function runSearch() {
    setError(null)
    setSearchArgs({ query, scope })
  }

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    runSearch()
  }

  function selectGroup(group: AdGroup) {
    setError(null)
    setSelectedName(group.name)
  }

  // The picker surfaces per-row success/failure itself, so these just rethrow.
  const addGroupMember = (opt: { value: string }) =>
    addMembersMutation.mutateAsync({ groupName: selectedName!, usernames: [opt.value], scope })
  const removeGroupMember = (opt: { value: string }) =>
    removeMemberMutation.mutateAsync({ groupName: selectedName!, username: opt.value, scope })

  async function handleRemoveMember(username: string) {
    if (!selectedName) return
    setError(null)
    try {
      await removeMemberMutation.mutateAsync({ groupName: selectedName, username, scope })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to remove member.')
    }
  }

  const columns: DataTableColumn<AdGroup>[] = [
    { key: 'name', header: 'Name', render: (g) => g.name },
    { key: 'members', header: 'Members', render: (g) => <span className="tabular-nums">{g.memberCount}</span> },
    { key: 'description', header: 'Description', render: (g) => g.description ?? '-' }
  ]

  return (
    <>
      <div className={styles.header}>
        <h1 className={styles.title}>Groups</h1>
        <Button onClick={() => setCreateModalOpen(true)} disabled={!canWrite} title={!canWrite ? VIEW_ONLY_TITLE : undefined}>
          New group
        </Button>
      </div>

      {displayedError && !selectedName && <Alert variant="error">{displayedError}</Alert>}

      <form onSubmit={handleSearch} className={styles.searchBar}>
        <div className={styles.searchInput}>
          <Input
            label="Search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search groups..."
            required
          />
        </div>
        <div className={styles.scopeSelect}>
          <Select
            label="Scope"
            value={scope}
            onValueChange={(v) => setScope(v as AdScope)}
            options={[
              { value: 'ou', label: 'OU scope' },
              { value: 'domain', label: 'Domain scope' }
            ]}
          />
        </div>
        <Button type="submit" loading={loading} icon={!loading ? <Search size={16} aria-hidden="true" /> : undefined}>
          {loading ? 'Searching...' : 'Search'}
        </Button>
      </form>

      <DataTable
        columns={columns}
        rows={results}
        rowKey={(g) => g.name}
        onRowClick={selectGroup}
        loading={loading}
        emptyTitle={searched ? 'No groups found' : 'Search for groups'}
        emptyDescription={searched ? 'Try a different search term or scope.' : 'Enter a group name above to get started.'}
        rowActions={(g) => [
          {
            label: 'Delete group',
            danger: true,
            disabled: !canWrite,
            onSelect: () => {
              selectGroup(g)
              setDeleteConfirmOpen(true)
            }
          }
        ]}
      />

      <Panel open={!!selectedName} onClose={() => setSelectedName(null)} title={selected?.name ?? 'Group detail'}>
        {selected && (
          <div className={styles.detailSection}>
            {error && <Alert variant="error">{error}</Alert>}

            <dl className={styles.dl}>
              <dt className={styles.dt}>Members</dt>
              <dd className={styles.dd}>{selected.memberCount}</dd>
              <dt className={styles.dt}>Description</dt>
              <dd className={styles.dd}>
                {editingDescription ? (
                  <div>
                    <Input value={descriptionDraft} onChange={(e) => setDescriptionDraft(e.target.value)} />
                    <Button
                      size="sm"
                      onClick={async () => {
                        try {
                          await updateDescriptionMutation.mutateAsync({
                            name: selected.name,
                            description: descriptionDraft || undefined,
                            scope
                          })
                          show('Description updated.', 'success')
                          setEditingDescription(false)
                        } catch (err) {
                          setError(err instanceof Error ? err.message : 'Failed to update description.')
                        }
                      }}
                      disabled={!canWrite}
                      title={!canWrite ? VIEW_ONLY_TITLE : undefined}
                    >
                      Save
                    </Button>
                    <Button variant="secondary" size="sm" onClick={() => setEditingDescription(false)}>
                      Cancel
                    </Button>
                  </div>
                ) : (
                  <div className={styles.detailStatusRow}>
                    <span>{selected.description ?? '(no description)'}</span>
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => {
                        setDescriptionDraft(selected.description ?? '')
                        setEditingDescription(true)
                      }}
                      disabled={!canWrite}
                      title={!canWrite ? VIEW_ONLY_TITLE : undefined}
                    >
                      Edit
                    </Button>
                  </div>
                )}
              </dd>
            </dl>

            <div>
              <div className={styles.detailStatusRow}>
                <div className={styles.subheading}>Members</div>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => setAddMembersModalOpen(true)}
                  disabled={!canWrite}
                  title={!canWrite ? VIEW_ONLY_TITLE : undefined}
                >
                  Manage members…
                </Button>
              </div>
              {membersLoading ? (
                <div className={styles.memberListLoading}>
                  {Array.from({ length: 8 }).map((_, i) => (
                    <div key={i} className={styles.memberSkeleton} />
                  ))}
                </div>
              ) : selected.members.length > 0 ? (
                <ul className={styles.memberList}>
                  {selected.members.map((m) => (
                    <li key={m.username} className={styles.memberItem}>
                      <div className={styles.memberInfo}>
                        <span className={styles.memberName}>{m.displayName ?? m.username}</span>
                        <span className={styles.memberMeta}>{m.email ?? m.username}</span>
                      </div>
                      <Tooltip label={`Remove ${m.displayName ?? m.username}`}>
                        <button
                          type="button"
                          onClick={() => handleRemoveMember(m.username)}
                          aria-label={`Remove ${m.displayName ?? m.username}`}
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
                <EmptyState title="No members" description="This group has no members yet." />
              )}
            </div>

            <Button
              variant="danger"
              onClick={() => setDeleteConfirmOpen(true)}
              disabled={!canWrite}
              title={!canWrite ? VIEW_ONLY_TITLE : undefined}
            >
              Delete group
            </Button>
          </div>
        )}
      </Panel>

      <Modal
        open={createModalOpen}
        title="New group"
        onClose={() => setCreateModalOpen(false)}
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateModalOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={!GROUP_NAME_PATTERN.test(newGroupName) || !canWrite}
              title={!canWrite ? VIEW_ONLY_TITLE : undefined}
              onClick={async () => {
                try {
                  await createMutation.mutateAsync({ name: newGroupName, description: newGroupDescription || undefined, scope })
                  show(`Group '${newGroupName}' created.`, 'success')
                  setCreateModalOpen(false)
                  setNewGroupName('')
                  setNewGroupDescription('')
                  runSearch()
                } catch (err) {
                  setError(err instanceof Error ? err.message : 'Failed to create group.')
                }
              }}
            >
              Create
            </Button>
          </>
        }
      >
        <Input
          value={newGroupName}
          onChange={(e) => setNewGroupName(e.target.value)}
          placeholder="Group name"
          error={
            newGroupName.length > 0 && !GROUP_NAME_PATTERN.test(newGroupName)
              ? 'Name must be 1-64 characters, no spaces or "/\\[]:;|=,+*?<>@'
              : undefined
          }
        />
        <Input
          value={newGroupDescription}
          onChange={(e) => setNewGroupDescription(e.target.value)}
          placeholder="Description (optional)"
        />
      </Modal>

      <Modal
        open={addMembersModalOpen && !!selectedName}
        title={`Manage members · ${selected?.name ?? ''}`}
        onClose={() => setAddMembersModalOpen(false)}
        footer={
          <Button variant="secondary" onClick={() => setAddMembersModalOpen(false)}>
            Done
          </Button>
        }
      >
        {selectedName && (
          <MembershipPicker
            members={memberUsernames}
            onSearch={searchUsers}
            onAdd={addGroupMember}
            onRemove={removeGroupMember}
            noun="user"
            disabled={!canWrite}
            disabledReason={VIEW_ONLY_TITLE}
          />
        )}
      </Modal>

      <Modal
        open={deleteConfirmOpen}
        title="Delete group"
        onClose={() => setDeleteConfirmOpen(false)}
        footer={
          <>
            <Button variant="secondary" onClick={() => setDeleteConfirmOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="danger"
              disabled={!canWrite}
              title={!canWrite ? VIEW_ONLY_TITLE : undefined}
              onClick={async () => {
                if (!selectedName) return
                try {
                  await deleteMutation.mutateAsync({ name: selectedName, scope })
                  show(`Group '${selectedName}' deleted.`, 'success')
                  setDeleteConfirmOpen(false)
                  setSelectedName(null)
                  runSearch()
                } catch (err) {
                  setError(err instanceof Error ? err.message : 'Failed to delete group.')
                }
              }}
            >
              Delete
            </Button>
          </>
        }
      >
        <p>Delete group '{selectedName}'? This cannot be undone.</p>
      </Modal>

      <ToastContainer toasts={toasts} onDismiss={dismiss} />
    </>
  )
}
