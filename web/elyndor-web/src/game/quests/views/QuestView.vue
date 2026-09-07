<script setup lang="ts">
import { computed } from 'vue'

import type { WorldContract } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UICard } from '@/ui/components'

const session = useGameSessionStore()

const activeContracts = computed(() =>
  (session.snapshot?.world?.contracts ?? []).filter(contract => contract.status === 'ACTIVE'),
)
const completedContracts = computed(() =>
  (session.snapshot?.world?.contracts ?? []).filter(contract => contract.status === 'COMPLETED'),
)

function monsterName(id: string): string {
  if (id === 'SPIDER_BROODMOTHER_L14') return 'Паучья Прародительница'
  return id.replaceAll('_', ' ')
}

function locationName(id: string): string {
  if (id === 'BLIGHTED_GROVE') return 'Осквернённая чаща'
  if (id === 'BROODMOTHER_LAIR') return 'Логово Прародительницы'
  return id.replaceAll('_', ' ')
}

function statusLabel(contract: WorldContract): string {
  return contract.status === 'COMPLETED' ? 'Выполнен' : 'Активен'
}
</script>

<template>
  <section class="quests" data-quest-view>
    <header class="quests__header">
      <div>
        <small>ЖУРНАЛ ЗАДАНИЙ</small>
        <h1>Квесты</h1>
      </div>
      <strong>{{ activeContracts.length }}</strong>
    </header>

    <section class="quests__section">
      <div class="quests__section-title">
        <span>Активные</span>
        <b>{{ activeContracts.length }}</b>
      </div>

      <div v-if="activeContracts.length" class="quests__list">
        <UICard
          v-for="contract in activeContracts"
          :key="contract.id"
          class="quest-card"
          :data-quest-id="contract.id"
          data-quest-status="ACTIVE"
        >
          <div class="quest-card__status">АКТИВЕН</div>
          <h2>{{ contract.displayName }}</h2>
          <p>{{ contract.description }}</p>
          <dl>
            <div>
              <dt>Цель</dt>
              <dd>Убейте: {{ monsterName(contract.targetMonsterId) }}</dd>
            </div>
            <div>
              <dt>Награда</dt>
              <dd>+{{ contract.rewardXp }} опыта · +{{ contract.rewardGold }} золота</dd>
            </div>
            <div>
              <dt>Открывает</dt>
              <dd>{{ locationName(contract.unlockLocationId) }}</dd>
            </div>
          </dl>
        </UICard>
      </div>

      <UICard v-else class="quests__empty">
        <strong>Нет активных заданий</strong>
        <p>Принятые контракты автоматически появятся здесь.</p>
      </UICard>
    </section>

    <section v-if="completedContracts.length" class="quests__section">
      <div class="quests__section-title">
        <span>Завершённые</span>
        <b>{{ completedContracts.length }}</b>
      </div>
      <div class="quests__list">
        <UICard
          v-for="contract in completedContracts"
          :key="contract.id"
          class="quest-card quest-card--completed"
          :data-quest-id="contract.id"
          data-quest-status="COMPLETED"
        >
          <div class="quest-card__status">ВЫПОЛНЕН</div>
          <h2>{{ contract.displayName }}</h2>
          <p>{{ statusLabel(contract) }} · {{ contract.description }}</p>
          <strong>Путь открыт: {{ locationName(contract.unlockLocationId) }}</strong>
        </UICard>
      </div>
    </section>
  </section>
</template>

<style scoped>
.quests{display:grid;gap:var(--ui-space-4);width:min(100%,var(--ui-content-width));margin-inline:auto;padding:var(--ui-space-4);padding-bottom:var(--ui-space-7)}
.quests__header,.quests__section-title{display:flex;align-items:center;justify-content:space-between;gap:var(--ui-space-3)}
.quests__header small,.quests__section-title span{color:var(--ui-color-primary);font-size:var(--ui-font-size-xs);font-weight:700;letter-spacing:.08em}
.quests__header h1{margin:.15rem 0 0;font-family:var(--ui-font-display)}
.quests__header>strong,.quests__section-title b{min-width:2rem;padding:.25rem .5rem;border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-round);text-align:center}
.quests__section{display:grid;gap:var(--ui-space-2)}.quests__list{display:grid;gap:var(--ui-space-3)}
.quest-card{display:grid;gap:var(--ui-space-2)}.quest-card h2,.quest-card p{margin:0}.quest-card h2{font-family:var(--ui-font-display);font-size:var(--ui-font-size-lg)}
.quest-card__status{width:max-content;padding:.2rem .45rem;border:1px solid color-mix(in srgb,var(--ui-color-primary) 40%,transparent);border-radius:var(--ui-radius-round);color:var(--ui-color-primary);font-size:.62rem;font-weight:800}
.quest-card dl{display:grid;gap:var(--ui-space-2);margin:var(--ui-space-2) 0 0}.quest-card dl div{display:flex;justify-content:space-between;gap:var(--ui-space-3);padding-top:var(--ui-space-2);border-top:1px solid var(--ui-color-border)}dt{color:var(--ui-color-text-muted)}dd{margin:0;text-align:right}.quest-card--completed{opacity:.76}.quests__empty{text-align:center}.quests__empty p{margin-bottom:0;color:var(--ui-color-text-muted)}
</style>
