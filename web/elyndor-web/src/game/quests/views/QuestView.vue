<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import type { Quest, QuestObjective, QuestStatus } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard } from '@/ui/components'

type QuestTab = 'available' | 'active' | 'ready' | 'completed'

const session = useGameSessionStore()
const activeTab = ref<QuestTab>('available')

const tabs: readonly { id: QuestTab; label: string; status: QuestStatus }[] = [
  { id: 'available', label: 'Доступные', status: 'AVAILABLE' },
  { id: 'active', label: 'Активные', status: 'ACTIVE' },
  { id: 'ready', label: 'Готовы к сдаче', status: 'READY_TO_CLAIM' },
  { id: 'completed', label: 'Выполненные', status: 'COMPLETED' },
]

const quests = computed(() => session.questJournal?.quests ?? [])
const currentStatus = computed(() => tabs.find(tab => tab.id === activeTab.value)!.status)
const visibleQuests = computed(() =>
  quests.value.filter(quest => quest.status === currentStatus.value),
)
const inProgressCount = computed(() =>
  quests.value.filter(quest => quest.status === 'ACTIVE' || quest.status === 'READY_TO_CLAIM').length,
)

const targetNames: Readonly<Record<string, string>> = {
  FOREST_WOLF_L1: 'Лесной волк',
  FOREST_BOAR_L2: 'Лесной кабан',
  GIANT_SPIDER_L3: 'Гигантский паук',
  FOREST_WOLF_L4: 'Лесной волк',
  ALPHA_WOLF_L5: 'Альфа-волк',
  DEEP_WOLF_L6: 'Тёмный лесной волк',
  CORRUPTED_BOAR_L7: 'Осквернённый кабан',
  DEEP_SPIDER_L8: 'Глубинный паук',
  GOBLIN_SCOUT_L9: 'Гоблин-разведчик',
  DEEP_WOLF_L10: 'Матёрый лесной волк',
  ALPHA_WOLF_L11: 'Старый альфа-волк',
  SPIDER_BROODMOTHER_L14: 'Паучья Прародительница',
  BANDIT_ROGUE_L15: 'Лесной разбойник',
  BANDIT_ARCHER_L16: 'Разбойник-лучник',
  CORRUPTED_BOAR_L17: 'Осквернённый вепрь',
  BLIGHTED_SPIDER_L18: 'Осквернённый паук',
  BANDIT_ROGUE_L19: 'Опытный лесной разбойник',
  ALPHA_WOLF_L20: 'Осквернённый альфа-волк',
  WOLF_HIDE: 'Шкура волка',
  WOLF_FANG: 'Волчий клык',
  BOAR_TUSK: 'Кабаний клык',
  SPIDER_SILK: 'Паучий шёлк',
  SPIDER_VENOM_SAC: 'Ядовитая железа паука',
}

const itemNames: Readonly<Record<string, string>> = {
  SMALL_HEALING_POTION: 'Малое зелье лечения',
  MINOR_ANTIDOTE: 'Малое противоядие',
  MINOR_BATTLE_TONIC: 'Малый боевой тоник',
  DEEP_FOREST_CHARM: 'Оберег Глубокого леса',
  BLIGHTED_GROVE_RING: 'Кольцо Осквернённой чащи',
}

const locationNames: Readonly<Record<string, string>> = {
  STARTER_TOWN: 'Стартовый город',
  WHISPERING_FOREST: 'Шепчущий лес',
  DEEP_FOREST: 'Глубокий лес',
  BROODMOTHER_LAIR: 'Логово Прародительницы',
  BLIGHTED_GROVE: 'Осквернённая чаща',
}

const errorMessages: Readonly<Record<string, string>> = {
  quest_level_required: 'Нужен более высокий уровень.',
  quest_invalid_location: 'Это задание можно принять только в указанной локации.',
  quest_prerequisite_required: 'Сначала завершите предыдущую часть цепочки.',
  quest_already_active: 'Задание уже принято.',
  quest_already_completed: 'Задание уже выполнено.',
  quest_not_ready: 'Цели задания ещё не выполнены.',
  quest_items_protected: 'Не удалось забрать необходимые предметы из инвентаря.',
  quest_claim_conflict: 'Награда уже обрабатывается. Обновите журнал.',
}

