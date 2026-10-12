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
  it('gathers only the selected server resource intent and refreshes inventory without starting combat', async () => {
    const resource = {
      id: 'RESOURCE_MINT_1',
      kind: 'Resource',
      displayName: 'Лесная мята',
      description: 'Растение',
      x: 18,
      y: 30,
      resident: null,
      isRare: false,
      questId: null,
      availableUntilUtc: null,
      resource: {
        nodeId: 'MINT_1',
        itemId: 'FOREST_MINT',
        itemName: 'Лесная мята',
        iconId: null,
        minQuantity: 1,
        maxQuantity: 2,
        requiredSkill: 1,
        learnedSkill: 1,
        canGather: true,
        availableAtUtc: '1970-01-01T00:00:00Z',
      },
    }
    const request = vi
      .spyOn(apiClient, 'request')
      .mockImplementation(async (path) =>
        path.includes('/gather')
          ? { isSuccess: true, itemId: 'FOREST_MINT', quantity: 2, skillIncreased: true }
          : { ...scene, objects: [resource] },
      )
    const refresh = vi.spyOn(useGameSessionStore(), 'refreshSnapshot').mockResolvedValue()
    const combat = vi.spyOn(useCombatSessionStore(), 'startCombat')
    const wrapper = mount(LocationScene, { props: { ...props, canAttack: false } })
    await flushPromises()
    await wrapper.get('[data-scene-marker="RESOURCE_MINT_1"]').trigger('click')
    await wrapper.get('[data-scene-gather]').trigger('click')
    await flushPromises()
    const mutation = request.mock.calls.find((call) => call[0] === '/api/v1/professions/gather')!
    expect(JSON.parse(mutation[1]!.body as string)).toEqual({
      nodeId: 'MINT_1',
      mutationId: expect.any(String),
    })
    expect(refresh).toHaveBeenCalledOnce()
    expect(combat).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Лесная мята ×2')
    wrapper.unmount()
  })

  it('does not allow gathering an unavailable or unlearned resource', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue({
      ...scene,
      objects: [
        {
          id: 'RESOURCE_MINT_1',
          kind: 'Resource',
          displayName: 'Лесная мята',
          description: 'Растение',
          x: 18,
          y: 30,
          resident: null,
          questId: null,
          resource: {
            nodeId: 'MINT_1',
            itemId: 'FOREST_MINT',
            itemName: 'Лесная мята',
            iconId: null,
            minQuantity: 1,
            maxQuantity: 2,
            requiredSkill: 1,
            learnedSkill: 0,
            canGather: false,
            availableAtUtc: '1970-01-01T00:00:00Z',
          },
        },
      ],
    })
    const wrapper = mount(LocationScene, { props })
    await flushPromises()
    await wrapper.get('[data-scene-marker="RESOURCE_MINT_1"]').trigger('click')
    expect(wrapper.get('[data-scene-gather]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Изучите травничество')
    wrapper.unmount()
  })
  it('shows the shared elite respawn and blocks attack until a fresh server scene allows it', async () => {
    const cooling = {
      ...enemy,
      respawnAtUtc: '2026-10-11T00:05:00Z',
      resident: { ...enemy.resident, rank: 'Elite' },
    }
    const request = vi
      .spyOn(apiClient, 'request')
      .mockResolvedValue({ ...scene, objects: [cooling] })
    const wrapper = mount(LocationScene, { props })
    await flushPromises()
    await wrapper.get('[data-scene-marker="ENEMY_WOLF"]').trigger('click')
    expect(wrapper.get('[data-scene-attack]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Возрождение через 5:00')
    await wrapper.get('[data-scene-attack]').trigger('click')
    expect(request).toHaveBeenCalledTimes(1)
    wrapper.unmount()
  })

  it('refreshes the scene after claiming a reward and removes the completed quest marker', async () => {
    const quest = {
      id: 'Q1',
      displayName: 'Поручение',
      status: 'READY_TO_CLAIM',
      requiredLevel: 1,
      rewardXp: 0,
      rewardGold: 0,
    }
    const marker = { ...enemy, id: 'QUEST_Q1', kind: 'Npc', resident: null, questId: 'Q1' }
    vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce({ ...scene, objects: [marker] })
      .mockResolvedValueOnce({ ...scene, objects: [] })
    const session = useGameSessionStore()
    Object.assign(session, { questJournal: { quests: [quest] } })
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(null)
    const claim = vi.spyOn(session, 'claimQuest').mockResolvedValue(null)
    const wrapper = mount(LocationScene, { props })
    await flushPromises()
    await wrapper.get('[data-scene-marker="QUEST_Q1"]').trigger('click')
    await wrapper.get('[data-scene-quest]').trigger('click')
    await flushPromises()
    expect(claim).toHaveBeenCalledWith('Q1')
    expect(wrapper.find('[data-scene-marker="QUEST_Q1"]').exists()).toBe(false)
    wrapper.unmount()
  })

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
