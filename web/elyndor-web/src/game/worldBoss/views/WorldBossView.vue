<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'

import { itemArtUrl } from '@/assets/itemArt'
import { useWorldBossStore } from '@/game/worldBoss/worldBossStore'
import { UIButton, UILoadingState } from '@/ui/components'

const emit = defineEmits<{ close: [] }>()

const worldBoss = useWorldBossStore()
const now = ref(Date.now())
let clockTimer: number | null = null
let liveTimer: number | null = null

const boss = computed(() => worldBoss.active)
const settlement = computed(() => worldBoss.settlement)
const reward = computed(() => settlement.value?.reward ?? null)
const showingResult = computed(() => settlement.value !== null)

const healthPercent = computed(() => {
  const current = boss.value
  if (!current || current.maxHealth <= 0) return 0
  return Math.max(0, Math.min(100, (current.currentHealth / current.maxHealth) * 100))
})

const remainingTime = computed(() => {
  const expiresAt = boss.value?.expiresAtUtc
  if (!expiresAt) return '--:--'
  const milliseconds = Math.max(0, Date.parse(expiresAt) - now.value)
  const totalSeconds = Math.floor(milliseconds / 1_000)
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
})

const rewardProgress = computed(() => {
  const current = boss.value
  if (!current?.rewardEligible) return 0
  return Math.max(0, Math.min(100, current.rewardPercentile))
})

const rewardTierLabel = computed(() => formatRewardTier(boss.value?.rewardTier ?? null))

const rewardChestLabel = computed(() => {
  const current = boss.value
  if (!current?.rewardEligible) return 'Награда пока не открыта'
  if (current.rewardEnhancedChestCount > 0) {
    return `${current.rewardEnhancedChestCount} усиленных сундука`
  }
  if (current.rewardChestCount === 1) return '1 обычный сундук'
  if (current.rewardChestCount > 1) return `${current.rewardChestCount} обычных сундука`
  return 'Без сундука'
})

const formatter = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 })

function formatNumber(value: number): string {
  return formatter.format(Math.max(0, value))
}

function formatRewardTier(tier: string | null): string {
  if (!tier) return 'Не квалифицирован'
  if (tier === 'Qualified') return 'Участник'
  if (tier === 'Top5') return 'TOP 5'
  const match = /^Top(\d+)$/.exec(tier)
  return match ? `TOP ${match[1]}` : tier
}

function close(): void {
  worldBoss.acknowledgeResult()
  emit('close')
}

async function enterCombat(): Promise<void> {
  await worldBoss.enter()
}

onMounted(() => {
  void worldBoss.refreshLive()
  now.value = Date.now()
  clockTimer = window.setInterval(() => {
    now.value = Date.now()
  }, 1_000)
  liveTimer = window.setInterval(() => {
    if (!showingResult.value) void worldBoss.refreshLive()
  }, 3_000)
})

onUnmounted(() => {
  if (clockTimer !== null) window.clearInterval(clockTimer)
  if (liveTimer !== null) window.clearInterval(liveTimer)
})
</script>

