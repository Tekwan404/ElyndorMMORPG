import { describe, expect, it } from 'vitest'
import { evaluateEntityCompleteness } from '@/admin/adminCompleteness'

describe('admin entity completeness', () => {
  it('marks a monster ready only after loot, location and art are connected', () => {
    const monster = {
      id: 'WOLF',
      name: 'Wolf',
      displayName: 'Волк',
      description: 'Лесной хищник',
      level: 2,
      maxHp: 120,
      aiProfileId: 'WOLF_AI',
      lootTableId: 'WOLF_LOOT',
      abilityIds: ['BITE'],
      artId: 'wolf',
    }
    const result = evaluateEntityCompleteness(
      {
        abilities: [{ id: 'BITE' }],
        monsterAiProfiles: [{ id: 'WOLF_AI' }],
        lootTables: [{ id: 'WOLF_LOOT', entries: [{ itemId: 'FANG' }] }],
        locations: [{ id: 'FOREST', encounters: [{ monsterId: 'WOLF', weight: 1 }] }],
      },
      'monsters',
      monster,
    )

    expect(result.ready).toBe(true)
    expect(result.completed).toBe(result.total)
  })

  it('explains incomplete monster content without blocking draft editing', () => {
    const result = evaluateEntityCompleteness(
      { monsterAiProfiles: [], lootTables: [], locations: [], abilities: [] },
      'monsters',
      {
        id: 'WOLF',
        name: 'Wolf',
        description: '',
        level: 1,
        maxHp: 100,
        aiProfileId: 'WOLF_AI',
        lootTableId: null,
        abilityIds: [],
        artId: null,
      },
    )

    expect(result.ready).toBe(false)
    expect(result.checks.find(check => check.key === 'loot')?.complete).toBe(false)
    expect(result.checks.find(check => check.key === 'location')?.complete).toBe(false)
    expect(result.checks.find(check => check.key === 'art')?.complete).toBe(false)
  })
})
