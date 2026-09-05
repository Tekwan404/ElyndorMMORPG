<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'

import type { CombatAbility, CombatEvent } from '@/api/contracts'
import { abilityArtUrl } from '@/assets/abilityArt'
import { monsterArtUrl } from '@/assets/monsterArt'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIHealthBar } from '@/ui/components'

const emit = defineEmits<{ leave: [] }>()
const combat = useCombatSessionStore()
const session = useGameSessionStore()
const now = ref(Date.now())
const timer = window.setInterval(() => (now.value = Date.now()), 250)
const snapshot = computed(() => combat.snapshot)

type LogSide = 'player' | 'enemy' | 'system'
interface CombatLogEntry { key: number; side: LogSide; actor: string; text: string; detail?: string }
interface EnemyPresentation { name: string; level: number; art?: string }

const TRAINING_DUMMY_ID = 'TRAINING_DUMMY'

const enemyPresentation = computed<EnemyPresentation | null>(() => {
  const enemy = snapshot.value?.enemy
  if (!enemy) return null
  const encounter = combat.encounterPresentation
  const matchesEncounter = encounter?.monsterId === enemy.definitionId
  const artId = enemy.artId ?? (matchesEncounter ? encounter.artId : null)
  return {
    name: enemy.name,
    level: enemy.level ?? (matchesEncounter ? encounter.level : 1),
    art: monsterArtUrl(artId),
  }
})
const displayAbilities = computed(() => snapshot.value?.player.abilities ?? [])
const abilityById = computed(() => new Map<string, CombatAbility>(
  [
    ...(snapshot.value?.player.abilities ?? []),
    ...(snapshot.value?.enemy.abilities ?? []),
  ].map((ability) => [ability.id, ability]),
))
const healingPotion = computed(() => session.snapshot?.character?.inventory.items.find((item) => item.definitionId === 'SMALL_HEALING_POTION') ?? null)
const resourceName = computed(() => snapshot.value?.player.resourceType === 'MANA' ? 'Мана' : snapshot.value?.player.resourceType === 'FOCUS' ? 'Фокус' : 'Ярость')
const resourceTone = computed<'rage' | 'focus' | 'mana'>(() => snapshot.value?.player.resourceType === 'MANA' ? 'mana' : snapshot.value?.player.resourceType === 'FOCUS' ? 'focus' : 'rage')
const fireballStreak = computed(() => snapshot.value?.player.effects.find((effect) => effect.id === 'PYRO_FIREBALL_STREAK')?.stacks ?? 0)
const heatLimit = computed(() => snapshot.value?.player.effects.find((effect) => effect.id === 'PYRO_HEAT_LIMIT') ?? null)
const combustion = computed(() => snapshot.value?.player.effects.find((effect) => effect.id === 'PYRO_COMBUSTION') ?? null)
const targetBurn = computed(() => snapshot.value?.enemy.effects.find((effect) => effect.id === 'PYRO_BURN') ?? null)
const isMage = computed(() => snapshot.value?.player.definitionId === 'MAGE')
const isTraining = computed(() => snapshot.value?.enemy.definitionId === TRAINING_DUMMY_ID)
const trainingElapsedSeconds = computed(() => {
  const startedAt = combat.trainingStats.startedAtUtc
  if (!startedAt) return 0
  return Math.max(0, (now.value - Date.parse(startedAt)) / 1_000)
})
const trainingDps = computed(() => trainingElapsedSeconds.value > 0
  ? combat.trainingStats.totalDamage / trainingElapsedSeconds.value
  : 0)

