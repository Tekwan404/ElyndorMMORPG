import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { Quest, QuestStatus } from '@/api/contracts'
import QuestView from '@/game/quests/views/QuestView.vue'
import * as worldPresentation from '@/game/world/locationPresentation'
import { useGameSessionStore } from '@/stores/gameSession'

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

    expect(wrapper.findAll('[data-quest-tab]')).toHaveLength(4)
    expect(wrapper.get('[data-quest-tab="story"]').text()).toContain('1')
    expect(wrapper.get('[data-quest-id="QUEST_STORY"]').text()).toContain('В РАБОТЕ')
    expect(wrapper.find('[data-quest-id="QUEST_AVAILABLE"]').exists()).toBe(false)

    await wrapper.get('[data-quest-tab="errands"]').trigger('click')
    expect(wrapper.get('[data-quest-id="QUEST_ERRAND"]').text()).toContain('Поручение')

    await wrapper.get('[data-quest-tab="contracts"]').trigger('click')
    expect(wrapper.get('[data-quest-id="QUEST_CONTRACT"]').text()).toContain('Получить награду')

    await wrapper.get('[data-quest-tab="completed"]').trigger('click')
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
    expect(abandon).toHaveBeenCalledWith('QUEST_ACTIVE')

    await wrapper.get('[data-quest-tab="contracts"]').trigger('click')
    await wrapper.get('[data-claim-quest]').trigger('click')
    expect(claim).toHaveBeenCalledWith('QUEST_READY')
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
    await wrapper.get('[data-quest-tab="contracts"]').trigger('click')
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