const questError = computed(() => {
  const code = session.errorCode
  return code?.startsWith('quest_') ? (errorMessages[code] ?? 'Не удалось выполнить действие с заданием.') : null
})

function count(status: QuestStatus): number {
  return quests.value.filter(quest => quest.status === status).length
}

function locationName(id: string): string {
  return locationNames[id] ?? 'Неизвестная область'
}

function targetName(id: string): string {
  return targetNames[id] ?? 'Неизвестная цель'
}

function objectiveLabel(objective: QuestObjective): string {
  return objective.type === 'CollectItem'
    ? `Собрать: ${targetName(objective.targetId)}`
    : `Победить: ${targetName(objective.targetId)}`
}

function objectiveProgress(objective: QuestObjective): number {
  return Math.min(100, Math.round(objective.currentCount / objective.requiredCount * 100))
}

function questTypeLabel(quest: Quest): string {
  if (quest.type === 'CONTRACT') return 'Контракт'
  if (quest.type === 'SIDE') return 'Побочное'
  return 'Сюжетное'
}

function statusLabel(status: QuestStatus): string {
  if (status === 'AVAILABLE') return 'ДОСТУПНО'
  if (status === 'ACTIVE') return 'В РАБОТЕ'
  if (status === 'READY_TO_CLAIM') return 'МОЖНО СДАТЬ'
  return 'ВЫПОЛНЕНО'
}

function emptyMessage(tab: QuestTab): string {
  if (tab === 'available') return 'В этой локации сейчас нет доступных заданий.'
  if (tab === 'active') return 'Примите доступное задание, чтобы начать цепочку.'
  if (tab === 'ready') return 'Завершите цели активных заданий — они появятся здесь.'
  return 'История выполненных заданий пока пуста.'
}

async function accept(questId: string): Promise<void> {
  await session.acceptQuest(questId)
}

async function abandon(questId: string): Promise<void> {
  await session.abandonQuest(questId)
}

async function claim(questId: string): Promise<void> {
  await session.claimQuest(questId)
}

onMounted(async () => {
  await session.refreshQuestJournal()
  if (count('AVAILABLE') === 0 && count('ACTIVE') > 0) activeTab.value = 'active'
  if (count('AVAILABLE') === 0 && count('ACTIVE') === 0 && count('READY_TO_CLAIM') > 0) {
    activeTab.value = 'ready'
  }
})
</script>

