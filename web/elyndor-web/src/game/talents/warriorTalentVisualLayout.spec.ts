import { describe, expect, it } from 'vitest'

import {
  resolveWarriorTalentVisualPosition,
  WARRIOR_TALENT_VISUAL_COLUMN_COUNT,
} from './warriorTalentVisualLayout'

describe('warriorTalentVisualLayout', () => {
  it('spreads the Guardian branch across nine presentation rows', () => {
    expect(resolveWarriorTalentVisualPosition('GUARDIAN', 'G-1-1')).toEqual({ row: 1, column: 6 })
    expect(resolveWarriorTalentVisualPosition('GUARDIAN', 'G-6-1')).toEqual({ row: 7, column: 6 })
    expect(resolveWarriorTalentVisualPosition('GUARDIAN', 'G-6-2')).toEqual({ row: 8, column: 6 })
    expect(resolveWarriorTalentVisualPosition('GUARDIAN', 'G-6-5')).toEqual({ row: 9, column: 6 })
    expect(WARRIOR_TALENT_VISUAL_COLUMN_COUNT).toBe(12)
  })

  it('does not override branches that still use the legacy tier layout', () => {
    expect(resolveWarriorTalentVisualPosition('BERSERKER', 'B-1-1')).toBeNull()
    expect(resolveWarriorTalentVisualPosition('WARLORD', 'W-1-1')).toBeNull()
  })

  it('keeps unknown Guardian nodes on the safe fallback path', () => {
    expect(resolveWarriorTalentVisualPosition('GUARDIAN', 'G-FUTURE')).toBeNull()
  })
})