<template>
  <section class="world-boss" data-world-boss-view>
    <header class="world-boss__header">
      <button class="world-boss__back" type="button" aria-label="Назад" @click="close">‹</button>
      <div>
        <small>Глобальное событие</small>
        <strong>Мировой босс</strong>
      </div>
      <span
        class="world-boss__connection"
        :data-state="worldBoss.connectionState"
        :title="worldBoss.connectionState"
      />
    </header>

    <template v-if="showingResult">
      <section class="result-card" data-world-boss-result>
        <p class="result-card__kicker">Сражение завершено</p>
        <h1>{{ boss?.name ?? 'Архон Пепла' }} повержен</h1>
        <p class="result-card__contribution">
          Твой вклад: <strong>{{ formatNumber(settlement?.contribution ?? 0) }} урона</strong>
        </p>

        <template v-if="reward">
          <div class="result-card__placement">
            <div>
              <small>Итоговое место</small>
              <strong>#{{ reward.rank }} / {{ reward.eligibleParticipants }}</strong>
            </div>
            <div>
              <small>Процентиль</small>
              <strong>{{ reward.percentile.toFixed(2) }}%</strong>
            </div>
            <div>
              <small>Категория</small>
              <strong>{{ formatRewardTier(reward.tier) }}</strong>
            </div>
          </div>

          <div class="result-card__rewards">
            <div>
              <span>Опыт</span>
              <strong>+{{ formatNumber(reward.experience) }} XP</strong>
            </div>
            <div>
              <span>Золото</span>
              <strong>+{{ formatNumber(reward.totalGold) }}</strong>
              <small>{{ formatNumber(reward.bossGold) }} босс + {{ formatNumber(reward.chestGold) }} сундуки</small>
            </div>
            <div>
              <span>Сундуки</span>
              <strong v-if="reward.enhancedChestCount > 0">
                {{ reward.enhancedChestCount }} × усиленный
              </strong>
              <strong v-else-if="reward.chestCount > 0">
                {{ reward.chestCount }} × обычный
              </strong>
              <strong v-else>Без сундука</strong>
            </div>
          </div>

          <section v-if="reward.items.length" class="boss-chest">
            <div class="boss-chest__title">
              <span>{{ reward.enhancedChestCount > 0 ? 'Усиленная добыча TOP 5' : 'Сундук Архона Пепла' }}</span>
              <small>Персональная добыча</small>
            </div>
            <article
              v-for="(item, index) in reward.items"
              :key="item.instanceId ?? `${item.itemId}-${index}`"
              class="reward-item"
              :data-rarity="item.rarity.toLowerCase()"
            >
              <div class="reward-item__icon">
                <img v-if="itemArtUrl(item.iconId)" :src="itemArtUrl(item.iconId)" alt="" />
                <span v-else aria-hidden="true">◆</span>
              </div>
              <div>
                <strong>{{ item.name }}</strong>
                <span>{{ item.rarity }}<template v-if="item.quantity > 1"> · ×{{ item.quantity }}</template></span>
                <small>Предмет уже сохранён в инвентарь или ожидающую добычу.</small>
              </div>
            </article>
          </section>
        </template>

        <section v-else class="result-card__ineligible">
          <strong>Награда не начислена</strong>
          <p>В этом убийстве не был достигнут минимальный личный вклад для сундука.</p>
        </section>

        <UIButton class="result-card__close" @click="close">Вернуться в мир</UIButton>
      </section>
    </template>

    <template v-else-if="boss">
      <section class="boss-card">
        <div class="boss-card__topline">
          <span>WORLD BOSS · УР. {{ boss.level }}</span>
          <time>{{ remainingTime }}</time>
        </div>
        <h1>{{ boss.name }}</h1>
        <p>Фаза {{ boss.currentPhase }} · {{ boss.phaseName }}</p>

        <div
          class="boss-health"
          role="progressbar"
          aria-label="Здоровье мирового босса"
          :aria-valuenow="boss.currentHealth"
          :aria-valuemax="boss.maxHealth"
        >
          <div class="boss-health__meta">
            <strong>{{ formatNumber(boss.currentHealth) }}</strong>
            <span>/ {{ formatNumber(boss.maxHealth) }}</span>
            <small>{{ healthPercent.toFixed(1) }}%</small>
          </div>
          <div class="boss-health__track">
            <span :style="{ width: `${healthPercent}%` }" />
          </div>
        </div>

        <div class="boss-card__metrics">
          <div><span>Участники</span><strong>{{ boss.participants }}</strong></div>
          <div><span>Мой урон</span><strong>{{ formatNumber(boss.personalDamage) }}</strong></div>
          <div><span>Урон группы</span><strong>{{ formatNumber(boss.partyDamage) }}</strong></div>
        </div>
      </section>

      <section class="reward-progress">
        <div class="reward-progress__head">
          <div>
            <small>Предварительная категория</small>
            <strong>{{ rewardTierLabel }}</strong>
          </div>
          <div>
            <small>Наградное место</small>
            <strong v-if="boss.personalRewardRank">
              #{{ boss.personalRewardRank }} / {{ boss.eligibleParticipants }}
            </strong>
            <strong v-else>—</strong>
          </div>
        </div>
        <div class="reward-progress__track">
          <span :style="{ width: `${rewardProgress}%` }" />
        </div>
        <p v-if="boss.rewardEligible">
          {{ rewardChestLabel }} · текущий процентиль
          <strong>{{ boss.rewardPercentile.toFixed(2) }}%</strong>.
          Итоговая награда фиксируется после смерти босса.
        </p>
        <p v-else>
          Набери минимальный вклад, чтобы попасть в наградный рейтинг.
        </p>
      </section>

      <section class="leaderboard">
        <header>
          <div>
            <small>Рейтинг события</small>
            <h2>Лидеры по урону</h2>
          </div>
          <span v-if="worldBoss.leaderboard?.personalRank">
            Моё место #{{ worldBoss.leaderboard.personalRank }}
          </span>
        </header>

        <div class="leaderboard__columns">
          <div>
            <h3>Игроки</h3>
            <ol v-if="worldBoss.leaderboard?.players.length">
              <li
                v-for="entry in worldBoss.leaderboard.players.slice(0, 10)"
                :key="entry.characterId"
              >
                <b>#{{ entry.rank }}</b>
                <span>{{ entry.name }}</span>
                <strong>{{ formatNumber(entry.damage) }}</strong>
              </li>
            </ol>
            <p v-else class="leaderboard__empty">Первые удары ещё не нанесены.</p>
          </div>

          <div>
            <h3>Группы</h3>
            <ol v-if="worldBoss.leaderboard?.parties.length">
              <li
                v-for="entry in worldBoss.leaderboard.parties.slice(0, 5)"
                :key="entry.partyId"
              >
                <b>#{{ entry.rank }}</b>
                <span>{{ entry.leaderName }}</span>
                <strong>{{ formatNumber(entry.damage) }}</strong>
              </li>
            </ol>
            <p v-else class="leaderboard__empty">Группы ещё не отметились.</p>
          </div>
        </div>
      </section>

      <p v-if="worldBoss.errorCode" class="world-boss__error">{{ worldBoss.errorCode }}</p>

      <div class="world-boss__action">
        <UIButton
          :loading="worldBoss.entering"
          :disabled="worldBoss.isDefeated || boss.currentHealth <= 0"
          @click="enterCombat"
        >
          В бой
        </UIButton>
      </div>
    </template>

    <UILoadingState
      v-else-if="worldBoss.loading"
      state="loading"
      title="Ищем мирового босса"
      message="Синхронизируем глобальное событие."
    />
    <UILoadingState
      v-else
      state="empty"
      title="Сейчас мирового босса нет"
      message="Когда глобальное событие начнётся, оно появится здесь автоматически."
    >
      <UIButton variant="secondary" @click="close">Назад</UIButton>
    </UILoadingState>
  </section>
