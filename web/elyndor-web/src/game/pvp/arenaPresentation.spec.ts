import { describe, expect, it } from 'vitest'

import { arenaErrorMessage, arenaResultLabel } from './arenaPresentation'

describe('arenaPresentation', () => {
  it('maps known error codes and falls back for unknown ones', () => {
    expect(arenaErrorMessage('arena_dungeon_active')).toContain('подземель')
    expect(arenaErrorMessage('something_new')).toBe('Не удалось выполнить действие на арене.')
    expect(arenaErrorMessage(null)).toBe('')
  })

  it('labels every terminal outcome', () => {
    expect(arenaResultLabel('Victory', 'WinnerA')).toBe('Победа')
    expect(arenaResultLabel('Defeat', 'WinnerB')).toBe('Поражение')
    expect(arenaResultLabel('Cancelled', 'Draw')).toBe('Ничья')
    expect(arenaResultLabel('Cancelled', 'Cancelled')).toBe('Бой отменён')
    expect(arenaResultLabel('Active', 'Active')).toBe('')
  })
})
