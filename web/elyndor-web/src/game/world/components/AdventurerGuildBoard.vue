<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { Quest } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
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
    ? 'Городская гильдия'
    : 'Экспедиционный пост',
)
const boardSubtitle = computed(() =>
  isCentralPost.value
    ? 'Официальные контракты для доступных регионов.'
    : 'Контракты и цели текущей экспедиции.',
)
const featuredNpc = computed(() => isCentralPost.value
  ? { name: 'Селия', role: 'Регистратор', art: gameArt.npc.registrar }
  : { name: 'Гаррет', role: 'Полевой охотник', art: gameArt.npc.huntMaster })
const guildNpcs = [
  { name: 'Селия', role: 'Регистратор', detail: 'Контракты', art: gameArt.npc.registrar, active: true },
  { name: 'Гаррет', role: 'Мастер охоты', detail: 'Цели и угрозы', art: gameArt.npc.huntMaster, active: false },
  { name: 'Бран', role: 'Квартирмейстер', detail: 'Снабжение', art: gameArt.npc.quartermaster, active: false },
  { name: 'Эллира', role: 'Картограф', detail: 'Новые регионы', art: gameArt.npc.cartographer, active: false },
]

type ContractFilter = 'ALL' | 'READY_TO_CLAIM' | 'ACTIVE' | 'AVAILABLE' | 'LOCKED'

const contractFilters: Array<{ id: ContractFilter; label: string }> = [
  { id: 'ALL', label: 'Все' },
  { id: 'READY_TO_CLAIM', label: 'К сдаче' },
  { id: 'ACTIVE', label: 'В работе' },
  { id: 'AVAILABLE', label: 'Новые' },
  { id: 'LOCKED', label: 'Закрытые' },
]
const activeFilter = ref<ContractFilter>('ALL')
const selectedContractId = ref<string | null>(null)

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

const filteredContracts = computed(() => activeFilter.value === 'ALL'
  ? contracts.value
  : contracts.value.filter(contract => contract.status === activeFilter.value))

const selectedContract = computed(() =>
  filteredContracts.value.find(contract => contract.id === selectedContractId.value)
  ?? filteredContracts.value[0]
  ?? null,
)

watch(() => props.open, (open) => {
  if (open) void session.refreshQuestJournal()
})

watch(contracts, availableContracts => {
  if (!availableContracts.some(contract => contract.id === selectedContractId.value)) {
    selectedContractId.value = availableContracts[0]?.id ?? null
  }
}, { immediate: true })

