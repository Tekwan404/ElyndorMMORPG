import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'

import type { Quest, QuestStatus } from '@/api/contracts'
import QuestView from '@/game/quests/views/QuestView.vue'
import * as worldPresentation from '@/game/world/locationPresentation'
import { useGameSessionStore } from '@/stores/gameSession'

enableAutoUnmount(afterEach)

describe('QuestView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  it('tracks story errands contracts and completed history instead of available work', async () => {
    const session = useGameSessionStore()
    session.questJournal = {
      quests: [
        quest('QUEST_AVAILABLE', 'AVAILABLE', 'STORY'),
        quest('QUEST_STORY', 'ACTIVE', 'STORY'),
        quest('QUEST_ERRAND', 'ACTIVE', 'SIDE'),
        quest('QUEST_CONTRACT', 'READY_TO_CLAIM', 'CONTRACT'),
        quest('QUEST_DONE', 'COMPLETED', 'STORY'),
      ],
    }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)

    const wrapper = mount(QuestView)
    await flushPromises()

    expect(wrapper.findAll('[data-tab]')).toHaveLength(4)
    expect(wrapper.get('[data-tab="story"]').text()).toContain('1')
    expect(wrapper.get('[data-quest-id="QUEST_STORY"]').text()).toContain('В РАБОТЕ')
    expect(wrapper.find('[data-quest-id="QUEST_AVAILABLE"]').exists()).toBe(false)

    await wrapper.get('[data-tab="errands"]').trigger('click')
    expect(wrapper.get('[data-quest-id="QUEST_ERRAND"]').text()).toContain('Поручение')

    await wrapper.get('[data-tab="contracts"]').trigger('click')
    expect(wrapper.get('[data-quest-id="QUEST_CONTRACT"]').text()).toContain('Получить награду')

    await wrapper.get('[data-tab="completed"]').trigger('click')
    expect(wrapper.get('[data-quest-id="QUEST_DONE"]').text()).toContain('Задание завершено')
  })

  it('routes abandon and claim through the game session store', async () => {
    const session = useGameSessionStore()
    session.questJournal = {
      quests: [
        quest('QUEST_ACTIVE', 'ACTIVE', 'STORY'),
        quest('QUEST_READY', 'READY_TO_CLAIM', 'CONTRACT'),
      ],
    }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)
    const abandon = vi.spyOn(session, 'abandonQuest').mockResolvedValue(undefined)
    const claim = vi.spyOn(session, 'claimQuest').mockResolvedValue(null)

    const wrapper = mount(QuestView)
    await flushPromises()

    await wrapper.get('[data-abandon-quest]').trigger('click')
    expect(abandon).not.toHaveBeenCalled()
    document.querySelector<HTMLButtonElement>('[data-confirm-accept]')!.click()
    await flushPromises()
    expect(abandon).toHaveBeenCalledWith('QUEST_ACTIVE')

    await wrapper.get('[data-tab="contracts"]').trigger('click')
    await wrapper.get('[data-claim-quest]').trigger('click')
    expect(claim).toHaveBeenCalledWith('QUEST_READY')
  })

  it('uses the accessible shared tabs and their existing selectors', async () => {
    const session = useGameSessionStore()
    session.questJournal = { quests: [] }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)
    const wrapper = mount(QuestView)
    await flushPromises()
    expect(wrapper.get('[role="tablist"]').attributes('aria-label')).toBe('Разделы журнала')
    await wrapper.get('[data-tab="story"]').trigger('keydown', { key: 'ArrowRight' })
    expect(wrapper.get('[data-tab="errands"]').attributes('aria-selected')).toBe('true')
  })

  it('offers retry for a generic journal HTTP failure instead of remaining in loading', async () => {
    const session = useGameSessionStore()
    const refresh = vi.spyOn(session, 'refreshQuestJournal')
      .mockImplementationOnce(async () => { session.errorCode = 'http_503'; return null })
      .mockImplementationOnce(async () => { session.questJournal = { quests: [] }; return session.questJournal })
    const wrapper = mount(QuestView)
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось')
    expect(wrapper.text()).not.toContain('Загружаем журнал')
    await wrapper.get('[data-retry-quest-journal]').trigger('click')
    await flushPromises()
    expect(refresh).toHaveBeenCalledTimes(2)
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    expect(wrapper.get('[role="status"]').text()).toContain('Здесь пока пусто')
  })

  it('does not claim success when claim returns null without a quest-prefixed error', async () => {
    const session = useGameSessionStore()
    session.questJournal = { quests: [quest('READY', 'READY_TO_CLAIM', 'STORY')] }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)
    vi.spyOn(session, 'claimQuest').mockImplementation(async () => { session.errorCode = 'http_503'; return null })
    const wrapper = mount(QuestView)
    await flushPromises()
    await wrapper.get('[data-claim-quest]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось')
    expect(wrapper.find('[data-quest-feedback]').exists()).toBe(false)
    expect(wrapper.get('[data-quest-id="READY"]').attributes('data-quest-status')).toBe('READY_TO_CLAIM')
  })

  it.each([true, false])('announces the server claim outcome when granted is %s', async granted => {
    const session = useGameSessionStore()
    session.questJournal = { quests: [quest('READY', 'READY_TO_CLAIM', 'STORY')] }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)
    vi.spyOn(session, 'claimQuest').mockResolvedValue({ questId: 'READY', granted, xpEarned: 90, goldEarned: 15, leveledUp: false, previousLevel: 1, currentLevel: 1, items: [] })
    const wrapper = mount(QuestView)
    await flushPromises()
    await wrapper.get('[data-claim-quest]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-quest-feedback]').text()).toContain(granted ? 'Награда получена' : 'Награда уже была получена')
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
  })

  it('shows only the claimed quest as busy and ignores unrelated mutation loading', async () => {
    const session = useGameSessionStore()
    session.questJournal = { quests: [quest('FIRST', 'READY_TO_CLAIM', 'STORY'), quest('SECOND', 'READY_TO_CLAIM', 'STORY')] }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)
    session.mutationPending = true
    const pending = ref(false)
    vi.spyOn(session, 'isMutationPending').mockImplementation(key => pending.value && key === 'quest:claim:FIRST')
    const wrapper = mount(QuestView)
    await flushPromises()
    expect(wrapper.get('[data-quest-id="FIRST"] [data-claim-quest]').attributes('disabled')).toBeUndefined()
    pending.value = true
    await wrapper.vm.$nextTick()
    expect(wrapper.get('[data-quest-id="FIRST"] [data-claim-quest]').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[data-quest-id="SECOND"] [data-claim-quest]').attributes('disabled')).toBeUndefined()
  })

  it('allows cancelling abandon and shows loading until the confirmed action completes', async () => {
    const session = useGameSessionStore()
    session.questJournal = { quests: [quest('ACTIVE', 'ACTIVE', 'STORY')] }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)
    let complete!: () => void
    const abandon = vi.spyOn(session, 'abandonQuest').mockImplementation(() => new Promise<void>(resolve => { complete = resolve }))
    const wrapper = mount(QuestView)
    await flushPromises()
    await wrapper.get('[data-abandon-quest]').trigger('click')
    document.querySelector<HTMLButtonElement>('[data-confirm-cancel]')!.click()
    await flushPromises()
    expect(abandon).not.toHaveBeenCalled()
    await wrapper.get('[data-abandon-quest]').trigger('click')
    document.querySelector<HTMLButtonElement>('[data-confirm-accept]')!.click()
    await flushPromises()
    expect(wrapper.get('[data-abandon-quest]').attributes('aria-busy')).toBe('true')
    session.questJournal = { quests: [] }
    complete()
    await flushPromises()
    expect(wrapper.get('[data-quest-feedback]').text()).toContain('Задание оставлено')
  })

  it('does not report abandon success when the server rejects a concurrently completed quest', async () => {
    const session = useGameSessionStore()
    session.questJournal = { quests: [quest('ACTIVE', 'ACTIVE', 'STORY')] }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)
    vi.spyOn(session, 'abandonQuest').mockImplementation(async () => {
      session.errorCode = 'quest_already_completed'
      session.questJournal = { quests: [quest('ACTIVE', 'COMPLETED', 'STORY')] }
    })
    const wrapper = mount(QuestView)
    await flushPromises()
    await wrapper.get('[data-abandon-quest]').trigger('click')
    document.querySelector<HTMLButtonElement>('[data-confirm-accept]')!.click()
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Задание уже выполнено')
    expect(wrapper.find('[data-quest-feedback]').exists()).toBe(false)
  })

  it('renders a semantic glyph beside every quest objective', async () => {
    const session = useGameSessionStore()
    session.questJournal = {
      quests: [quest('QUEST_ACTIVE', 'ACTIVE', 'STORY')],
    }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)

    const wrapper = mount(QuestView)
    await flushPromises()

    expect(wrapper.get('[data-quest-objective-icon="KillMonster"]').attributes('data-icon-id'))
      .toBe('quest-objective-KillMonster')
  })

  it('opens the guild from the contracts journal in any city location', async () => {
    const session = useGameSessionStore()
    session.snapshot = {
      world: { currentLocation: { id: 'SECOND_CITY' } },
    } as never
    session.questJournal = { quests: [] }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)
    vi.spyOn(worldPresentation, 'locationKind').mockReturnValue('city')

    const wrapper = mount(QuestView)
    await flushPromises()
    await wrapper.get('[data-tab="contracts"]').trigger('click')
    await wrapper.get('[data-quest-open-guild]').trigger('click')

    expect(wrapper.emitted('open-guild')).toEqual([[]])
  })
})