</template>

<style scoped>
.world-boss {
  display: grid;
  width: min(100%, 760px);
  min-height: 100%;
  margin-inline: auto;
  gap: var(--ui-space-4);
  padding: var(--ui-space-3) var(--ui-space-4) calc(var(--ui-space-7) + env(safe-area-inset-bottom, 0px));
  color: var(--ui-color-text-primary);
}

.world-boss__header {
  display: grid;
  grid-template-columns: 42px minmax(0, 1fr) 18px;
  align-items: center;
  gap: var(--ui-space-3);
}

.world-boss__header div {
  display: grid;
}

.world-boss__header small,
.boss-card__topline,
.reward-progress small,
.leaderboard header small {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
  letter-spacing: .05em;
  text-transform: uppercase;
}

.world-boss__header strong {
  font: 700 1rem var(--ui-font-display);
}

.world-boss__back {
  width: 42px;
  height: 42px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(255 255 255 / 3%);
  color: var(--ui-color-text-primary);
  font-size: 1.7rem;
}

.world-boss__connection {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--ui-color-text-muted);
}

.world-boss__connection[data-state='connected'] {
  background: #6fbd78;
  box-shadow: 0 0 10px rgb(111 189 120 / 55%);
}

.boss-card,
.reward-progress,
.leaderboard,
.result-card {
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background:
    linear-gradient(180deg, rgb(255 255 255 / 3%), transparent 42%),
    rgb(12 15 22 / 92%);
  box-shadow: var(--ui-shadow-inset), 0 14px 30px rgb(0 0 0 / 20%);
}

