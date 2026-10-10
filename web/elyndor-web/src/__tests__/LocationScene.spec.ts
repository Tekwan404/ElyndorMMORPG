import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { apiClient } from '@/api/apiClient'
import LocationScene from '@/game/world/components/LocationScene.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'

const enemy = {
  id: 'ENEMY_WOLF',
  kind: 'Enemy',
  displayName: 'Лесной волк',
  description: 'Хищник',
  x: 18,
  y: 30,
  isRare: false,
  questId: null,
  availableUntilUtc: null,
  resident: {
    monsterId: 'WOLF',
    displayName: 'Лесной волк',
    level: 3,
    rank: 'Normal',
    artId: 'wolf',
    description: 'Хищник',
    xpReward: 10,
    goldRewardMin: 1,
    goldRewardMax: 2,
    loot: [
      {
        itemId: 'HIDE',
        name: 'Волчья шкура',
        type: 'Material',
        rarity: 'Common',
        requiredLevel: 1,
        description: '',
        iconId: null,
      },
    ],
  },
}
const scene = {
  locationId: 'FOREST',
  contentVersion: 'test',
  state: 'Calm',
  serverTimeUtc: '2026-10-11T00:00:00Z',
  nextChangeAtUtc: null,
  objects: [enemy],
}
const props = {
  locationId: 'FOREST',
  contentVersion: 'test',
  title: 'Лес',
  background: '',
  levelLabel: 'Ур. 1–5',
  canAttack: true,
}

describe('LocationScene', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  it('opens the selected enemy and starts only the server-issued encounter', async () => {
    const encounter = { encounterId: 'opaque-token', monsterId: 'WOLF' }
    const request = vi
      .spyOn(apiClient, 'request')
      .mockResolvedValueOnce(scene)
      .mockResolvedValueOnce(encounter)
    const combat = useCombatSessionStore()
    const start = vi.spyOn(combat, 'startCombat').mockResolvedValue(true)
    const wrapper = mount(LocationScene, { props })
    await flushPromises()
    expect(wrapper.find('[data-scene-panel]').exists()).toBe(false)
    await wrapper.get('[data-scene-marker="ENEMY_WOLF"]').trigger('click')
    expect(wrapper.get('[data-scene-panel]').text()).toContain('Волчья шкура')
    await wrapper.get('[data-scene-attack]').trigger('click')
    await flushPromises()
    expect(request).toHaveBeenCalledWith(
      '/api/v1/world/select-encounter',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ locationId: 'FOREST', monsterId: 'WOLF' }),
      }),
    )
    expect(start).toHaveBeenCalledWith(encounter)
    wrapper.unmount()
  })

  it('does not attack when the server rejects selection and keeps an actionable error', async () => {
    vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce(scene)
      .mockRejectedValueOnce(new Error('expired'))
      .mockResolvedValue(scene)
    const start = vi.spyOn(useCombatSessionStore(), 'startCombat').mockResolvedValue(true)
    const wrapper = mount(LocationScene, { props })
    await flushPromises()
    await wrapper.get('[data-scene-marker]').trigger('click')
    await wrapper.get('[data-scene-attack]').trigger('click')
    await flushPromises()
    expect(start).not.toHaveBeenCalled()
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось')
    wrapper.unmount()
  })

  it('rejects a late scene response after the location changes', async () => {
    let resolveOld!: (value: typeof scene) => void
    vi.spyOn(apiClient, 'request')
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            resolveOld = resolve
          }),
      )
      .mockResolvedValue({ ...scene, locationId: 'NEW', objects: [] })
    const wrapper = mount(LocationScene, { props })
    await wrapper.setProps({ locationId: 'NEW' })
    await flushPromises()
    resolveOld(scene)
    await flushPromises()
    expect(wrapper.find('[data-scene-marker]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Поблизости нет')
    wrapper.unmount()
  })

  it('paginates markers and disables attacks for party members or an active mutation', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue({
      ...scene,
      objects: Array.from({ length: 10 }, (_, index) => ({ ...enemy, id: `ENEMY_${index}` })),
    })
    const wrapper = mount(LocationScene, { props: { ...props, canAttack: false } })
    await flushPromises()
    expect(wrapper.findAll('[data-scene-marker]')).toHaveLength(8)
    await wrapper.get('[data-scene-marker="ENEMY_0"]').trigger('click')
    expect(wrapper.get('[data-scene-attack]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-scene-next]').trigger('click')
    expect(wrapper.findAll('[data-scene-marker]')).toHaveLength(2)
    await wrapper.setProps({ canAttack: true })
    useGameSessionStore().mutationPending = true
    await wrapper.get('[data-scene-marker="ENEMY_8"]').trigger('click')
    expect(wrapper.get('[data-scene-attack]').attributes('disabled')).toBeDefined()
    wrapper.unmount()
  })
})