watch(activeFilter, () => {
  if (!filteredContracts.value.some(contract => contract.id === selectedContractId.value)) {
    selectedContractId.value = filteredContracts.value[0]?.id ?? null
  }
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

function contractGlyph(quest: Quest): string {
  if (quest.status === 'READY_TO_CLAIM') return '✦'
  if (quest.status === 'ACTIVE') return '⚔'
  if (quest.status === 'AVAILABLE') return '◇'
  if (quest.status === 'LOCKED') return '◌'
  return '✓'
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
        <div class="guild-board__portrait">
          <img :src="featuredNpc.art" :alt="featuredNpc.name" />
        </div>
        <div>
          <small>{{ isCentralPost ? 'ГОРОДСКОЕ ПРЕДСТАВИТЕЛЬСТВО' : 'ПОЛЕВОЕ ПРЕДСТАВИТЕЛЬСТВО' }}</small>
          <h2>{{ boardTitle }}</h2>
          <p>{{ featuredNpc.name }} · {{ featuredNpc.role }}. {{ boardSubtitle }}</p>
        </div>
      </header>

      <section class="guild-board__people" aria-label="Представители гильдии">
        <header class="guild-staff-heading">
          <div>
            <small>СОСТАВ ПОСТА</small>
            <strong>Люди, которые держат экспедицию</strong>
          </div>
          <span>4 специалиста</span>
        </header>
        <div class="guild-staff-rail">
          <article
            v-for="npc in guildNpcs"
            :key="npc.name"
            class="guild-npc"
            :class="{ 'guild-npc--active': npc.active }"
            :aria-label="`${npc.name}, ${npc.role}`"
          >
            <span class="guild-npc__portrait">
              <img :src="npc.art" :alt="npc.name" loading="lazy" />
            </span>
            <div>
              <strong>{{ npc.name }}</strong>
              <small>{{ npc.role }}</small>
              <span>{{ npc.detail }}</span>
            </div>
          </article>
        </div>
      </section>

      <section class="registrar">
        <div>
          <small>РЕГИСТРАТОР</small>
          <strong>{{ isCentralPost ? 'Стойка регистрации контрактов' : 'Полевой журнал контрактов' }}</strong>
          <p>Контракты — официально зарегистрированная работа. Недоступные заказы открываются по мере развития героя и истории.</p>
        </div>
        <span>{{ contracts.length }} заказов</span>
      </section>

      <section v-if="contracts.length" class="contract-board" aria-label="Доска контрактов">
        <header class="contract-board__heading">
          <div>
            <small>ДОСКА КОНТРАКТОВ</small>
            <strong>Выбери заказ, чтобы изучить детали</strong>
          </div>
          <span>{{ filteredContracts.length }} / {{ contracts.length }}</span>
        </header>

        <nav class="contract-filters" aria-label="Фильтр контрактов">
          <button
            v-for="filter in contractFilters"
            :key="filter.id"
            type="button"
            class="contract-filter"
            :class="{ 'contract-filter--active': activeFilter === filter.id }"
            :aria-pressed="activeFilter === filter.id"
            @click="activeFilter = filter.id"
          >
            {{ filter.label }}
          </button>
        </nav>

        <div v-if="filteredContracts.length" class="contract-rail">
          <button
            v-for="contract in filteredContracts"
            :key="contract.id"
            type="button"
            class="contract-tab"
            :class="{
              'contract-tab--selected': selectedContract?.id === contract.id,
              'contract-tab--ready': contract.status === 'READY_TO_CLAIM',
            }"
            :data-guild-contract-tab="contract.id"
            :aria-pressed="selectedContract?.id === contract.id"
            @click="selectedContractId = contract.id"
          >
            <span class="contract-tab__seal" aria-hidden="true">{{ contractGlyph(contract) }}</span>
            <span class="contract-tab__copy">
              <small>№{{ contract.contractNumber ?? contract.id }}</small>
              <strong>{{ contractTitle(contract) }}</strong>
              <em>{{ statusLabel(contract) }}</em>
            </span>
            <b>ур. {{ contract.requiredLevel }}</b>
          </button>
        </div>

        <div v-else class="guild-board__empty guild-board__empty--filter">
          <strong>В этом разделе нет контрактов</strong>
          <p>Переключи фильтр или дождись нового заказа от гильдии.</p>
        </div>

        <article
          v-if="selectedContract"
          class="guild-contract"
          :class="{ 'guild-contract--ready': selectedContract.status === 'READY_TO_CLAIM' }"
          :data-guild-contract-id="selectedContract.id"
          :data-guild-contract-status="selectedContract.status"
        >
          <header class="guild-contract__heading">
            <div>
              <small>
                КОНТРАКТ №{{ selectedContract.contractNumber ?? selectedContract.id }}
                · {{ statusLabel(selectedContract) }}
              </small>
              <h3>{{ contractTitle(selectedContract) }}</h3>
            </div>
            <span>ур. {{ selectedContract.requiredLevel }}</span>
          </header>

          <dl class="guild-contract__meta">
            <div>
              <dt>Заказчик</dt>
              <dd>{{ selectedContract.issuerName ?? 'Гильдия авантюристов' }}</dd>
            </div>
            <div>
              <dt>Регион</dt>
              <dd>{{ selectedContract.regionName ?? selectedContract.offerLocationId }}</dd>
            </div>
            <div>
              <dt>Угроза</dt>
              <dd>{{ selectedContract.threatLevel ?? 'Не указана' }}</dd>
            </div>
          </dl>

          <p class="guild-contract__description">{{ selectedContract.description }}</p>

          <div class="guild-contract__reward">
            <small>НАГРАДА</small>
            <strong>+{{ selectedContract.rewardXp }} опыта · +{{ selectedContract.rewardGold }} золота</strong>
            <span v-if="selectedContract.unlockLocationId">Открывает новую область</span>
          </div>

          <footer>
            <UIButton
              v-if="selectedContract.status === 'AVAILABLE'"
              data-guild-accept-contract
              :disabled="session.mutationPending"
              @click="accept(selectedContract)"
            >
              Принять контракт
            </UIButton>
            <UIButton
              v-else-if="selectedContract.status === 'READY_TO_CLAIM'"
              data-guild-claim-contract
              :disabled="session.mutationPending"
              @click="claim(selectedContract)"
            >
              Получить награду
            </UIButton>
            <span v-else-if="selectedContract.status === 'ACTIVE'" class="guild-contract__state">
              Контракт зарегистрирован · цель в работе
            </span>
            <span v-else-if="selectedContract.status === 'LOCKED'" class="guild-contract__state guild-contract__state--locked">
              {{ lockedReason(selectedContract) }}
            </span>
            <span v-else class="guild-contract__state guild-contract__state--done">
              Контракт закрыт
            </span>
          </footer>
        </article>
      </section>

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
.guild-board__portrait{display:grid;width:3.4rem;height:3.4rem;place-items:center;overflow:hidden;border:1px solid rgb(232 200 102 / 46%);border-radius:var(--ui-radius-md);background:rgb(5 8 13 / 88%)}
.guild-board__portrait img{width:145%;height:145%;object-fit:cover;object-position:50% 18%;transform:translateY(8%)}
.guild-board__header small,.registrar small,.guild-contract small{color:var(--ui-color-gold);font-size:.56rem;font-weight:800;letter-spacing:.08em}
.guild-board__header h2{margin:.15rem 0 0;font-family:var(--ui-font-display);font-size:var(--ui-font-size-xl)}
.guild-board__header p,.registrar p,.guild-board__empty p{margin:.3rem 0 0;color:var(--ui-color-text-muted);font-size:.68rem;line-height:1.45}
.guild-board__people{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:5px}
.guild-npc{display:grid;grid-template-columns:2.35rem minmax(0,1fr);align-items:center;gap:7px;min-width:0;padding:6px;border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-sm);background:rgb(8 12 18 / 90%);opacity:.68}
.guild-npc--active{border-color:rgb(232 200 102 / 42%);background:linear-gradient(90deg,rgb(232 200 102 / 10%),rgb(8 12 18 / 92%));opacity:1}
.guild-npc img{width:2.35rem;height:2.35rem;overflow:hidden;border:1px solid var(--ui-color-border-strong);border-radius:var(--ui-radius-sm);object-fit:cover;object-position:50% 16%}
.guild-npc div{display:grid;min-width:0;gap:1px}
.guild-npc strong{overflow:hidden;font-family:var(--ui-font-display);font-size:.65rem;text-overflow:ellipsis;white-space:nowrap}
.guild-npc small,.guild-npc span{overflow:hidden;color:var(--ui-color-text-muted);font-size:.49rem;text-overflow:ellipsis;white-space:nowrap}
.guild-npc span{color:var(--ui-color-gold-muted)}
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

.guild-board__people {
  display: grid;
  grid-template-columns: 1fr;
  gap: 7px;
  padding: 2px 0;
}

.guild-staff-heading,
.contract-board__heading {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: var(--ui-space-3);
}

.guild-staff-heading > div,
.contract-board__heading > div {
  display: grid;
  gap: 2px;
}

.guild-staff-heading small,
.contract-board__heading small {
  color: var(--ui-color-gold);
  font-size: .55rem;
  font-weight: 800;
  letter-spacing: .1em;
}

.guild-staff-heading strong,
.contract-board__heading strong {
  font-family: var(--ui-font-display);
  font-size: .76rem;
}

.guild-staff-heading > span,
.contract-board__heading > span {
  flex: 0 0 auto;
  color: var(--ui-color-text-muted);
  font-size: .56rem;
}

.guild-staff-rail,
.contract-rail,
.contract-filters {
  display: flex;
  gap: 6px;
  overflow-x: auto;
  padding: 2px 1px 5px;
  scrollbar-width: thin;
  scrollbar-color: rgb(232 200 102 / 28%) transparent;
}

.guild-npc {
  display: grid;
  grid-template-columns: 2.65rem minmax(0, 1fr);
  flex: 0 0 9.1rem;
  align-items: center;
  gap: 7px;
  min-height: 3.3rem;
  padding: 5px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: linear-gradient(135deg, rgb(22 25 34 / 94%), rgb(7 10 16 / 94%));
  opacity: .72;
}

.guild-npc--active {
  border-color: rgb(232 200 102 / 58%);
  background: linear-gradient(110deg, rgb(91 66 27 / 28%), rgb(8 12 18 / 96%));
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 5%);
  opacity: 1;
}