.boss-card {
  padding: var(--ui-space-4);
  border-color: color-mix(in srgb, #b85d47 44%, var(--ui-color-border));
  background:
    radial-gradient(circle at 84% 0%, rgb(163 58 46 / 20%), transparent 34%),
    linear-gradient(180deg, rgb(32 19 20 / 95%), rgb(12 15 22 / 96%));
}

.boss-card__topline {
  display: flex;
  justify-content: space-between;
  color: #c58b72;
}

.boss-card__topline time {
  color: #f0d0ad;
  font-variant-numeric: tabular-nums;
  font-weight: 800;
}

.boss-card h1,
.result-card h1 {
  margin: var(--ui-space-2) 0 0;
  color: #f1d9c1;
  font: 800 clamp(1.55rem, 7vw, 2.25rem) var(--ui-font-display);
}

.boss-card > p {
  margin: 4px 0 var(--ui-space-4);
  color: var(--ui-color-text-secondary);
}

.boss-health {
  display: grid;
  gap: var(--ui-space-2);
}

.boss-health__meta {
  display: flex;
  align-items: baseline;
  gap: 5px;
  font-variant-numeric: tabular-nums;
}

.boss-health__meta strong {
  color: #f0ddd5;
  font-size: 1.05rem;
}

.boss-health__meta span,
.boss-health__meta small {
  color: var(--ui-color-text-muted);
}

.boss-health__meta small {
  margin-left: auto;
}

.boss-health__track,
.reward-progress__track {
  height: 13px;
  overflow: hidden;
  border: 1px solid rgb(255 255 255 / 7%);
  border-radius: var(--ui-radius-round);
  background: rgb(0 0 0 / 52%);
  box-shadow: inset 0 2px 5px rgb(0 0 0 / 65%);
}

.boss-health__track span,
.reward-progress__track span {
  display: block;
  height: 100%;
  border-radius: inherit;
  transition: width var(--ui-transition-normal);
}

.boss-health__track span {
  background:
    linear-gradient(180deg, rgb(255 255 255 / 16%), transparent 50%),
    linear-gradient(90deg, #742a2d, #bc4d3e);
}

.boss-card__metrics {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--ui-space-2);
  margin-top: var(--ui-space-4);
}

.boss-card__metrics div {
  display: grid;
  gap: 3px;
  padding: var(--ui-space-2);
  border: 1px solid rgb(255 255 255 / 5%);
  border-radius: var(--ui-radius-md);
  background: rgb(0 0 0 / 18%);
}

.boss-card__metrics span {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.boss-card__metrics strong {
  font-variant-numeric: tabular-nums;
}

.reward-progress,
.leaderboard {
  padding: var(--ui-space-4);
}

.reward-progress__head {
  display: flex;
  justify-content: space-between;
  gap: var(--ui-space-4);
  margin-bottom: var(--ui-space-3);
}

.reward-progress__head > div {
  display: grid;
}

.reward-progress__head > div:last-child {
  text-align: right;
}

.reward-progress__head strong {
  color: var(--ui-color-gold);
  font: 700 1rem var(--ui-font-display);
}

.reward-progress__track {
  height: 8px;
}

.reward-progress__track span {
  background: linear-gradient(90deg, #8a6739, #d0a35b);
}

.reward-progress p {
  margin: var(--ui-space-2) 0 0;
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-sm);
}

.leaderboard header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: var(--ui-space-3);
  margin-bottom: var(--ui-space-3);
}

.leaderboard h2,
.leaderboard h3 {
  margin: 0;
  font-family: var(--ui-font-display);
}

.leaderboard h2 {
  margin-top: 2px;
  font-size: 1.02rem;
}

.leaderboard header > span {
  color: var(--ui-color-gold);
  font-size: var(--ui-font-size-xs);
}

.leaderboard__columns {
  display: grid;
  grid-template-columns: 1.2fr .8fr;
  gap: var(--ui-space-4);
}

.leaderboard h3 {
  margin-bottom: var(--ui-space-2);
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-sm);
}

.leaderboard ol {
  display: grid;
  margin: 0;
  padding: 0;
  list-style: none;
}

.leaderboard li {
  display: grid;
  grid-template-columns: 34px minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-2);
  min-height: 34px;
  border-bottom: 1px solid rgb(255 255 255 / 5%);
  font-size: var(--ui-font-size-sm);
}

.leaderboard li b {
  color: var(--ui-color-text-muted);
}

.leaderboard li span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.leaderboard li strong {
  font-variant-numeric: tabular-nums;
}

.leaderboard__empty {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
}

