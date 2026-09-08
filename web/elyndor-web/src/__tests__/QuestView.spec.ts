import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { Quest, QuestStatus } from '@/api/contracts'
import QuestView from '@/game/quests/views/QuestView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('QuestView', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('separates available active ready and completed quests', async () => {
    const session = useGameSessionStore()
    session.questJournal = {
      quests: [
        quest('QUEST_AVAILABLE', 'AVAILABLE'),
        quest('QUEST_ACTIVE', 'ACTIVE'),
        quest('QUEST_READY', 'READY_TO_CLAIM'),
        quest('QUEST_DONE', 'COMPLETED'),
      ],
    }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)

    const wrapper = mount(QuestView)
    await flushPromises()

    expect(wrapper.findAll('[data-quest-tab]')).toHaveLength(4)
    expect(wrapper.get('[data-quest-tab="available"]').text()).toContain('1')
    expect(wrapper.get('[data-quest-id="QUEST_AVAILABLE"]').attributes('data-quest-status'))
      .toBe('AVAILABLE')

    await wrapper.get('[data-quest-tab="active"]').trigger('click')
    expect(wrapper.get('[data-quest-id="QUEST_ACTIVE"]').attributes('data-quest-status'))
      .toBe('ACTIVE')

    await wrapper.get('[data-quest-tab="ready"]').trigger('click')
    expect(wrapper.get('[data-quest-id="QUEST_READY"]').text()).toContain('Получить награду')

    await wrapper.get('[data-quest-tab="completed"]').trigger('click')
    expect(wrapper.get('[data-quest-id="QUEST_DONE"]').text()).toContain('Задание завершено')
  })

  it('routes quest mutations through the game session store', async () => {
    const session = useGameSessionStore()
    session.questJournal = {
      quests: [
        quest('QUEST_AVAILABLE', 'AVAILABLE'),
        quest('QUEST_ACTIVE', 'ACTIVE'),
        quest('QUEST_READY', 'READY_TO_CLAIM'),
      ],
    }
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(session.questJournal)
    const accept = vi.spyOn(session, 'acceptQuest').mockResolvedValue(undefined)
    const abandon = vi.spyOn(session, 'abandonQuest').mockResolvedValue(undefined)
    const claim = vi.spyOn(session, 'claimQuest').mockResolvedValue(null)

    const wrapper = mount(QuestView)
    await flushPromises()

    await wrapper.get('[data-accept-quest]').trigger('click')
    expect(accept).toHaveBeenCalledWith('QUEST_AVAILABLE')

    await wrapper.get('[data-quest-tab="active"]').trigger('click')
    await wrapper.get('[data-abandon-quest]').trigger('click')
    expect(abandon).toHaveBeenCalledWith('QUEST_ACTIVE')

    await wrapper.get('[data-quest-tab="ready"]').trigger('click')
    await wrapper.get('[data-claim-quest]').trigger('click')
    expect(claim).toHaveBeenCalledWith('QUEST_READY')
  })
})

function quest(id: string, status: QuestStatus): Quest {
  return {
    id,
    displayName: id === 'QUEST_READY' ? 'Готовое задание' : 'Лесное поручение',
    description: 'Проверьте состояние лесных троп.',
    type: 'STORY',
    requiredLevel: 1,
    offerLocationId: 'WHISPERING_FOREST',
    status,
    objectives: [
      {
        id: 'KILL_WOLVES',
        type: 'KillMonster',
        targetId: 'FOREST_WOLF_L1',
        currentCount: status === 'READY_TO_CLAIM' || status === 'COMPLETED' ? 3 : 1,
        requiredCount: 3,
        completed: status === 'READY_TO_CLAIM' || status === 'COMPLETED',
        consumeOnClaim: false,
      },
    ],
    rewardXp: 90,
    rewardGold: 15,
    rewardItems: [],
    prerequisiteQuestIds: [],
    unlockLocationId: null,
  }
}