const logEntries = computed<CombatLogEntry[]>(() => {
  const events = combat.events.slice(-40)
  const entries: CombatLogEntry[] = []
  const consumedResources = new Set<number>()

  for (let index = 0; index < events.length; index += 1) {
    const event = events[index]!
    if (consumedResources.has(event.sequence)) continue
    if (['AbilityStarted', 'AbilityCompleted', 'CriticalHit'].includes(event.type)) continue
    if (event.type === 'ResourceChanged') continue

    if (event.type === 'AbilityUsed') {
      const hasOutcome = events.slice(Math.max(0, index - 6), index).some((candidate) =>
        candidate.definitionId === event.definitionId
        && candidate.sourceActorId === event.sourceActorId
        && ['DamageDealt', 'EffectApplied', 'HealingApplied', 'TauntApplied'].includes(candidate.type),
      )
      if (hasOutcome) continue
    }

    const previous = index > 0 ? events[index - 1] : undefined
    const critical = event.type === 'DamageDealt'
      && previous?.type === 'CriticalHit'
      && previous.sourceActorId === event.sourceActorId
      && previous.targetActorId === event.targetActorId
      && previous.amount === event.amount

    const resourceEvent = event.type === 'DamageDealt'
      ? events.slice(index + 1, Math.min(events.length, index + 6)).find((candidate) =>
          candidate.type === 'ResourceChanged'
          && candidate.amount !== 0
          && candidate.serverTimeUtc === event.serverTimeUtc
          && !consumedResources.has(candidate.sequence),
        )
      : undefined
    if (resourceEvent) consumedResources.add(resourceEvent.sequence)

    const side = eventSide(event)
    entries.push({
      key: event.sequence,
      side,
      actor: actorLabel(side),
      text: eventText(event, critical),
      detail: resourceEvent ? `${resourceEvent.amount > 0 ? '+' : ''}${Math.round(resourceEvent.amount * 10) / 10} ${resourceName.value.toLowerCase()} · ${abilityName(resourceEvent.definitionId)}` : undefined,
    })
  }
  return entries.slice(-12).reverse()
})

function cooldownRemaining(abilityId: string): number {
  const readyAt = snapshot.value?.player.cooldowns[abilityId]
  return readyAt ? Math.max(0, (Date.parse(readyAt) - now.value) / 1_000) : 0
}

function effectRemaining(expiresAtUtc: string): number {
  return Math.max(0, (Date.parse(expiresAtUtc) - now.value) / 1_000)
}

function abilityName(id: string | null | undefined): string {
  if (!id) return ''
  const ability = abilityById.value.get(id)
  if (ability) return ability.displayName
  if (id === 'AUTO_ATTACK') return 'Автоатака'
  if (id === 'DIRECT_DAMAGE_TAKEN') return 'Получение урона'
  if (id === 'COMBAT_REGEN') return 'Регенерация'
  if (id === 'SMALL_HEALING_POTION') return 'Малое зелье лечения'
  if (id === 'PYRO_BURN') return 'Горение'
  if (id === 'PYRO_COMET_AFTERSHOCK') return 'Кометный удар'
  return id.split('_').join(' ')
}

function abilityIcon(ability: CombatAbility): string | undefined {
  return abilityArtUrl(ability.iconId)
}

function eventSide(event: CombatEvent): LogSide {
  const current = snapshot.value
  if (!current || ['CombatStarted', 'CombatEnded', 'ActorDied', 'EnemyKilled'].includes(event.type)) return 'system'
  const source = event.sourceActorId ?? event.actorId
  if (source === current.player.actorId) return 'player'
  if (source === current.enemy.actorId) return 'enemy'
  return 'system'
}

function actorLabel(side: LogSide): string {
  if (side === 'player') return 'ВЫ'
  if (side === 'enemy') return enemyPresentation.value?.name.toUpperCase() ?? 'ВРАГ'
  return 'СИСТЕМА'
}

function eventText(event: CombatEvent, critical = false): string {
  const definition = abilityName(event.definitionId)
  const enemyName = enemyPresentation.value?.name ?? 'Противник'
  switch (event.type) {
    case 'CombatStarted': return isTraining.value ? 'Тренировка началась' : `Бой с ${enemyName} начался`
    case 'AutoAttackStarted': return 'Автоатака включена'
    case 'AutoAttackStopped': return 'Автоатака остановлена'
    case 'DamageDealt': return `${definition || 'Атака'} · ${Math.round(event.amount)} урона${critical ? ' · КРИТ!' : ''}`
    case 'AbilityUsed': return `${definition} · действие выполнено`
    case 'EffectApplied': return `Наложен эффект «${definition}»`
    case 'EffectRefreshed': return `Обновлён эффект «${definition}»`
    case 'HealingApplied': return `Восстановлено ${Math.round(event.amount)} здоровья`
    case 'ConsumableUsed': return `${definition} · +${Math.round(event.amount)} здоровья`
    case 'TauntApplied': return `Провокация · ${definition}`
    case 'ActorDied': return event.actorId === snapshot.value?.enemy.actorId ? `${enemyName} повержен` : 'Вы повержены'
    case 'EnemyKilled': return `${enemyName} повержен`
    case 'CombatEnded': return event.definitionId === 'Victory' ? 'Победа' : event.definitionId === 'Defeat' ? 'Поражение' : isTraining.value ? 'Тренировка завершена' : 'Бой завершён'
    default: return definition ? `Событие · ${definition}` : 'Событие боя'
  }
}

