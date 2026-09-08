<script setup lang="ts">
import { computed } from 'vue'

import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard } from '@/ui/components'

const session = useGameSessionStore()
const contracts = computed(() => session.snapshot?.world?.contracts ?? [])

function statusLabel(status: string): string {
  if (status === 'COMPLETED') return 'ВЫПОЛНЕНО'
  if (status === 'ACTIVE') return 'ВЗЯТО'
  if (status === 'AVAILABLE') return 'ДОСТУПНО'
  return 'ЗАКРЫТО'
}

async function accept(contractId: string): Promise<void> {
  await session.acceptContract(contractId)
}
</script>

<template>
  <section class="quests-view">
    <header class="quests-view__header">
      <div>
        <small>ПУТЬ ГЕРОЯ</small>
        <h1>Квесты</h1>
      </div>
      <span>{{ contracts.length }}</span>
    </header>

    <UICard v-if="!contracts.length" class="empty-card">
      <strong>Здесь пока тихо</strong>
      <p>Новые задания появятся, когда герой откроет следующие области мира.</p>
    </UICard>

    <UICard v-for="contract in contracts" :key="contract.id" class="quest-card" :data-contract-id="contract.id">
      <div class="quest-card__heading">
        <div>
          <small>{{ statusLabel(contract.status) }} · УР. {{ contract.requiredLevel }}</small>
          <strong>{{ contract.displayName }}</strong>
        </div>
        <span>{{ contract.status }}</span>
      </div>
      <p>{{ contract.description }}</p>
      <div class="quest-card__reward">Награда: +{{ contract.rewardXp }} опыта · +{{ contract.rewardGold }} золота</div>
      <UIButton v-if="contract.status === 'AVAILABLE'" :loading="session.mutationPending" @click="accept(contract.id)">Взять квест</UIButton>
    </UICard>
  </section>
</template>

<style scoped>
.quests-view { display: grid; gap: 12px; min-height: 100%; padding: 14px; }
.quests-view__header { display: flex; align-items: end; justify-content: space-between; }
.quests-view__header small { color: var(--ui-color-primary); font-size: .55rem; letter-spacing: .14em; }
.quests-view__header h1 { margin: 2px 0 0; font-family: var(--ui-font-display); font-size: 1.35rem; }
.quests-view__header > span { color: var(--ui-color-text-muted); font-size: .7rem; }
.empty-card, .quest-card { display: grid; gap: 10px; }
.empty-card p, .quest-card p { margin: 0; color: var(--ui-color-text-muted); font-size: .72rem; line-height: 1.5; }
.quest-card__heading { display: flex; justify-content: space-between; gap: 12px; }
.quest-card__heading > div { display: grid; gap: 4px; }
.quest-card__heading small { color: var(--ui-color-primary); font-size: .55rem; letter-spacing: .08em; }
.quest-card__heading strong { font-family: var(--ui-font-display); font-size: 1rem; }
.quest-card__heading > span { color: var(--ui-color-text-muted); font-size: .56rem; }
.quest-card__reward { color: var(--ui-color-gold); font-size: .68rem; }
</style>
