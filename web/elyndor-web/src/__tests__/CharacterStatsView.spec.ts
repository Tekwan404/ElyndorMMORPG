import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'

import CharacterStatsView from '@/game/character/views/CharacterStatsView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('CharacterStatsView', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('shows authoritative stats and explains their sources on click', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot()

    const wrapper = mount(CharacterStatsView, { attachTo: document.body })

    expect(wrapper.get('[data-stat="agility"]').text()).toContain('Основная характеристика класса')
    expect(wrapper.get('[data-stat="agility"]').text()).toContain('9')

    await wrapper.get('[data-stat="agility"]').trigger('click')
    expect(document.body.textContent).toContain('Из чего складывается')
    expect(document.body.textContent).toContain('База класса')
    expect(document.body.textContent).toContain('Экипировка')
    wrapper.unmount()
  })

  it('shows block stats and server-calculated effective defense percentages', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot()

    const wrapper = mount(CharacterStatsView, { attachTo: document.body })

    expect(wrapper.get('[data-stat="blockChance"]').text()).toContain('18%')
    expect(wrapper.get('[data-stat="blockValueMin"]').text()).toContain('24')
    expect(wrapper.get('[data-stat="blockValueMax"]').text()).toContain('36')
    expect(wrapper.get('[data-stat="armor"]').text()).toContain('15.97% снижения физического урона')
    expect(wrapper.get('[data-stat="magicResistance"]').text()).toContain('10.71% снижения магического урона')

    await wrapper.get('[data-stat="armor"]').trigger('click')
    expect(document.body.textContent).toContain('Эффективное значение')
    expect(document.body.textContent).toContain('15.97%')
    expect(document.body.textContent).toContain('до учёта пробивания атакующего')
    expect(document.body.textContent).toContain('Вклад Выносливости')
    expect(document.body.textContent).toContain('Бонус экипировки')
    wrapper.unmount()
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
      gold: 0,
      inventory: {
        items: [],
        equipped: {
          weapon: null,
          head: null,
          chest: null,
          legs: null,
          boots: null,
          accessory: null,
        },
      },
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
      statBreakdown: {
        strength: { finalValue: 5, contributions: [{ source: 'CLASS_BASE' as const, value: 5 }] },
        agility: { finalValue: 9, contributions: [{ source: 'CLASS_BASE' as const, value: 8 }, { source: 'EQUIPMENT' as const, value: 1 }] },
        intellect: { finalValue: 5, contributions: [{ source: 'CLASS_BASE' as const, value: 5 }] },
        stamina: { finalValue: 7, contributions: [{ source: 'CLASS_BASE' as const, value: 7 }] },
        maxHp: { finalValue: 120, contributions: [{ source: 'FORMULA_BASE' as const, value: 50 }, { source: 'STAMINA' as const, value: 70 }] },
        attackPower: { finalValue: 19, contributions: [{ source: 'STRENGTH' as const, value: 10 }, { source: 'AGILITY' as const, value: 9 }] },
        spellPower: { finalValue: 10, contributions: [{ source: 'INTELLECT' as const, value: 10 }] },
        criticalChance: { finalValue: 7.25, contributions: [{ source: 'FORMULA_BASE' as const, value: 5 }, { source: 'AGILITY' as const, value: 2.25 }] },
        criticalDamage: { finalValue: 100, contributions: [{ source: 'FORMULA_BASE' as const, value: 100 }] },
        accuracy: { finalValue: 95, contributions: [{ source: 'FORMULA_BASE' as const, value: 95 }] },
        armorPenetration: { finalValue: 0, contributions: [] },
        magicPenetration: { finalValue: 0, contributions: [{ source: 'FORMULA_BASE' as const, value: 0 }] },
        attackSpeed: { finalValue: 1, contributions: [{ source: 'FORMULA_BASE' as const, value: 1 }] },
        armor: {
          finalValue: 19,
          contributions: [
            { source: 'STAMINA' as const, value: 14 },
            { source: 'STRENGTH' as const, value: 4 },
            { source: 'EQUIPMENT_BONUS' as const, value: 1 },
          ],
        },
        armorDamageReductionPercent: { finalValue: 15.9663865546, contributions: [] },
        magicResistance: { finalValue: 12, contributions: [{ source: 'STAMINA' as const, value: 7 }, { source: 'INTELLECT' as const, value: 5 }] },
        magicDamageReductionPercent: { finalValue: 10.7142857143, contributions: [] },
        dodge: { finalValue: 1.8, contributions: [{ source: 'AGILITY' as const, value: 1.8 }] },
        blockChance: { finalValue: 18, contributions: [{ source: 'EQUIPMENT_BONUS' as const, value: 18 }] },
        blockValueMin: { finalValue: 24, contributions: [{ source: 'EQUIPMENT_BONUS' as const, value: 24 }] },
        blockValueMax: { finalValue: 36, contributions: [{ source: 'EQUIPMENT_BONUS' as const, value: 36 }] },
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