async function usePotion(): Promise<void> {
  if (!healingPotion.value || !snapshot.value || isTraining.value) return
  await combat.useConsumable(healingPotion.value.definitionId)
  await session.refreshSnapshot()
}

async function resetTrainingCombat(): Promise<void> {
  if (!isTraining.value) return
  await combat.resetTraining()
}

async function leaveCombat(): Promise<void> {
  const left = await combat.leave()
  if (left) emit('leave')
}

onUnmounted(() => window.clearInterval(timer))
</script>

<template>
  <section class="combat-screen">
    <template v-if="snapshot && enemyPresentation">
      <section class="enemy-stage" :class="{ 'enemy-stage--training': isTraining }">
        <div class="enemy-heading">
          <div>
            <small>{{ isTraining ? 'ТРЕНИРОВОЧНАЯ ЦЕЛЬ' : `ПРОТИВНИК · УР. ${enemyPresentation.level}` }}</small>
            <h1>{{ enemyPresentation.name }}</h1>
          </div>
          <span v-if="targetBurn" class="burn">🔥 Горение · {{ effectRemaining(targetBurn.expiresAtUtc).toFixed(1) }}с</span>
        </div>
        <div class="enemy-portrait">
          <img v-if="enemyPresentation.art" :src="enemyPresentation.art" :alt="enemyPresentation.name" />
          <div v-else-if="isTraining" class="training-dummy" role="img" aria-label="Тренировочный манекен"><span>✦</span><b>ЦЕЛЬ</b></div>
          <div v-else class="enemy-placeholder" role="img" :aria-label="enemyPresentation.name">⚔</div>
        </div>
        <UIHealthBar :label="`${enemyPresentation.name} · ${Math.ceil(snapshot.enemy.hp)} / ${Math.ceil(snapshot.enemy.maxHp)}`" :value="snapshot.enemy.hp" :max="snapshot.enemy.maxHp" />
        <p v-if="isTraining" class="training-note">Манекен не может умереть: на 1 HP можно проверять execute-механики.</p>
      </section>

      <section v-if="isTraining" class="training-stats" aria-label="Статистика тренировки">
        <div><small>ВРЕМЯ</small><strong>{{ trainingElapsedSeconds.toFixed(1) }}с</strong></div>
        <div><small>УРОН</small><strong>{{ Math.round(combat.trainingStats.totalDamage).toLocaleString('ru-RU') }}</strong></div>
        <div><small>DPS</small><strong>{{ Math.round(trainingDps).toLocaleString('ru-RU') }}</strong></div>
        <div><small>КРИТЫ</small><strong>{{ combat.trainingStats.criticalHits }}</strong></div>
        <div><small>МАКС. УДАР</small><strong>{{ Math.round(combat.trainingStats.maxHit).toLocaleString('ru-RU') }}</strong></div>
      </section>

      <section class="player-panel">
        <div class="player-heading"><div><small>ВАШ ПЕРСОНАЖ</small><strong>{{ snapshot.player.name }}</strong></div><span :class="{ active: snapshot.player.autoAttackEnabled }">{{ snapshot.player.autoAttackEnabled ? 'Автоатака включена' : 'Автоатака выключена' }}</span></div>
        <UIHealthBar label="Здоровье" :value="snapshot.player.hp" :max="snapshot.player.maxHp" />
        <UIHealthBar :label="resourceName" :tone="resourceTone" :value="snapshot.player.resource" :max="snapshot.player.maxResource" />

        <div v-if="isMage" class="pyro-state" aria-label="Состояние пироманта">
          <span>Fireball Crit <b>{{ fireballStreak }}/3</b></span>
          <span v-if="heatLimit" class="hot">ПРЕДЕЛ ЖАРА · {{ effectRemaining(heatLimit.expiresAtUtc).toFixed(1) }}с</span>
          <span v-if="combustion" class="hot">ВОЗГОРАНИЕ · {{ effectRemaining(combustion.expiresAtUtc).toFixed(1) }}с</span>
        </div>

        <div class="abilities">
          <button v-for="ability in displayAbilities" :key="ability.id" type="button" :class="{ 'ability--comet': ability.id === 'FIRE_COMET' }" :disabled="combat.pending || cooldownRemaining(ability.id) > 0 || snapshot.player.resource < ability.resourceCost" @click="combat.useAbility(ability.id)">
            <span class="ability-icon"><img v-if="abilityIcon(ability)" :src="abilityIcon(ability)" :alt="ability.displayName" /><b v-else>{{ ability.displayName.slice(0,2) }}</b></span>
            <span><strong>{{ ability.displayName }}</strong><small>{{ cooldownRemaining(ability.id) > 0 ? `${Math.ceil(cooldownRemaining(ability.id))} сек.` : ability.resourceCost > 0 ? `${Math.round(ability.resourceCost * 10) / 10} ${resourceName.toLowerCase()}` : 'Без затрат' }}</small></span>
          </button>
        </div>

        <div v-if="!isTraining" class="consumables"><button type="button" :disabled="combat.pending || !healingPotion || snapshot.player.hp >= snapshot.player.maxHp" @click="usePotion"><span>✚</span><div><strong>Малое зелье лечения</strong><small v-if="healingPotion">+{{ healingPotion.healAmount }} здоровья · в рюкзаке {{ healingPotion.quantity }}</small><small v-else>В рюкзаке нет зелий</small></div></button></div>
        <div class="controls">
          <UIButton variant="secondary" :disabled="combat.pending" @click="combat.toggleAutoAttack">{{ snapshot.player.autoAttackEnabled ? 'Остановить автоатаку' : 'Включить автоатаку' }}</UIButton>
          <UIButton v-if="isTraining" variant="secondary" :disabled="combat.pending" @click="resetTrainingCombat">Сбросить тренировку</UIButton>
          <UIButton variant="ghost" :disabled="combat.pending" @click="leaveCombat">{{ isTraining ? 'Завершить тренировку' : 'Покинуть бой' }}</UIButton>
        </div>
      </section>

      <section class="combat-log"><header><b>Журнал боя</b><small>Последние действия</small></header><ol><li v-for="entry in logEntries" :key="entry.key" :data-side="entry.side"><span class="actor">{{ entry.actor }}</span><div><strong>{{ entry.text }}</strong><small v-if="entry.detail">↳ {{ entry.detail }}</small></div></li></ol></section>
      <p v-if="combat.errorCode" class="error">Не удалось выполнить действие: {{ combat.errorCode }}</p>
    </template>
    <div v-else class="missing"><h1>Бой прерван</h1><UIButton @click="emit('leave')">Вернуться в мир</UIButton></div>
  </section>
