import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import AppShell from '@/app/AppShell.vue'
import { useGameSessionStore } from '@/stores/gameSession'

vi.mock('@/telegram/telegramWebApp', () => ({ initializeTelegramWebApp: vi.fn<() => void>() }))

describe('AppShell', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('presents authoritative vitals and switches between world and hero views', async () => {
    const store = useGameSessionStore()
    vi.spyOn(store, 'start').mockResolvedValue(undefined)
    store.state = 'world'
    store.snapshot = worldSnapshot()
    const wrapper = mount(AppShell)
    expect(wrapper.get('[role="progressbar"][aria-label="Health"]')).toBeTruthy()
    expect(wrapper.get('[role="progressbar"][aria-label="Focus"]')).toBeTruthy()
    expect(wrapper.get('main').text()).toContain('Starter Town')
    expect(wrapper.find('[data-nav="combat"]').exists()).toBe(false)
    await wrapper.get('[data-nav="hero"]').trigger('click')
    expect(wrapper.get('main').text()).toContain('Основные')
  })

  it('explains a failed connection and offers an explicit retry', async () => {
    const store = useGameSessionStore()
    vi.spyOn(store, 'start').mockResolvedValue(undefined)
    store.state = 'offline'
    store.errorCode = 'network_unavailable'
    const wrapper = mount(AppShell)
    expect(wrapper.get('[role="alert"]').text()).toContain('network_unavailable')
    await wrapper.get('[data-retry-session]').trigger('click')
    expect(store.start).toHaveBeenCalledTimes(2)
  })
})

function worldSnapshot() {
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
    world: {
      currentLocation: {
        id: 'STARTER_TOWN',
        displayName: 'Starter Town',
        dangerLevel: 'SAFE' as const,
        recommendedLevel: 1,
      },
      version: 1,
      outgoingTransitions: [],
    },
    contentVersion: '0.1.0',
    balanceVersion: '0.1.0',
    serverTimeUtc: '2026-08-30T00:00:00Z',
  }
}
