import { describe, expect, it } from 'vitest'

import {
  canonicalItemIconIdFromModulePath,
  isCanonicalItemIconId,
  resolveItemArtUrl,
} from './itemArt'

describe('itemArt', () => {
  it('derives canonical ids from asset-root-relative module paths', () => {
    expect(canonicalItemIconIdFromModulePath('./items/sets/guardian/helmet.webp'))
      .toBe('sets/guardian/helmet')
  })

  it('rejects paths that are not canonical item icon ids', () => {
    expect(isCanonicalItemIconId('../helmet')).toBe(false)
    expect(isCanonicalItemIconId('/helmet')).toBe(false)
    expect(isCanonicalItemIconId('sets\\helmet')).toBe(false)
    expect(resolveItemArtUrl('../helmet')).toBeUndefined()
  })
})