</template>

<style scoped>
.combat-screen {
  display: grid;
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4) var(--ui-space-6);
}

.enemy-stage,
.player-panel,
.combat-log,
.training-stats {
  display: grid;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background: var(--ui-gradient-panel);
  box-shadow: var(--ui-shadow-inset);
}

.enemy-stage {
  position: relative;
  overflow: hidden;
  border-color: color-mix(in srgb, var(--ui-color-danger) 28%, var(--ui-color-border));
  background:
    radial-gradient(circle at 50% 42%, rgb(135 48 64 / 11%), transparent 42%),
    var(--ui-gradient-panel);
  box-shadow: var(--ui-shadow-inset), 0 16px 36px rgb(0 0 0 / 24%);
}

.enemy-stage::after {
  position: absolute;
  right: 18%;
  bottom: 0;
  left: 18%;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgb(216 95 114 / 50%), transparent);
  content: '';
}

.enemy-stage--training {
  border-color: color-mix(in srgb, var(--ui-color-primary) 40%, var(--ui-color-border));
  background:
    radial-gradient(circle at 50% 42%, rgb(146 136 255 / 12%), transparent 44%),
    var(--ui-gradient-panel);
}

.enemy-stage--training::after {
  background: linear-gradient(90deg, transparent, rgb(146 136 255 / 50%), transparent);
}

