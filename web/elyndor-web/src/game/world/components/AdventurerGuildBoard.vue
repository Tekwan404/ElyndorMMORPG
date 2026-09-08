<script setup lang="ts">
import { computed, watch } from 'vue'

import type { Quest } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIModal } from '@/ui/components'

const props = defineProps<{
  open: boolean
  locationId: string
}>()

const emit = defineEmits<{ close: [] }>()
const session = useGameSessionStore()

const isCentralPost = computed(() => props.locationId === 'STARTER_TOWN')
const boardTitle = computed(() =>
  isCentralPost.value
    ? 'Представительство Гильдии авантюристов'
    : 'Экспедиционный пост Гильдии',
)
const boardSubtitle = computed(() =>
  isCentralPost.value
    ? 'Регистратор публикует официальные контракты для доступных регионов.'
    : 'Полевой регистратор принимает и выдаёт контракты текущей экспедиции.',
)

const contracts = computed(() => {
  const quests = session.questJournal?.quests ?? []
  return quests
    .filter(quest =>
      quest.type === 'CONTRACT'
      && (
        quest.offerLocationId === props.locationId
        || quest.status === 'ACTIVE'
        || quest.status === 'READY_TO_CLAIM'
      ),
    )
    .sort((left, right) => statusPriority(left) - statusPriority(right)
      || left.requiredLevel - right.requiredLevel)
})

watch(() => props.open, (open) => {
  if (open) void session.refreshQuestJournal()
})

function statusPriority(quest: Quest): number {
  if (quest.status === 'READY_TO_CLAIM') return 0
  if (quest.status === 'ACTIVE') return 1
  if (quest.status === 'AVAILABLE') return 2
  if (quest.status === 'LOCKED') return 3
  return 4
}

function contractTitle(quest: Quest): string {
  return quest.displayName.startsWith('Контракт: ')
    ? quest.displayName.slice('Контракт: '.length)
    : quest.displayName
}

function statusLabel(quest: Quest): string {
  if (quest.status === 'READY_TO_CLAIM') return 'ГОТОВ К СДАЧЕ'
  if (quest.status === 'ACTIVE') return 'ПРИНЯТ'
  if (quest.status === 'AVAILABLE') return 'ДОСТУПЕН'
  if (quest.status === 'COMPLETED') return 'ВЫПОЛНЕН'
  return 'НЕДОСТУПЕН'
}

function lockedReason(quest: Quest): string {
  const level = session.snapshot?.character?.level ?? 0
  if (level < quest.requiredLevel) return 'Требуется ' + quest.requiredLevel + ' уровень'
  return 'Требуется завершить предыдущую историю или контракт'
}

async function accept(quest: Quest): Promise<void> {
  await session.acceptQuest(quest.id)
}

async function claim(quest: Quest): Promise<void> {
  await session.claimQuest(quest.id)
}
</script>

<template>
  <UIModal :open="open" title="Гильдия авантюристов" @close="emit('close')">
    <section class="guild-board" data-adventurer-guild-board>
      <header class="guild-board__header">
        <div class="guild-board__seal" aria-hidden="true">⚔</div>
        <div>
          <small>{{ isCentralPost ? 'ГОРОДСКОЕ ПРЕДСТАВИТЕЛЬСТВО' : 'ПОЛЕВОЕ ПРЕДСТАВИТЕЛЬСТВО' }}</small>
          <h2>{{ boardTitle }}</h2>
          <p>{{ boardSubtitle }}</p>
        </div>
      </header>

      <section class="registrar">
        <div>
          <small>РЕГИСТРАТОР</small>
          <strong>{{ isCentralPost ? 'Стойка регистрации контрактов' : 'Полевой журнал контрактов' }}</strong>
          <p>Контракты — официально зарегистрированная работа. Недоступные заказы открываются по мере развития героя и истории.</p>
        </div>
        <span>{{ contracts.length }} записей</span>
      </section>

      <div v-if="contracts.length" class="contract-list">
        <article
          v-for="contract in contracts"
          :key="contract.id"
          class="guild-contract"
          :class="{ 'guild-contract--ready': contract.status === 'READY_TO_CLAIM' }"
          :data-guild-contract-id="contract.id"
          :data-guild-contract-status="contract.status"
        >
          <header class="guild-contract__heading">
            <div>
              <small>
                КОНТРАКТ №{{ contract.contractNumber ?? contract.id }}
                · {{ statusLabel(contract) }}
              </small>
              <h3>{{ contractTitle(contract) }}</h3>
            </div>
            <span>ур. {{ contract.requiredLevel }}</span>
          </header>

          <dl class="guild-contract__meta">
            <div>
              <dt>Заказчик</dt>
              <dd>{{ contract.issuerName ?? 'Гильдия авантюристов' }}</dd>
            </div>
            <div>
              <dt>Регион</dt>
              <dd>{{ contract.regionName ?? contract.offerLocationId }}</dd>
            </div>
            <div>
              <dt>Угроза</dt>
              <dd>{{ contract.threatLevel ?? 'Не указана' }}</dd>
            </div>
          </dl>

          <p class="guild-contract__description">{{ contract.description }}</p>

          <div class="guild-contract__reward">
            <small>НАГРАДА</small>
            <strong>+{{ contract.rewardXp }} опыта · +{{ contract.rewardGold }} золота</strong>
            <span v-if="contract.unlockLocationId">Открывает новую область</span>
          </div>

          <footer>
            <UIButton
              v-if="contract.status === 'AVAILABLE'"
              data-guild-accept-contract
              :disabled="session.mutationPending"
              @click="accept(contract)"
            >
              Принять контракт
            </UIButton>
            <UIButton
              v-else-if="contract.status === 'READY_TO_CLAIM'"
              data-guild-claim-contract
              :disabled="session.mutationPending"
              @click="claim(contract)"
            >
              Получить награду
            </UIButton>
            <span v-else-if="contract.status === 'ACTIVE'" class="guild-contract__state">
              Контракт зарегистрирован · цель в работе
            </span>
            <span v-else-if="contract.status === 'LOCKED'" class="guild-contract__state guild-contract__state--locked">
              {{ lockedReason(contract) }}
            </span>
            <span v-else class="guild-contract__state guild-contract__state--done">
              Контракт закрыт
            </span>
          </footer>
        </article>
      </div>

      <div v-else class="guild-board__empty">
        <strong>Новых контрактов нет</strong>
        <p>В этом представительстве сейчас нет работы, доступной для регистрации.</p>
      </div>
    </section>
  </UIModal>
