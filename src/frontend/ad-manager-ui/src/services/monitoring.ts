import type { Breadcrumb, ErrorEvent } from '@sentry/react'

/**
 * Browser error reporting via Sentry. The DSN comes from the server (GET /api/client-config),
 * so nothing is baked into the bundle; with no DSN the SDK is never downloaded.
 * The portal handles real directory accounts, so personal data is stripped before sending.
 */

interface ClientConfig {
  sentryDsn: string | null
  sentryEnvironment: string
  sentryTracesSampleRate: number
}

const REDACTED = '[redacted]'

const patterns: Array<[RegExp, string | ((...m: string[]) => string)]> = [
  [
    /(\/api\/(?:users|groups|saved-searches))\/[^/\s?#"']+(?:\/(members|groups)\/[^/\s?#"']+)?/gi,
    (_m, base: string, sub?: string) => (sub ? `${base}/${REDACTED}/${sub}/${REDACTED}` : `${base}/${REDACTED}`),
  ],
  [/(\/(?:users|groups))\/[^/\s?#"']+/gi, `$1/${REDACTED}`],
  [/\b(CN|OU|DC|UID|O|L|ST|C)=[^,;"'\r\n]+/gi, `$1=${REDACTED}`],
  [/[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/g, REDACTED],
  [/\b[A-Za-z0-9-]{1,15}\\[A-Za-z0-9._$-]+/g, REDACTED],
  [/'[^']*'|"[^"]*"/g, (m: string) => `${m[0]}${REDACTED}${m[0]}`],
]

export function redact(text: string): string
export function redact(text: string | undefined): string | undefined
export function redact(text: string | undefined): string | undefined {
  if (!text) return text
  return patterns.reduce<string>(
    (acc, [re, replacement]) => acc.replace(re, replacement as never),
    text,
  )
}

function redactUrl(url: string | undefined): string | undefined {
  if (!url) return url
  try {
    const parsed = new URL(url, window.location.origin)
    return `${parsed.origin}${redact(parsed.pathname)}`
  } catch {
    return redact(url.split('?')[0])
  }
}

export function scrubEvent(event: ErrorEvent): ErrorEvent {
  delete event.user
  if (event.request) {
    event.request = { url: redactUrl(event.request.url) }
  }
  if (event.message) event.message = redact(event.message)
  for (const ex of event.exception?.values ?? []) {
    ex.value = redact(ex.value)
  }
  delete event.extra
  return event
}

export function scrubBreadcrumb(breadcrumb: Breadcrumb): Breadcrumb | null {
  const url = typeof breadcrumb.data?.url === 'string' ? redactUrl(breadcrumb.data.url) : undefined
  return {
    ...breadcrumb,
    message: redact(breadcrumb.message),
    data: url ? { url, method: breadcrumb.data?.method, status_code: breadcrumb.data?.status_code } : undefined,
  }
}

export async function initMonitoring(): Promise<void> {
  let config: ClientConfig
  try {
    const response = await fetch('/api/client-config', { credentials: 'same-origin' })
    if (!response.ok) {
      console.warn(`Error reporting disabled: /api/client-config returned HTTP ${response.status}.`)
      return
    }
    config = ((await response.json()) as { data: ClientConfig }).data
  } catch (error) {
    console.warn('Error reporting disabled: could not load /api/client-config.', error)
    return
  }
  if (!config?.sentryDsn) return

  const Sentry = await import('@sentry/react')
  Sentry.init({
    dsn: config.sentryDsn,
    environment: config.sentryEnvironment,
    sendDefaultPii: false,
    tracesSampleRate: config.sentryTracesSampleRate,
    integrations: [Sentry.browserTracingIntegration()],
    beforeSend: scrubEvent,
    beforeSendTransaction: (transaction) => {
      delete transaction.user
      if (transaction.request) transaction.request = { url: redactUrl(transaction.request.url) }
      transaction.transaction = redact(transaction.transaction)
      for (const span of transaction.spans ?? []) span.description = redact(span.description)
      return transaction
    },
    beforeBreadcrumb: scrubBreadcrumb,
  })
}