.enemy-heading {
  position: relative;
  z-index: 1;
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: var(--ui-space-2);
}

.enemy-heading h1 {
  margin: 2px 0 0;
  font-family: var(--ui-font-display);
  font-size: clamp(1.65rem, 7vw, 2.15rem);
  line-height: 1;
}

.enemy-heading small,
.player-heading small,
.combat-log header small,
.training-stats small {
  color: var(--ui-color-text-muted);
  font-size: .62rem;
  font-weight: 600;
  letter-spacing: .05em;
}

.burn,
.hot {
  color: #f08b63;
  font-size: var(--ui-font-size-xs);
  font-weight: 700;
}

.enemy-portrait {
  position: relative;
  z-index: 1;
  display: grid;
  height: 14rem;
  place-items: center;
  overflow: hidden;
  background: radial-gradient(circle at 50% 60%, rgb(255 255 255 / 4%), transparent 55%);
}

.enemy-portrait::after {
  position: absolute;
  right: 20%;
  bottom: 8%;
  left: 20%;
  height: 12%;
  border-radius: 50%;
  background: rgb(0 0 0 / 30%);
  filter: blur(12px);
  content: '';
}

.enemy-portrait img {
  position: relative;
  z-index: 1;
  width: 100%;
  height: 100%;
  object-fit: contain;
  filter: drop-shadow(0 .8rem 1.2rem rgb(0 0 0 / 42%));
}

.enemy-placeholder {
  position: relative;
  z-index: 1;
  display: grid;
  width: 7rem;
  height: 7rem;
  place-items: center;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: 50%;
  background: rgb(4 7 12 / 65%);
  color: var(--ui-color-text-muted);
  font-size: 2rem;
}

.training-dummy {
  position: relative;
  z-index: 1;
  display: grid;
  width: 7rem;
  height: 11rem;
  place-items: center;
  align-content: center;
  gap: var(--ui-space-2);
  border: 2px solid rgb(188 161 114 / 38%);
  border-radius: 45% 45% 18% 18%;
  background: linear-gradient(180deg, #705b42, #2a2119);
  box-shadow: 0 1rem 2rem rgb(0 0 0 / 35%);
  color: #d5b783;
  text-align: center;
}

.training-dummy span {
  font-size: 2.5rem;
}

.training-dummy b {
  font-size: var(--ui-font-size-xs);
  letter-spacing: .12em;
}

.training-note {
  position: relative;
  z-index: 1;
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.training-stats {
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: var(--ui-space-2);
  background:
    linear-gradient(135deg, rgb(146 136 255 / 7%), transparent 55%),
    var(--ui-gradient-panel);
}

.training-stats > div {
  display: grid;
  gap: 2px;
  padding: var(--ui-space-2);
  border-radius: var(--ui-radius-sm);
  background: rgb(255 255 255 / 1.5%);
  text-align: center;
}

.training-stats strong {
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-lg);
  font-variant-numeric: tabular-nums;
}

.player-panel {
  background:
    radial-gradient(circle at 8% 0, rgb(74 184 207 / 6%), transparent 18rem),
    var(--ui-gradient-panel);
}

.player-heading {
  display: flex;
  justify-content: space-between;
  gap: var(--ui-space-2);
}

.player-heading > div {
  display: grid;
}

.player-heading > div strong {
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-lg);
}

.player-heading > span {
  align-self: center;
  padding: 4px 7px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  color: var(--ui-color-text-muted);
  font-size: .6rem;
}

.player-heading > span.active {
  border-color: rgb(79 185 150 / 34%);
  background: rgb(79 185 150 / 6%);
  color: #84d5bb;
}

.pyro-state {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ui-space-2);
  padding: var(--ui-space-2);
  border: 1px solid color-mix(in srgb, var(--ui-modifier-fire) 35%, var(--ui-color-border));
  border-radius: var(--ui-radius-md);
  background: color-mix(in srgb, var(--ui-modifier-fire) 7%, transparent);
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.pyro-state b {
  color: var(--ui-color-text-primary);
}

.abilities {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--ui-space-2);
}