.guild-npc__portrait {
  display: grid;
  width: 2.65rem;
  height: 3.25rem;
  place-items: start center;
  overflow: hidden;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-sm);
  background: radial-gradient(circle at 50% 24%, rgb(170 163 255 / 16%), transparent 65%), rgb(4 7 12 / 92%);
}

.guild-npc__portrait img {
  width: 175%;
  height: 175%;
  object-fit: cover;
  object-position: 50% 13%;
  transform: translateY(-2%);
}

.guild-npc div {
  gap: 2px;
}

.guild-npc strong {
  font-size: .68rem;
}

.guild-npc small,
.guild-npc span {
  font-size: .5rem;
}

.contract-board {
  display: grid;
  gap: var(--ui-space-2);
  padding: var(--ui-space-3);
  border: 1px solid rgb(232 200 102 / 24%);
  border-radius: var(--ui-radius-md);
  background:
    linear-gradient(145deg, rgb(232 200 102 / 7%), transparent 42%),
    rgb(6 9 15 / 92%);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 4%);
}

.contract-filters {
  margin-inline: -3px;
  padding-inline: 3px;
}

.contract-filter {
  flex: 0 0 auto;
  min-height: 28px;
  padding: 4px 9px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  background: rgb(12 16 24 / 86%);
  color: var(--ui-color-text-muted);
  font: inherit;
  font-size: .58rem;
  cursor: pointer;
}

