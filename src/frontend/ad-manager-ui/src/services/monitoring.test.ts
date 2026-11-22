import { describe, expect, it } from 'vitest'
import type { ErrorEvent } from '@sentry/react'
import { redact, scrubBreadcrumb, scrubEvent } from './monitoring'

describe('redact', () => {
  it.each([
    ['PUT /api/users/jdoe/disable', 'PUT /api/users/[redacted]/disable'],
    ['/api/groups/Sales/members/jdoe', '/api/groups/[redacted]/members/[redacted]'],
    ['CN=John Doe,OU=Staff,DC=corp', 'CN=[redacted],OU=[redacted],DC=[redacted]'],
    ['jdoe@corp.local locked', '[redacted] locked'],
    ['CORP\\jdoe denied', '[redacted] denied'],
    ["User 'jdoe' not found", "User '[redacted]' not found"],
    ['Network Error', 'Network Error'],
  ])('%s', (input, expected) => {
    expect(redact(input)).toBe(expected)
  })
})

describe('scrubEvent', () => {
  it('drops user, query, headers and extra and redacts messages', () => {
    const event = {
      type: undefined,
      user: { username: 'jdoe', ip_address: '10.0.0.5' },
      request: {
        url: 'https://ad.example.com/api/users/jdoe/groups?q=jdoe',
        headers: { Cookie: 'ADManager.Session=abc' },
        query_string: 'q=jdoe',
      },
      message: "User 'jdoe' not found",
      exception: { values: [{ type: 'Error', value: 'Request failed for /api/users/jdoe' }] },
      extra: { username: 'jdoe' },
    } as unknown as ErrorEvent

    const scrubbed = scrubEvent(event)

    expect(scrubbed.user).toBeUndefined()
    expect(scrubbed.request).toEqual({ url: 'https://ad.example.com/api/users/[redacted]/groups' })
    expect(scrubbed.message).toBe("User '[redacted]' not found")
    expect(scrubbed.exception?.values?.[0].value).toBe('Request failed for /api/users/[redacted]')
    expect(scrubbed.extra).toBeUndefined()
  })
})

describe('scrubBreadcrumb', () => {
  it('keeps only redacted url, method and status of http breadcrumbs', () => {
    const scrubbed = scrubBreadcrumb({
      category: 'xhr',
      data: { url: '/api/users/jdoe?x=1', method: 'GET', status_code: 404, request_body_size: 10 },
    })

    expect(scrubbed?.data).toEqual({
      url: `${window.location.origin}/api/users/[redacted]`,
      method: 'GET',
      status_code: 404,
    })
  })
})
