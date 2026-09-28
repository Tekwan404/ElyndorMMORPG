import { describe, expect, it } from 'vitest'
import { formatGoldInput, parseGoldInput } from '@/game/economy/commerce'

describe('commerce money entry', () => {
  it('converts Gold decimals to exact bronze', () => {
    expect(parseGoldInput('1')).toBe(10_000)
    expect(parseGoldInput('1.2345')).toBe(12_345)
    expect(formatGoldInput('12345')).toBe('1.2345')
    expect(formatGoldInput(0)).toBe('0')
  })

  it('rejects negative, malformed and unsafe prices', () => {
    for (const value of ['-1', '1.23456', '1e5', '0x10', '900719925475']) {
      expect(() => parseGoldInput(value)).toThrow(RangeError)
    }
  })
})
