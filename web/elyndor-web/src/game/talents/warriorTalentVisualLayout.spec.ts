import { describe, expect, it } from 'vitest'

import {
  resolveWarriorTalentVisualPosition,
  WARRIOR_TALENT_VISUAL_COLUMN_COUNT,
} from './warriorTalentVisualLayout'

const guardianTiers: ReadonlyArray<ReadonlyArray<string>> = [
  ['G-1-1', 'G-1-2', 'G-1-3', 'G-1-4', 'G-1-5'],
  ['G-2-1', 'G-2-2', 'G-2-4', 'G-2-5'],
  ['G-2-3', 'G-3-1', 'G-3-3', 'G-3-5'],
  ['G-3-2', 'G-3-4', 'G-3-6', 'G-4-1'],
  ['G-4-2', 'G-4-3', 'G-5-1', 'G-6-1'],
  ['G-4-4', 'G-4-5', 'G-5-3'],
  ['G-5-2', 'G-5-4', 'G-6-3'],
  ['G-5-5', 'G-6-2', 'G-6-4'],
  ['G-6-5'],
]

describe('warriorTalentVisualLayout', () => {
  it('shows all 31 Guardian talents on the same nine rows as their gameplay tiers', () => {
    const uniqueIds = new Set<string>()
    guardianTiers.forEach((ids, index) => {
      for (const id of ids) {
        const position = resolveWarriorTalentVisualPosition('GUARDIAN', id)
        expect(position?.row).toBe(index + 1)
        expect(position?.column).toBeGreaterThanOrEqual(1)
        expect(position?.column).toBeLessThanOrEqual(WARRIOR_TALENT_VISUAL_COLUMN_COUNT)
        expect(uniqueIds.has(id)).toBe(false)
        uniqueIds.add(id)
      }
    })
    expect(uniqueIds.size).toBe(31)
    expect(resolveWarriorTalentVisualPosition('GUARDIAN', 'G-6-1')).toEqual({ row: 5, column: 6 })
    expect(resolveWarriorTalentVisualPosition('GUARDIAN', 'G-6-2')).toEqual({ row: 8, column: 8 })
    expect(resolveWarriorTalentVisualPosition('GUARDIAN', 'G-6-5')).toEqual({ row: 9, column: 6 })
    expect(WARRIOR_TALENT_VISUAL_COLUMN_COUNT).toBe(12)
  })

  it('does not override Berserker or Warlord gameplay tier layouts', () => {
    expect(resolveWarriorTalentVisualPosition('BERSERKER', 'B-1-1')).toBeNull()
    expect(resolveWarriorTalentVisualPosition('WARLORD', 'W-1-1')).toBeNull()
  })

  it('keeps unknown Guardian nodes on the safe fallback path', () => {
    expect(resolveWarriorTalentVisualPosition('GUARDIAN', 'G-FUTURE')).toBeNull()
  })
})