<template>
  <section class="quests" data-quest-view>
    <header class="quests__header">
      <div>
        <small>ЖУРНАЛ ЗАДАНИЙ</small>
        <h1>Квесты</h1>
        <p>Цепочка приключений и контрактов героя.</p>
      </div>
      <div class="quests__counter" aria-label="Заданий в работе">
        <strong>{{ inProgressCount }}</strong>
        <span>в работе</span>
      </div>
    </header>

    <nav class="quest-tabs" aria-label="Разделы журнала">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        type="button"
        class="quest-tabs__button"
        :class="{ 'quest-tabs__button--active': activeTab === tab.id }"
        :data-quest-tab="tab.id"
        :aria-current="activeTab === tab.id ? 'page' : undefined"
        @click="activeTab = tab.id"
      >
        <span>{{ tab.label }}</span>
        <b>{{ count(tab.status) }}</b>
      </button>
    </nav>

    <p v-if="questError" class="quests__error" role="alert">{{ questError }}</p>

    <UICard v-if="session.questJournal === null && !questError" class="quests__empty">
      <strong>Загружаем журнал…</strong>
      <p>Получаем актуальное состояние заданий с сервера.</p>
    </UICard>

    <div v-else-if="visibleQuests.length" class="quests__list">
      <UICard
        v-for="quest in visibleQuests"
        :key="quest.id"
        class="quest-card"
        :class="{ 'quest-card--completed': quest.status === 'COMPLETED' }"
        :data-quest-id="quest.id"
        :data-quest-status="quest.status"
      >
        <div class="quest-card__topline">
          <span class="quest-card__status">{{ statusLabel(quest.status) }}</span>
          <span>{{ questTypeLabel(quest) }} · ур. {{ quest.requiredLevel }}</span>
        </div>

        <div>
          <h2>{{ quest.displayName }}</h2>
          <p class="quest-card__location">{{ locationName(quest.offerLocationId) }}</p>
        </div>

        <p class="quest-card__description">{{ quest.description }}</p>

        <section class="quest-card__objectives" aria-label="Цели задания">
          <div
            v-for="objective in quest.objectives"
            :key="objective.id"
            class="objective"
            :class="{ 'objective--done': objective.completed }"
          >
            <div class="objective__line">
              <span>{{ objectiveLabel(objective) }}</span>
              <b>{{ objective.currentCount }} / {{ objective.requiredCount }}</b>
            </div>
            <div class="objective__bar" aria-hidden="true">
              <span :style="{ width: `${objectiveProgress(objective)}%` }" />
            </div>
          </div>
        </section>

        <section class="quest-card__rewards" aria-label="Награда">
          <small>НАГРАДА</small>
          <div>
            <span v-if="quest.rewardXp">+{{ quest.rewardXp }} опыта</span>
            <span v-if="quest.rewardGold">+{{ quest.rewardGold }} золота</span>
            <span v-for="item in quest.rewardItems" :key="item.itemId">
              {{ itemNames[item.itemId] ?? 'Предмет' }} ×{{ item.quantity }}
            </span>
          </div>
        </section>

        <p v-if="quest.unlockLocationId" class="quest-card__unlock">
          Открывает: <strong>{{ locationName(quest.unlockLocationId) }}</strong>
        </p>

        <footer class="quest-card__actions">
          <UIButton
            v-if="quest.status === 'AVAILABLE'"
            data-accept-quest
            :disabled="session.mutationPending"
            @click="accept(quest.id)"
          >
            Принять
          </UIButton>
          <UIButton
            v-else-if="quest.status === 'ACTIVE'"
            data-abandon-quest
            variant="secondary"
            :disabled="session.mutationPending"
            @click="abandon(quest.id)"
          >
            Отказаться
          </UIButton>
          <UIButton
            v-else-if="quest.status === 'READY_TO_CLAIM'"
            data-claim-quest
            :disabled="session.mutationPending"
            @click="claim(quest.id)"
          >
            Получить награду
          </UIButton>
          <span v-else class="quest-card__completed-mark">✓ Задание завершено</span>
        </footer>
      </UICard>
    </div>

    <UICard v-else class="quests__empty">
      <strong>Здесь пока пусто</strong>
      <p>{{ emptyMessage(activeTab) }}</p>
    </UICard>
  </section>
</template>

