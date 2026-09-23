import { describe, expect, it, vi } from 'vitest'

import { takePartyInviteId } from '@/game/party/partyInviteDeepLink'

describe('party invite deep link', () => {
  it('takes a valid invite id and removes it from the visible url', () => {
    const inviteId = '55555555-5555-5555-5555-555555555599'
    const replaceState = vi.fn()
    const location = {
      href: `https://elyndor.su/world?partyInvite=${inviteId}&source=telegram#party`,
    } as Location
    const history = {
      state: { navigation: 1 },
      replaceState,
    } as unknown as History

    expect(takePartyInviteId(location, history)).toBe(inviteId)
    expect(replaceState).toHaveBeenCalledWith(
      history.state,
      '',
      '/world?source=telegram#party',
    )
  })

  it('ignores malformed invite ids without changing the url', () => {
    const replaceState = vi.fn()
    const location = {
      href: 'https://elyndor.su/world?partyInvite=not-a-guid',
    } as Location
    const history = {
      state: null,
      replaceState,
    } as unknown as History

    expect(takePartyInviteId(location, history)).toBeNull()
    expect(replaceState).not.toHaveBeenCalled()
  })
})