function quest(
  id: string,
  status: QuestStatus,
  type: 'STORY' | 'SIDE' | 'CONTRACT',
): Quest {
  return {
    id,
    displayName: id,
    description: 'Проверьте состояние лесных троп.',
    type,
    requiredLevel: 1,
    offerLocationId: 'WHISPERING_FOREST',
    status,
    objectives: [{
      id: 'KILL_WOLVES',
      type: 'KillMonster',
      targetId: 'FOREST_WOLF_L1',
      currentCount: status === 'READY_TO_CLAIM' || status === 'COMPLETED' ? 3 : 1,
      requiredCount: 3,
      completed: status === 'READY_TO_CLAIM' || status === 'COMPLETED',
      consumeOnClaim: false,
    }],
    rewardXp: 90,
    rewardGold: 15,
    rewardItems: [],
    prerequisiteQuestIds: [],
    unlockLocationId: null,
    issuerName: type === 'CONTRACT' ? 'Гильдия авантюристов' : 'Городской дозор',
    issuerRole: type === 'CONTRACT' ? 'Регистратор' : 'Источник',
    regionName: 'Шепчущий лес',
    contractNumber: type === 'CONTRACT' ? 'WF-001' : null,
    threatLevel: type === 'CONTRACT' ? 'Средняя' : null,
  }
}