<style scoped>
.quests{display:grid;gap:var(--ui-space-4);width:min(100%,var(--ui-content-width));margin-inline:auto;padding:var(--ui-space-4);padding-bottom:var(--ui-space-7)}
.quests__header{display:flex;align-items:flex-start;justify-content:space-between;gap:var(--ui-space-3)}
.quests__header small{color:var(--ui-color-primary);font-size:var(--ui-font-size-xs);font-weight:800;letter-spacing:.1em}
.quests__header h1{margin:.15rem 0 0;font-family:var(--ui-font-display)}
.quests__header p{margin:.25rem 0 0;color:var(--ui-color-text-muted);font-size:var(--ui-font-size-sm)}
.quests__counter{display:grid;min-width:3.8rem;justify-items:center;padding:.45rem .55rem;border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-md);background:rgb(255 255 255 / 2%)}
.quests__counter strong{font-family:var(--ui-font-display);font-size:var(--ui-font-size-lg);color:var(--ui-color-primary)}
.quests__counter span{color:var(--ui-color-text-muted);font-size:.56rem;text-transform:uppercase}
.quest-tabs{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:6px}
.quest-tabs__button{display:flex;min-height:38px;align-items:center;justify-content:space-between;gap:6px;padding:7px 9px;border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-md);background:rgb(255 255 255 / 2%);color:var(--ui-color-text-muted);font:inherit;font-size:.68rem;cursor:pointer}
.quest-tabs__button b{display:grid;min-width:1.35rem;height:1.35rem;place-items:center;border-radius:var(--ui-radius-round);background:rgb(255 255 255 / 5%);font-size:.62rem}
.quest-tabs__button--active{border-color:color-mix(in srgb,var(--ui-color-primary) 45%,var(--ui-color-border));background:color-mix(in srgb,var(--ui-color-primary) 10%,transparent);color:var(--ui-color-text-primary)}
.quests__list{display:grid;gap:var(--ui-space-3)}
.quest-card{display:grid;gap:var(--ui-space-3)}
.quest-card__topline{display:flex;align-items:center;justify-content:space-between;gap:var(--ui-space-2);color:var(--ui-color-text-muted);font-size:.62rem}
.quest-card__status{width:max-content;padding:.2rem .45rem;border:1px solid color-mix(in srgb,var(--ui-color-primary) 38%,transparent);border-radius:var(--ui-radius-round);color:var(--ui-color-primary);font-size:.58rem;font-weight:800;letter-spacing:.05em}
.quest-card h2{margin:0;font-family:var(--ui-font-display);font-size:var(--ui-font-size-lg)}
.quest-card__location{margin:.15rem 0 0;color:var(--ui-color-primary);font-size:.7rem}
.quest-card__description{margin:0;color:var(--ui-color-text-muted);line-height:1.48}
.quest-card__objectives{display:grid;gap:8px;padding:10px;border:1px solid rgb(255 255 255 / 6%);border-radius:var(--ui-radius-md);background:rgb(0 0 0 / 12%)}
.objective{display:grid;gap:5px}.objective__line{display:flex;justify-content:space-between;gap:var(--ui-space-3);font-size:.72rem}.objective__line b{white-space:nowrap;font-variant-numeric:tabular-nums}.objective--done .objective__line{color:var(--ui-color-success)}
.objective__bar{height:4px;overflow:hidden;border-radius:var(--ui-radius-round);background:rgb(255 255 255 / 7%)}.objective__bar span{display:block;height:100%;border-radius:inherit;background:var(--ui-color-primary)}
.objective--done .objective__bar span{background:var(--ui-color-success)}
.quest-card__rewards{display:grid;gap:5px}.quest-card__rewards small{color:var(--ui-color-gold);font-size:.58rem;font-weight:800;letter-spacing:.08em}.quest-card__rewards div{display:flex;flex-wrap:wrap;gap:6px}.quest-card__rewards span{padding:4px 7px;border:1px solid rgb(232 200 102 / 14%);border-radius:var(--ui-radius-round);background:rgb(232 200 102 / 4%);color:#ddd3a5;font-size:.66rem}
.quest-card__unlock{margin:0;padding-top:var(--ui-space-2);border-top:1px solid var(--ui-color-border);color:var(--ui-color-text-muted);font-size:.7rem}.quest-card__unlock strong{color:var(--ui-color-text-primary)}
.quest-card__actions{display:flex;justify-content:flex-end}.quest-card__completed-mark{color:var(--ui-color-success);font-size:.72rem;font-weight:700}.quest-card--completed{opacity:.74}
.quests__empty{text-align:center}.quests__empty p{margin-bottom:0;color:var(--ui-color-text-muted)}
.quests__error{margin:0;padding:9px 11px;border:1px solid color-mix(in srgb,var(--ui-color-danger) 36%,transparent);border-radius:var(--ui-radius-md);background:color-mix(in srgb,var(--ui-color-danger) 8%,transparent);color:var(--ui-color-danger);font-size:.72rem}
@media (min-width:460px){.quest-tabs{grid-template-columns:repeat(4,minmax(0,1fr))}.quest-tabs__button{display:grid;justify-items:center;justify-content:stretch;text-align:center}}
</style>
