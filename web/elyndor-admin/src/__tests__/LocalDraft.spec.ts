import { beforeEach, describe, expect, it } from 'vitest'

import {
  clearLocalContentDraft,
  loadLocalContentDraft,
  readLocalContentDraft,
  saveLocalContentDraft,
} from '@/admin/localDraft'

describe('Admin V2 local content draft', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('restores only against the exact live payload sha', () => {
    saveLocalContentDraft('LIVE_A', '{"monsters":[]}')

    expect(loadLocalContentDraft('LIVE_A')).toMatchObject({
      basePayloadSha256: 'LIVE_A',
      payloadJson: '{"monsters":[]}',
    })
    expect(loadLocalContentDraft('LIVE_B')).toBeNull()
  })

  it('does not delete a stale draft when clearing another live base', () => {
    saveLocalContentDraft('LIVE_A', '{"items":[1]}')

    clearLocalContentDraft('LIVE_B')

    expect(readLocalContentDraft()).toMatchObject({
      basePayloadSha256: 'LIVE_A',
    })
  })

  it('clears the matching draft after reset or publish', () => {
    saveLocalContentDraft('LIVE_A', '{"items":[1]}')

    clearLocalContentDraft('LIVE_A')

    expect(readLocalContentDraft()).toBeNull()
  })

  it('ignores malformed storage without throwing', () => {
    localStorage.setItem('elyndor.admin.content-draft.v1', '{broken')

    expect(readLocalContentDraft()).toBeNull()
  })
})
