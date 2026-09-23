<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, watch } from 'vue'

import type { CombatEvent } from '@/api/contracts'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'

interface PreviewEntry {
  key: number
  side: 'player' | 'ally' | 'enemy' | 'system'
  actor: string
  text: string
}

const combat = useCombatSessionStore()
const session = useGameSessionStore()
const targetReady = ref(false)
let targetFrame: number | null = null

const enemies = computed(() => combat.snapshot?.enemies ?? (combat.snapshot ? [combat.snapshot.enemy] : []))
const players = computed(() => combat.snapshot?.players ?? (combat.snapshot ? [combat.snapshot.player] : []))

function abilityName(id: string | null | undefined): string {
  if (!id) return ''
  const abilities = [
    ...(combat.snapshot?.player.abilities ?? []),
    ...(combat.snapshot?.enemy.abilities ?? []),
  ]
  const ability = abilities.find(candidate => candidate.id === id)
  if (ability) return ability.displayName
  if (id === 'AUTO_ATTACK') return 'Автоатака'
  const item = session.snapshot?.character?.inventory.items.find(candidate => candidate.definitionId === id)
  return item?.name ?? ''
}

function eventSide(event: CombatEvent): PreviewEntry['side'] {
  const snapshot = combat.snapshot
  if (!snapshot) return 'system'
  const source = event.sourceActorId ?? event.actorId
  if (source === snapshot.player.actorId) return 'player'
  if (snapshot.companion?.actorId === source) return 'ally'
  if (enemies.value.some(enemy => enemy.actorId === source)) return 'enemy'
  return 'system'
}

function actorLabel(side: PreviewEntry['side'], event: CombatEvent): string {
  if (side === 'player') return 'ВЫ'
  if (side === 'ally') return combat.snapshot?.companion?.name ?? 'СОЮЗНИК'
  if (side === 'enemy') {
    const source = event.sourceActorId ?? event.actorId
    return enemies.value.find(enemy => enemy.actorId === source)?.name ?? 'ВРАГ'
  }
  return 'БОЙ'
}

function eventText(event: CombatEvent, critical = false): string | null {
  const definition = abilityName(event.definitionId)
  const targetEnemy = enemies.value.find(enemy => enemy.actorId === (event.targetActorId ?? event.actorId))
  switch (event.type) {
    case 'DamageDealt':
      return `${definition || 'Атака'} · ${Math.round(event.amount)} урона${critical ? ' · КРИТ' : ''}`
    case 'DamageBlocked':
      return event.amountBeforeShields <= 0 ? 'Полный блок' : `Блок −${Math.round(event.amount)}`
    case 'HealingApplied':
      return `+${Math.round(event.amount)} здоровья`
    case 'EffectApplied':
      return definition ? `Эффект · ${definition}` : 'Наложен эффект'
    case 'EffectRefreshed':
      return definition ? `Обновлён · ${definition}` : 'Эффект обновлён'
    case 'ConsumableUsed':
      return definition ? `Использовано · ${definition}` : 'Расходник использован'
    case 'TauntApplied':
      return definition ? `Провокация · ${definition}` : 'Провокация'
    case 'EnemyKilled':
      return `${targetEnemy?.name ?? 'Противник'} повержен`
    case 'ActorDied': {
      const deadEnemy = enemies.value.find(enemy => enemy.actorId === event.actorId)
      if (deadEnemy) return `${deadEnemy.name} повержен`
      const deadPlayer = players.value.find(player => player.actorId === event.actorId)
      return deadPlayer ? `${deadPlayer.name} пал` : 'Участник боя пал'
    }
    case 'AutoAttackStarted':
      return 'Автоатака включена'
    case 'AutoAttackStopped':
      return 'Автоатака остановлена'
    case 'CombatStarted':
      return 'Бой начался'
    case 'CombatEnded':
      return event.definitionId === 'Victory'
        ? 'Победа'
        : event.definitionId === 'Defeat'
          ? 'Поражение'
          : event.definitionId === 'FLED'
            ? 'Вы сбежали из боя'
            : 'Бой завершён'
    default:
      return null
  }
}

function isCriticalDamage(index: number, event: CombatEvent): boolean {
  if (event.type !== 'DamageDealt' || index <= 0) return false
  const previous = combat.events[index - 1]
  return previous?.type === 'CriticalHit'
    && previous.sourceActorId === event.sourceActorId
    && previous.targetActorId === event.targetActorId
    && previous.amount === event.amount
}

const previewEntries = computed<PreviewEntry[]>(() => {
  const entries: PreviewEntry[] = []
  for (let index = combat.events.length - 1; index >= 0 && entries.length < 3; index -= 1) {
    const event = combat.events[index]
    if (!event) continue
    const text = eventText(event, isCriticalDamage(index, event))
    if (!text) continue
    const side = eventSide(event)
    entries.push({
      key: event.sequence,
      side,
      actor: actorLabel(side, event),
      text,
    })
  }
  return entries
})

function cancelTargetFrame(): void {
  if (targetFrame === null) return
  window.cancelAnimationFrame(targetFrame)
  targetFrame = null
}

async function syncTarget(active: boolean): Promise<void> {
  cancelTargetFrame()
  targetReady.value = false
  if (!active) return

  await nextTick()
  let attempts = 0
  const findTarget = (): void => {
    targetReady.value = document.querySelector('.combat-log__toggle > span') !== null
    if (targetReady.value || attempts >= 20) {
      targetFrame = null
      return
    }
    attempts += 1
    targetFrame = window.requestAnimationFrame(findTarget)
  }
  findTarget()
}

watch(
  [() => combat.snapshot?.sessionId ?? null, () => combat.isActive],
  ([sessionId, active]) => void syncTarget(Boolean(sessionId) && active),
  { immediate: true },
)

onUnmounted(() => {
  cancelTargetFrame()
  targetReady.value = false
})
</script>

<template>
  <Teleport v-if="targetReady" to=".combat-log__toggle > span">
    <span class="combat-log-preview" data-combat-log-preview aria-hidden="true">
      <span v-if="!previewEntries.length" class="combat-log-preview__empty">Событий пока нет</span>
      <span
        v-for="entry in previewEntries"
        :key="entry.key"
        class="combat-log-preview__entry"
        :data-side="entry.side"
      >
        <b>{{ entry.actor }}</b>
        <span>{{ entry.text }}</span>
      </span>
    </span>
  </Teleport>
</template>

<style scoped>
:global(.combat-log__toggle > span > small) {
  display: none;
}

.combat-log-preview {
  display: grid;
  min-width: 0;
  gap: 2px;
  margin-top: 2px;
}

.combat-log-preview__empty {
  overflow: hidden;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-log-preview__entry {
  display: grid;
  min-width: 0;
  grid-template-columns: 2.6rem minmax(0, 1fr);
  align-items: baseline;
  gap: 5px;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
  line-height: 1.2;
}

.combat-log-preview__entry > b {
  overflow: hidden;
  color: var(--ui-color-text-muted);
  font-size: .46rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-log-preview__entry > span {
  overflow: hidden;
  color: var(--ui-color-text-secondary);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-log-preview__entry[data-side='player'] > b { color: #b9b2ff; }
.combat-log-preview__entry[data-side='ally'] > b { color: #9be2c9; }
.combat-log-preview__entry[data-side='enemy'] > b { color: #efa1ae; }
</style>