.contract-filter--active {
  border-color: rgb(232 200 102 / 58%);
  background: rgb(232 200 102 / 12%);
  color: #f0d98f;
}

.contract-rail {
  margin-inline: -3px;
  padding-inline: 3px;
}

.contract-tab {
  display: grid;
  grid-template-columns: 2rem minmax(7rem, 1fr) auto;
  flex: 0 0 13rem;
  align-items: center;
  gap: 7px;
  min-height: 4.05rem;
  padding: 7px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: rgb(11 15 22 / 92%);
  color: var(--ui-color-text-secondary);
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.contract-tab--selected {
  border-color: rgb(170 163 255 / 64%);
  background: linear-gradient(110deg, rgb(146 136 255 / 14%), rgb(10 14 22 / 96%));
  box-shadow: inset 0 0 0 1px rgb(170 163 255 / 9%);
}

.contract-tab--ready {
  border-color: rgb(79 185 150 / 52%);
}

.contract-tab__seal {
  display: grid;
  width: 2rem;
  height: 2rem;
  place-items: center;
  border: 1px solid rgb(232 200 102 / 32%);
  border-radius: 50%;
  background: rgb(232 200 102 / 8%);
  color: var(--ui-color-gold);
  font-size: .95rem;
}

.contract-tab--ready .contract-tab__seal {
  border-color: rgb(79 185 150 / 48%);
  color: #84d5bb;
}

.contract-tab__copy {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.contract-tab__copy small {
  overflow: hidden;
  color: var(--ui-color-text-muted);
  font-size: .48rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.contract-tab__copy strong {
  overflow: hidden;
  color: var(--ui-color-text-primary);
  font-family: var(--ui-font-display);
  font-size: .68rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.contract-tab__copy em {
  color: var(--ui-color-gold-muted);
  font-size: .49rem;
  font-style: normal;
  font-weight: 800;
  letter-spacing: .04em;
}

.contract-tab > b {
  align-self: start;
  color: var(--ui-color-text-muted);
  font-size: .5rem;
  font-weight: 700;
  white-space: nowrap;
}

.guild-contract {
  margin-top: 2px;
  border-color: rgb(170 163 255 / 28%);
  background: linear-gradient(160deg, rgb(19 23 34 / 100%), rgb(6 9 15 / 100%));
}

.guild-contract__heading h3 {
  font-size: 1.05rem;
}

.guild-board__header h2 {
  max-width: 16rem;
  font-size: clamp(1.15rem, 5vw, 1.45rem);
  line-height: 1.05;
}

.guild-board__header p {
  max-width: 29rem;
  font-size: .64rem;
}

.guild-board__empty--filter {
  padding: var(--ui-space-3);
}

@media (max-width: 520px) {
  .guild-board__header {
    grid-template-columns: 3.9rem minmax(0, 1fr);
  }

  .guild-board__portrait {
    width: 3.9rem;
    height: 4.5rem;
  }

  .contract-tab {
    flex-basis: 12rem;
  }
}
</style>
