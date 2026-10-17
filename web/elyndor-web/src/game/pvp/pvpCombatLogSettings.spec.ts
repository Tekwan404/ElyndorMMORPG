import { beforeEach, describe, expect, it, vi } from 'vitest'
import { isPvpCombatLogEnabled, setPvpCombatLogEnabled } from './pvpCombatLogSettings'

describe('PvP combat report preference', () => {
  beforeEach(() => localStorage.clear())

  it('defaults to off and remembers explicit opt-in', () => {
    expect(isPvpCombatLogEnabled()).toBe(false)
    setPvpCombatLogEnabled(true)
    expect(isPvpCombatLogEnabled()).toBe(true)
    setPvpCombatLogEnabled(false)
    expect(isPvpCombatLogEnabled()).toBe(false)
  })

  it('survives unavailable storage without failing the game', () => {
    const getter = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('blocked') })
    expect(isPvpCombatLogEnabled()).toBe(false)
    getter.mockRestore()
    const setter = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('blocked') })
    expect(() => setPvpCombatLogEnabled(true)).not.toThrow()
    setter.mockRestore()
  })
})