</template>

<style scoped>
.guild-board{display:grid;gap:var(--ui-space-3)}
.guild-board__header{display:grid;grid-template-columns:3.4rem minmax(0,1fr);gap:var(--ui-space-3);align-items:center;padding:var(--ui-space-3);border:1px solid rgb(232 200 102 / 18%);border-radius:var(--ui-radius-md);background:linear-gradient(135deg,rgb(232 200 102 / 8%),transparent 58%),var(--ui-color-surface-1)}
.guild-board__seal{display:grid;width:3.4rem;height:3.4rem;place-items:center;border:1px solid rgb(232 200 102 / 28%);border-radius:50%;background:rgb(232 200 102 / 6%);color:var(--ui-color-gold);font-size:1.25rem}
.guild-board__header small,.registrar small,.guild-contract small{color:var(--ui-color-gold);font-size:.56rem;font-weight:800;letter-spacing:.08em}
.guild-board__header h2{margin:.15rem 0 0;font-family:var(--ui-font-display);font-size:var(--ui-font-size-xl)}
.guild-board__header p,.registrar p,.guild-board__empty p{margin:.3rem 0 0;color:var(--ui-color-text-muted);font-size:.68rem;line-height:1.45}
.registrar{display:flex;align-items:center;justify-content:space-between;gap:var(--ui-space-3);padding:var(--ui-space-3);border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-md);background:var(--ui-color-surface-1)}
.registrar>div{display:grid;gap:2px}.registrar>span{flex:0 0 auto;color:var(--ui-color-text-muted);font-size:.62rem}
.contract-list{display:grid;gap:var(--ui-space-2)}
.guild-contract{display:grid;gap:var(--ui-space-3);padding:var(--ui-space-3);border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-md);background:linear-gradient(160deg,rgb(18 22 32 / 100%),rgb(7 10 17 / 100%))}
.guild-contract--ready{border-color:color-mix(in srgb,var(--ui-color-success) 42%,var(--ui-color-border))}
.guild-contract__heading{display:flex;align-items:flex-start;justify-content:space-between;gap:var(--ui-space-3)}
.guild-contract__heading h3{margin:.15rem 0 0;font-family:var(--ui-font-display);font-size:var(--ui-font-size-lg)}
.guild-contract__heading>span{padding:3px 6px;border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-round);color:var(--ui-color-text-muted);font-size:.58rem;white-space:nowrap}
.guild-contract__meta{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:1px;margin:0;overflow:hidden;border:1px solid rgb(255 255 255 / 6%);border-radius:var(--ui-radius-sm);background:rgb(255 255 255 / 6%)}
.guild-contract__meta>div{display:grid;gap:2px;padding:7px;background:rgb(5 8 14 / 92%)}
.guild-contract__meta dt{color:var(--ui-color-text-muted);font-size:.52rem;text-transform:uppercase}
.guild-contract__meta dd{margin:0;font-size:.64rem}
.guild-contract__description{margin:0;color:var(--ui-color-text-secondary);font-size:.7rem;line-height:1.5}
.guild-contract__reward{display:grid;gap:2px;padding-top:var(--ui-space-2);border-top:1px solid var(--ui-color-border)}
.guild-contract__reward strong{color:#ddd3a5;font-size:.72rem}.guild-contract__reward span{color:var(--ui-color-text-muted);font-size:.6rem}
.guild-contract footer{display:flex;justify-content:flex-end}
.guild-contract__state{padding:6px 9px;border:1px solid rgb(146 136 255 / 22%);border-radius:var(--ui-radius-round);color:#c8c4ff;font-size:.62rem}
.guild-contract__state--locked{border-color:rgb(232 200 102 / 18%);color:#b9aa7d}
.guild-contract__state--done{border-color:rgb(79 185 150 / 20%);color:#84d5bb}
.guild-board__empty{padding:var(--ui-space-5);border:1px dashed var(--ui-color-border);border-radius:var(--ui-radius-md);text-align:center}
@media(max-width:520px){.guild-contract__meta{grid-template-columns:1fr}.registrar{align-items:flex-start;flex-direction:column}}
</style>