.world-boss__action {
  position: sticky;
  z-index: 4;
  bottom: var(--ui-space-3);
  display: flex;
  justify-content: center;
  padding: var(--ui-space-2);
  background: linear-gradient(180deg, transparent, rgb(8 10 15 / 92%) 36%);
}

.world-boss__action :deep(.ui-button) {
  width: min(100%, 420px);
  min-height: 50px;
  font: 800 1rem var(--ui-font-display);
}

.world-boss__error {
  margin: 0;
  color: var(--ui-color-danger);
  font-size: var(--ui-font-size-sm);
  text-align: center;
}

.result-card {
  display: grid;
  gap: var(--ui-space-4);
  padding: clamp(18px, 5vw, 30px);
  border-color: color-mix(in srgb, var(--ui-color-gold) 42%, var(--ui-color-border));
  background:
    radial-gradient(circle at 50% 0%, rgb(179 126 60 / 15%), transparent 32%),
    rgb(12 15 22 / 96%);
}

.result-card__kicker {
  margin: 0 0 calc(var(--ui-space-3) * -1);
  color: var(--ui-color-gold);
  font-size: var(--ui-font-size-xs);
  font-weight: 800;
  letter-spacing: .08em;
  text-transform: uppercase;
}

.result-card__contribution {
  margin: 0;
  color: var(--ui-color-text-secondary);
}

.result-card__placement,
.result-card__rewards {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--ui-space-2);
}

.result-card__placement > div {
  display: grid;
  gap: 2px;
  padding: var(--ui-space-3);
  border: 1px solid rgb(255 255 255 / 6%);
  border-radius: var(--ui-radius-md);
  background: rgb(0 0 0 / 20%);
}

.result-card__placement small {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.result-card__placement strong {
  color: var(--ui-color-gold);
  font-size: .95rem;
}

.result-card__rewards > div {
  display: grid;
  gap: 2px;
  padding: var(--ui-space-3);
  border: 1px solid rgb(255 255 255 / 6%);
  border-radius: var(--ui-radius-md);
  background: rgb(0 0 0 / 20%);
}

.result-card__rewards span,
.result-card__rewards small {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.result-card__rewards strong {
  color: #f0d6ad;
  font-size: .95rem;
}

.boss-chest {
  display: grid;
  gap: var(--ui-space-3);
}

.boss-chest__title {
  display: flex;
  justify-content: space-between;
  gap: var(--ui-space-3);
}

.boss-chest__title span {
  font: 700 1rem var(--ui-font-display);
}

.boss-chest__title small {
  color: var(--ui-color-text-muted);
}

.reward-item {
  display: grid;
  grid-template-columns: 58px minmax(0, 1fr);
  gap: var(--ui-space-3);
  align-items: center;
  padding: var(--ui-space-3);
  border: 1px solid rgb(255 255 255 / 8%);
  border-radius: var(--ui-radius-md);
  background: rgb(0 0 0 / 24%);
}

.reward-item[data-rarity='epic'] { border-color: rgb(166 92 214 / 42%); }
.reward-item[data-rarity='legendary'],
.reward-item[data-rarity='unique'] { border-color: rgb(216 151 60 / 52%); }

.reward-item__icon {
  display: grid;
  width: 56px;
  height: 56px;
  place-items: center;
  overflow: hidden;
  border: 1px solid rgb(255 255 255 / 12%);
  border-radius: var(--ui-radius-md);
  background: #0b0d12;
  color: var(--ui-color-gold);
}

.reward-item__icon img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.reward-item > div:last-child {
  display: grid;
  gap: 2px;
}

.reward-item strong {
  color: #e9d7c3;
}

.reward-item span,
.reward-item small {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.result-card__ineligible {
  padding: var(--ui-space-4);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(0 0 0 / 20%);
}

.result-card__ineligible p {
  margin-bottom: 0;
  color: var(--ui-color-text-secondary);
}

.result-card__close {
  width: 100%;
}

@media (max-width: 560px) {
  .world-boss {
    padding-inline: var(--ui-space-3);
  }

  .boss-card__metrics,
  .result-card__rewards {
    grid-template-columns: 1fr;
  }

  .boss-card__metrics {
    grid-template-columns: repeat(3, 1fr);
  }

  .leaderboard__columns {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 390px) {
  .boss-card__metrics {
    grid-template-columns: 1fr;
  }

  .boss-card__metrics div {
    grid-template-columns: 1fr auto;
  }
}
</style>
