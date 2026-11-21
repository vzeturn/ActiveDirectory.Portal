export interface AdUser {
  username: string
  displayName?: string
  firstName?: string
  lastName?: string
  email?: string
  phone?: string
  mobile?: string
  title?: string
  department?: string
  organizationalUnit?: string
  company?: string
  manager?: string
  enabled: boolean
  isLockedOut: boolean
  distinguishedName?: string
  created?: string
  modified?: string
  lastLogon?: string
  passwordLastSet?: string
  description?: string
  groups: string[]
}

export interface AdGroupMember {
  username: string
  displayName?: string
  email?: string
}

export interface AdGroup {
  name: string
  description?: string
  distinguishedName?: string
  memberCount: number
  created?: string
  modified?: string
  members: AdGroupMember[]
  parentGroups: string[]
}

export interface AuditEntry {
  timestamp: string
  actor: string
  operation: string
  target: string
  result: boolean
  details?: string
}

export interface UserInfo {
  username: string
  displayName: string
  groups: string[]
  isAdmin: boolean
  isViewer: boolean
  expiresAt: string
}

export interface ApiResponse<T> {
  success: boolean
  data?: T
  error?: string
}

export interface ApiMessageResponse {
  success: boolean
  message?: string
  error?: string
}

export type AdScope = 'ou' | 'domain'

export interface BulkOpItemResult {
  username: string
  success: boolean
  error?: string
}

export interface DashboardSummary {
  lockedOutCount: number
  expiringSoonCount: number
  recentActivity: AuditEntry[]
}

export interface HealthDetail {
  adReachable: boolean
  secretsLoaded: boolean
  auditDbWritable: boolean
  alertHookConfigured: boolean
}

export interface SavedSearch {
  id: string
  owner: string
  name: string
  filterJson: string
  createdAt: string
}

export interface SavedUserFilters {
  query: string
  scope: AdScope
  ou?: string
  enabledOnly?: boolean
  lockedOnly?: boolean
  memberOf?: string
}