.abilities button,
.consumables button {
  position: relative;
  display: grid;
  grid-template-columns: auto 1fr;
  align-items: center;
  gap: var(--ui-space-2);
  min-height: 4rem;
  padding: var(--ui-space-2);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: linear-gradient(180deg, rgb(255 255 255 / 2.5%), rgb(3 6 11 / 35%));
  box-shadow: var(--ui-shadow-inset);
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
  transition:
    border-color var(--ui-transition-fast),
    background var(--ui-transition-fast),
    transform var(--ui-transition-fast),
    opacity var(--ui-transition-fast);
}

.abilities button:hover:not(:disabled),
.consumables button:hover:not(:disabled) {
  border-color: var(--ui-color-border-strong);
  background: linear-gradient(180deg, rgb(146 136 255 / 7%), rgb(3 6 11 / 35%));
}

.abilities button:active:not(:disabled),
.consumables button:active:not(:disabled) {
  transform: scale(.99);
}

.abilities button:disabled,
.consumables button:disabled {
  cursor: not-allowed;
  opacity: .42;
  filter: saturate(.65);
}

.abilities button.ability--comet {
  border-color: color-mix(in srgb, var(--ui-modifier-fire) 70%, var(--ui-color-border));
  background: linear-gradient(180deg, rgb(219 117 72 / 11%), rgb(3 6 11 / 35%));
  box-shadow: var(--ui-shadow-inset), 0 0 18px color-mix(in srgb, var(--ui-modifier-fire) 22%, transparent);
}

.abilities button > span:last-child,
.consumables div {
  display: grid;
  gap: 2px;
}

.abilities small,
.consumables small {
  color: var(--ui-color-text-muted);
  font-size: .66rem;
}

.ability-icon,
.consumables button > span {
  display: grid;
  width: 3rem;
  height: 3rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(3 5 10 / 85%);
  color: var(--ui-color-primary);
}

.ability--comet .ability-icon {
  border-color: color-mix(in srgb, var(--ui-modifier-fire) 50%, var(--ui-color-border));
  color: var(--ui-modifier-fire);
}

.ability-icon img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.controls {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ui-space-2);
}

.combat-log {
  background: linear-gradient(180deg, rgb(12 17 28 / 92%), rgb(7 10 17 / 96%));
}

.combat-log header {
  display: flex;
  justify-content: space-between;
  gap: var(--ui-space-2);
}

.combat-log ol {
  display: grid;
  gap: 5px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.combat-log li {
  display: grid;
  grid-template-columns: 4.5rem 1fr;
  gap: var(--ui-space-2);
  padding: var(--ui-space-2);
  border-left: 2px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-sm);
  background: rgb(255 255 255 / 1.8%);
}

.combat-log li[data-side='player'] {
  border-left-color: var(--ui-color-primary);
}

.combat-log li[data-side='enemy'] {
  border-left-color: var(--ui-color-danger);
}

.actor {
  color: var(--ui-color-text-muted);
  font-size: .6rem;
  font-weight: 700;
}

.combat-log li div {
  display: grid;
  gap: 2px;
}

.combat-log li strong {
  font-size: var(--ui-font-size-sm);
  font-weight: var(--ui-font-weight-medium);
}

.combat-log li small {
  color: var(--ui-color-text-muted);
  font-size: .65rem;
  font-weight: 400;
}

.error {
  margin: 0;
  padding: var(--ui-space-3);
  border: 1px solid rgb(216 95 114 / 32%);
  border-radius: var(--ui-radius-md);
  background: rgb(216 95 114 / 6%);
  color: #ef9bab;
  font-size: var(--ui-font-size-sm);
}

.missing {
  display: grid;
  gap: var(--ui-space-3);
  place-items: start;
}

@media (max-width: 520px) {
  .combat-screen {
    padding-inline: var(--ui-space-3);
  }

  .training-stats {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .training-stats > div:last-child {
    grid-column: 1 / -1;
  }

  .controls {
    display: grid;
    grid-template-columns: 1fr;
  }

  .controls :deep(.ui-button) {
    width: 100%;
  }
}

@media (max-width: 420px) {
  .abilities {
    grid-template-columns: 1fr;
  }

  .combat-log li {
    grid-template-columns: 3.8rem 1fr;
  }

  .enemy-portrait {
    height: 12rem;
  }

  .enemy-stage,
  .player-panel,
  .combat-log,
  .training-stats {
    padding: var(--ui-space-3);
  }
}
</style>
