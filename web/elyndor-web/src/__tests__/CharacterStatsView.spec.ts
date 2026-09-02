import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'

import CharacterStatsView from '@/game/character/views/CharacterStatsView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('CharacterStatsView', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('renders authoritative vitals and highlights the class primary attribute', () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot()

    const wrapper = mount(CharacterStatsView)

    expect(wrapper.get('[role="progressbar"][aria-label="Здоровье"]')).toBeTruthy()
    expect(wrapper.get('[role="progressbar"][aria-label="Фокус"]')).toBeTruthy()
    expect(wrapper.get('[data-stat="agility"]').text()).toContain('основной')
    expect(wrapper.get('[data-stat="agility"]').text()).toContain('9')
  })
})

function snapshot() {
  return {
    accountId: crypto.randomUUID(),
    character: {
      id: crypto.randomUUID(),
      name: 'Arthas',
      raceId: 'HUMAN' as const,
      genderId: 'MALE' as const,
      classId: 'ARCHER' as const,
      level: 1,
      experience: 0,
      xpToNextLevel: 100,
      inventory: { items: [], equipped: { weapon: null, head: null, chest: null } },
      primaryAttribute: 'AGILITY' as const,
      classProfileVersion: '0.2.0',
      knownAbilityIds: [],
      knownAbilities: [],
      stats: {
        strength: 5,
        agility: 9,
        intellect: 5,
        stamina: 7,
        maxHp: 120,
        attackPower: 19,
        spellPower: 10,
        criticalChance: 7.25,
        criticalDamage: 100,
        accuracy: 95,
        armorPenetration: 0,
        magicPenetration: 0,
        attackSpeed: 1,
        armor: 19,
        magicResistance: 12,
        dodge: 1.8,
      },
      vitals: {
        currentHp: 120,
        maxHp: 120,
        resourceType: 'FOCUS' as const,
        currentResource: 84,
        maxResource: 100,
        checkpointedAtUtc: '2026-08-30T00:00:00Z',
      },
    },
    world: null,
    contentVersion: '0.1.0',
    balanceVersion: '0.1.0',
    serverTimeUtc: '2026-08-30T00:00:00Z',
  }
}
