<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'

import type { CombatAbility, CombatCastSnapshot, CombatEvent, CombatEffectSnapshot, InventoryItem } from '@/api/contracts'
import { abilityArtUrl } from '@/assets/abilityArt'
import { gameArt } from '@/assets/gameArt'
import { monsterArtUrl } from '@/assets/monsterArt'
import { resolveAbilityArt } from '@/game/talents/talentArt'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIHealthBar } from '@/ui/components'

const emit = defineEmits<{ leave: [] }>()
const combat = useCombatSessionStore()
const session = useGameSessionStore()
const now = ref(Date.now())
const logOpen = ref(false)
const timer = window.setInterval(() => (now.value = Date.now()), 100)
const snapshot = computed(() => combat.snapshot)
const combatEnemies = computed(() => snapshot.value?.enemies ?? (snapshot.value ? [snapshot.value.enemy] : []))
const aliveEnemies = computed(() => combatEnemies.value.filter((enemy) => enemy.hp > 0))
const battlefieldArt = computed(() => {
  const locationId = session.snapshot?.world?.currentLocation.id
  if (locationId === 'BROODMOTHER_LAIR') return gameArt.world.ancientRuins
  if (locationId === 'BLIGHTED_GROVE') return gameArt.world.caravanRoad
  return gameArt.world.combatWhispering
})

type LogSide = 'player' | 'enemy' | 'system'
interface CombatLogEntry {
  key: number
  side: LogSide
  actor: string
  text: string
  detail?: string
}
interface EnemyPresentation {
  name: string
  level: number
  art?: string
}

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
const displayAbilities = computed(() => snapshot.value?.player.abilities.slice(0, 6) ?? [])
const abilitySlots = computed<(CombatAbility | null)[]>(() =>
  Array.from({ length: 6 }, (_, index) => displayAbilities.value[index] ?? null),
)
const abilityById = computed(() => new Map<string, CombatAbility>(
  [
    ...(snapshot.value?.player.abilities ?? []),
    ...(snapshot.value?.enemy.abilities ?? []),
  ].map((ability) => [ability.id, ability]),
))
const combatConsumables = computed(() =>
  (session.snapshot?.character?.inventory.items ?? [])
    .filter((item) => item.type === 'Consumable' && item.quantity > 0)
    .filter((item) => item.consumableActions.every((action) =>
      action.type !== 'RestoreResource'
      || action.resourceType === snapshot.value?.player.resourceType,
    )),
)
const resourceName = computed(() =>
  snapshot.value?.player.resourceType === 'MANA'
    ? 'Мана'
    : snapshot.value?.player.resourceType === 'FOCUS'
      ? 'Фокус'
      : 'Ярость',
)
const resourceTone = computed<'rage' | 'focus' | 'mana'>(() =>
  snapshot.value?.player.resourceType === 'MANA'
    ? 'mana'
    : snapshot.value?.player.resourceType === 'FOCUS'
      ? 'focus'
      : 'rage',
)
const fireballStreak = computed(() =>
  snapshot.value?.player.effects.find((effect) => effect.id === 'PYRO_FIREBALL_STREAK')?.stacks ?? 0,
)
const heatLimit = computed(() =>
  snapshot.value?.player.effects.find((effect) => effect.id === 'PYRO_HEAT_LIMIT') ?? null,
)
const combustion = computed(() =>
  snapshot.value?.player.effects.find((effect) => effect.id === 'PYRO_COMBUSTION') ?? null,
)
const isMage = computed(() => snapshot.value?.player.definitionId === 'MAGE')
const playerArt = computed(() =>
  snapshot.value?.player.definitionId === 'WARRIOR' ? gameArt.characters.warrior : null,
)
const isTraining = computed(() => snapshot.value?.enemy.definitionId === TRAINING_DUMMY_ID)
const trainingElapsedSeconds = computed(() => {
  const startedAt = combat.trainingStats.startedAtUtc
  if (!startedAt) return 0
  return Math.max(0, (now.value - Date.parse(startedAt)) / 1_000)
})
const trainingDps = computed(() =>
  trainingElapsedSeconds.value > 0
    ? combat.trainingStats.totalDamage / trainingElapsedSeconds.value
    : 0,
)
const playerEffects = computed(() => snapshot.value?.player.effects.slice(0, 6) ?? [])
const enemyEffects = computed(() => snapshot.value?.enemy.effects.slice(0, 6) ?? [])
const playerCast = computed(() => snapshot.value?.player.activeCast ?? null)
const enemyCast = computed(() => snapshot.value?.enemy.activeCast ?? null)

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
      actor: actorLabel(side, event),
      text: eventText(event, critical),
      detail: resourceEvent
        ? `${resourceEvent.amount > 0 ? '+' : ''}${Math.round(resourceEvent.amount * 10) / 10} ${resourceName.value.toLowerCase()} · ${abilityName(resourceEvent.definitionId)}`
        : undefined,
    })
  }
  return entries.slice(-12).reverse()
})
const recentFeedback = computed(() =>
  logEntries.value.find((entry) => entry.side !== 'system') ?? logEntries.value[0] ?? null,
)

function cooldownRemaining(abilityId: string): number {
  const readyAt = snapshot.value?.player.cooldowns[abilityId]
  return readyAt ? Math.max(0, (Date.parse(readyAt) - now.value) / 1_000) : 0
}

function effectRemaining(expiresAtUtc: string): number {
  return Math.max(0, (Date.parse(expiresAtUtc) - now.value) / 1_000)
}

function castRemaining(cast: CombatCastSnapshot | null): number {
  if (!cast) return 0
  return Math.max(0, (Date.parse(cast.resolvesAtUtc) - now.value) / 1_000)
}

function castProgress(cast: CombatCastSnapshot | null): number {
  if (!cast) return 0
  const started = Date.parse(cast.startedAtUtc)
  const resolves = Date.parse(cast.resolvesAtUtc)
  const duration = Math.max(1, resolves - started)
  return Math.min(100, Math.max(0, ((now.value - started) / duration) * 100))
}

function abilityName(id: string | null | undefined): string {
  if (!id) return ''
  const ability = abilityById.value.get(id)
  if (ability) return ability.displayName
  if (id === 'AUTO_ATTACK') return 'Автоатака'
  if (id === 'DIRECT_DAMAGE_TAKEN') return 'Получение урона'
  if (id === 'COMBAT_REGEN') return 'Регенерация'
  const inventoryItem = session.snapshot?.character?.inventory.items.find(
    (item) => item.definitionId === id,
  )
  if (inventoryItem) return inventoryItem.name
  if (id === 'PYRO_BURN') return 'Горение'
  if (id === 'PYRO_COMET_AFTERSHOCK') return 'Кометный удар'
  return id.split('_').join(' ')
}

function abilityIcon(ability: CombatAbility): string | undefined {
  return resolveAbilityArt(ability.id) ?? abilityArtUrl(ability.iconId)
}

function effectLabel(effect: CombatEffectSnapshot): string {
  return abilityName(effect.id)
}

function eventSide(event: CombatEvent): LogSide {
  const current = snapshot.value
  if (!current || ['CombatStarted', 'CombatEnded', 'ActorDied', 'EnemyKilled', 'TargetChanged'].includes(event.type)) {
    return 'system'
  }
  const source = event.sourceActorId ?? event.actorId
  if (source === current.player.actorId) return 'player'
  if (combatEnemies.value.some((enemy) => enemy.actorId === source)) return 'enemy'
  return 'system'
}

function actorLabel(side: LogSide, event?: CombatEvent): string {
  if (side === 'player') return 'ВЫ'
  if (side === 'enemy') {
    const source = event?.sourceActorId ?? event?.actorId
    return combatEnemies.value.find((enemy) => enemy.actorId === source)?.name.toUpperCase()
      ?? enemyPresentation.value?.name.toUpperCase()
      ?? 'ВРАГ'
  }
  return 'СИСТЕМА'
}

function eventText(event: CombatEvent, critical = false): string {
  const definition = abilityName(event.definitionId)
  const enemyName = enemyPresentation.value?.name ?? 'Противник'
  const eventEnemy = combatEnemies.value.find((enemy) =>
    enemy.actorId === (event.targetActorId ?? event.actorId),
  )
  switch (event.type) {
    case 'CombatStarted':
      return isTraining.value ? 'Тренировка началась' : `Бой с ${enemyName} начался`
    case 'AutoAttackStarted':
      return 'Автоатака включена'
    case 'AutoAttackStopped':
      return 'Автоатака остановлена'
    case 'DamageDealt':
      return `${definition || 'Атака'} · ${Math.round(event.amount)} урона${critical ? ' · КРИТ!' : ''}`
    case 'DamageBlocked':
      return `Заблокировано ${Math.round(event.amount)} урона`
    case 'AbilityUsed':
      return `${definition} · действие выполнено`
    case 'EffectApplied':
      return `Наложен эффект «${definition}»`
    case 'EffectRefreshed':
      return `Обновлён эффект «${definition}»`
    case 'HealingApplied':
      return `Восстановлено ${Math.round(event.amount)} здоровья`
    case 'ConsumableUsed':
      return `${definition} · расходник использован`
    case 'TauntApplied':
      return `Провокация · ${definition}`
    case 'ActorDied': {
      const deadEnemy = combatEnemies.value.find((enemy) => enemy.actorId === event.actorId)
      return deadEnemy ? `${deadEnemy.name} повержен` : 'Вы повержены'
    }
    case 'EnemyKilled':
      return `${eventEnemy?.name ?? enemyName} повержен`
    case 'TargetChanged':
      return `Новая цель · ${eventEnemy?.name ?? enemyName}`
    case 'CombatEnded':
      return event.definitionId === 'Victory'
        ? 'Победа'
        : event.definitionId === 'Defeat'
          ? 'Поражение'
          : isTraining.value
            ? 'Тренировка завершена'
            : 'Бой завершён'
    default:
      return definition ? `Событие · ${definition}` : 'Событие боя'
  }
}

function abilityState(ability: CombatAbility): 'cooldown' | 'resource' | 'ready' {
  if (cooldownRemaining(ability.id) > 0) return 'cooldown'
  if ((snapshot.value?.player.resource ?? 0) < ability.resourceCost) return 'resource'
  return 'ready'
}

async function selectCombatTarget(targetActorId: string): Promise<void> {
  if (combat.pending) return
  await combat.selectTarget(targetActorId)
}

function consumableCooldownRemaining(item: InventoryItem): number {
  const category = item.consumableCooldownCategoryId
  if (!category) return 0
  const readyAt = snapshot.value?.player.consumableCooldowns?.[category]
  return readyAt ? Math.max(0, Date.parse(readyAt) - now.value) : 0
}

function consumableCanAffect(item: InventoryItem): boolean {
  const player = snapshot.value?.player
  if (!player) return false
  return item.consumableActions.some((action) => {
    if (action.type === 'RestoreHp') return player.hp < player.maxHp
    if (action.type === 'RestoreResource') {
      return action.resourceType === player.resourceType && player.resource < player.maxResource
    }
    return true
  })
}

function consumableGlyph(item: InventoryItem): string {
  if (item.consumableActions.some((action) => action.type === 'RestoreHp')) return '✚'
  if (item.consumableActions.some((action) => action.type === 'RestoreResource')) return '◈'
  if (item.consumableActions.some((action) => action.type === 'RemoveEffect')) return '⊘'
  return '✦'
}

async function useConsumable(item: InventoryItem): Promise<void> {
  if (!snapshot.value || isTraining.value || combat.pending) return
  await combat.useConsumable(item.definitionId)
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
      <header class="combat-hud">
        <section class="combat-hud__actor combat-hud__actor--player" aria-label="Состояние игрока">
          <div class="combat-hud__identity">
            <small>ВЫ</small>
            <strong>{{ snapshot.player.name }}</strong>
          </div>
          <UIHealthBar
            label="HP"
            :value="snapshot.player.hp"
            :max="snapshot.player.maxHp"
          />
          <UIHealthBar
            :label="resourceName"
            :tone="resourceTone"
            :value="snapshot.player.resource"
            :max="snapshot.player.maxResource"
          />
        </section>

        <section class="combat-hud__actor combat-hud__actor--enemy" aria-label="Состояние противника">
          <div class="combat-hud__identity">
            <small>{{ isTraining ? 'ТРЕНИРОВКА' : `ПРОТИВНИК · УР. ${enemyPresentation.level}` }}</small>
            <strong>{{ enemyPresentation.name }}</strong>
          </div>
          <UIHealthBar
            label="HP"
            :value="snapshot.enemy.hp"
            :max="snapshot.enemy.maxHp"
          />
        </section>
      </header>

      <nav
        v-if="aliveEnemies.length > 1"
        class="combat-targets"
        aria-label="Выбор цели"
        data-combat-targets
      >
        <button
          v-for="enemy in aliveEnemies"
          :key="enemy.actorId"
          type="button"
          :class="{ active: enemy.actorId === (snapshot.selectedTargetActorId ?? snapshot.enemy.actorId) }"
          :disabled="combat.pending"
          :data-target-actor-id="enemy.actorId"
          @click="selectCombatTarget(enemy.actorId)"
        >
          <span>{{ enemy.name }}</span>
          <small>{{ Math.ceil(enemy.hp) }} / {{ Math.ceil(enemy.maxHp) }}</small>
        </button>
      </nav>

      <section
        class="battlefield"
        data-combat-battlefield
        :style="{ '--battlefield-art': `url(${battlefieldArt})` }"
      >
        <div class="battlefield__vignette" />

        <div class="enemy-effects effect-strip effect-strip--enemy">
          <span
            v-for="effect in enemyEffects"
            :key="effect.id"
            :title="effectLabel(effect)"
          >
            <b>{{ effect.stacks }}</b>
            <small>{{ effectRemaining(effect.expiresAtUtc).toFixed(1) }}</small>
          </span>
        </div>

        <div v-if="enemyCast" class="cast-bar cast-bar--enemy" data-enemy-cast>
          <div>
            <strong>{{ abilityName(enemyCast.abilityId) }}</strong>
            <small>{{ castRemaining(enemyCast).toFixed(1) }}с</small>
          </div>
          <i><span :style="{ width: `${castProgress(enemyCast)}%` }" /></i>
        </div>

        <div class="enemy-figure">
          <img
            v-if="enemyPresentation.art"
            :src="enemyPresentation.art"
            :alt="enemyPresentation.name"
          />
          <div
            v-else-if="isTraining"
            class="training-dummy"
            role="img"
            aria-label="Тренировочный манекен"
          >
            <span>✦</span><b>ЦЕЛЬ</b>
          </div>
          <div
            v-else
            class="enemy-placeholder"
            role="img"
            :aria-label="enemyPresentation.name"
          >
            ⚔
          </div>
        </div>

        <div
          v-if="recentFeedback"
          class="combat-feedback"
          :data-side="recentFeedback.side"
          aria-live="polite"
        >
          {{ recentFeedback.text }}
        </div>

        <div class="player-figure" aria-hidden="true">
          <img v-if="playerArt" :src="playerArt" alt="" />
          <div v-else class="player-figure__fallback">
            {{ snapshot.player.definitionId.slice(0, 1) }}
          </div>
        </div>
      </section>

      <section v-if="isTraining" class="training-stats" aria-label="Статистика тренировки">
        <div><small>ВРЕМЯ</small><strong>{{ trainingElapsedSeconds.toFixed(1) }}с</strong></div>
        <div><small>DPS</small><strong>{{ Math.round(trainingDps).toLocaleString('ru-RU') }}</strong></div>
        <div><small>УРОН</small><strong>{{ Math.round(combat.trainingStats.totalDamage).toLocaleString('ru-RU') }}</strong></div>
        <div><small>КРИТЫ</small><strong>{{ combat.trainingStats.criticalHits }}</strong></div>
        <div><small>МАКС.</small><strong>{{ Math.round(combat.trainingStats.maxHit).toLocaleString('ru-RU') }}</strong></div>
      </section>

      <section class="combat-actions">
        <div class="player-state-row">
          <div class="effect-strip effect-strip--player">
            <span
              v-for="effect in playerEffects"
              :key="effect.id"
              :title="effectLabel(effect)"
            >
              <b>{{ effect.stacks }}</b>
              <small>{{ effectRemaining(effect.expiresAtUtc).toFixed(1) }}</small>
            </span>
            <small v-if="playerEffects.length === 0" class="effect-strip__empty">Нет активных эффектов</small>
          </div>

          <span
            class="autoattack-state"
            :class="{ active: snapshot.player.autoAttackEnabled }"
          >
            {{ snapshot.player.autoAttackEnabled ? 'AUTO · ON' : 'AUTO · OFF' }}
          </span>
        </div>

        <div v-if="isMage" class="pyro-state" aria-label="Состояние пироманта">
          <span>Fireball Crit <b>{{ fireballStreak }}/3</b></span>
          <span v-if="heatLimit" class="hot">
            ПРЕДЕЛ ЖАРА · {{ effectRemaining(heatLimit.expiresAtUtc).toFixed(1) }}с
          </span>
          <span v-if="combustion" class="hot">
            ВОЗГОРАНИЕ · {{ effectRemaining(combustion.expiresAtUtc).toFixed(1) }}с
          </span>
        </div>

        <div v-if="playerCast" class="cast-bar cast-bar--player" data-player-cast>
          <div>
            <strong>{{ abilityName(playerCast.abilityId) }}</strong>
            <small>{{ castRemaining(playerCast).toFixed(1) }}с</small>
          </div>
          <i><span :style="{ width: `${castProgress(playerCast)}%` }" /></i>
        </div>

        <div class="ability-row" aria-label="Боевые способности">
          <button
            v-for="(ability, index) in abilitySlots"
            :key="ability?.id ?? `empty-${index}`"
            type="button"
            class="ability-slot"
            :class="{ 'ability-slot--empty': !ability, 'ability-slot--comet': ability?.id === 'FIRE_COMET' }"
            :data-ability-slot="ability?.id ?? ''"
            :data-state="ability ? abilityState(ability) : 'empty'"
            :disabled="!ability || combat.pending || abilityState(ability) !== 'ready'"
            :aria-label="ability?.displayName ?? 'Пустой слот способности'"
            @click="ability && combat.useAbility(ability.id)"
          >
            <span class="ability-slot__icon">
              <img v-if="ability && abilityIcon(ability)" :src="abilityIcon(ability)" alt="" />
              <b v-else-if="ability">{{ ability.displayName.slice(0, 2) }}</b>
              <i v-else />
            </span>
            <small v-if="ability">{{ ability.displayName }}</small>
            <b v-if="ability && cooldownRemaining(ability.id) > 0" class="ability-slot__cooldown">
              {{ Math.ceil(cooldownRemaining(ability.id)) }}
            </b>
            <span
              v-else-if="ability && ability.resourceCost > 0"
              class="ability-slot__cost"
            >
              {{ Math.round(ability.resourceCost) }}
            </span>
          </button>
        </div>

        <div
          v-if="!isTraining && combatConsumables.length"
          class="consumable-row"
          aria-label="Боевые расходники"
        >
          <button
            v-for="item in combatConsumables"
            :key="item.id"
            type="button"
            class="utility-action"
            :data-combat-consumable="item.definitionId"
            :disabled="combat.pending || consumableCooldownRemaining(item) > 0 || !consumableCanAffect(item)"
            @click="useConsumable(item)"
          >
            <span>{{ consumableGlyph(item) }}</span>
            <div>
              <strong>{{ item.name }}</strong>
              <small v-if="consumableCooldownRemaining(item) > 0">
                {{ (consumableCooldownRemaining(item) / 1000).toFixed(1) }}с
              </small>
              <small v-else>×{{ item.quantity }}</small>
            </div>
          </button>
        </div>

        <div class="utility-row" aria-label="Дополнительные боевые действия">
          <button
            type="button"
            class="utility-action"
            data-autoattack-toggle
            :class="{ active: snapshot.player.autoAttackEnabled }"
            :disabled="combat.pending"
            @click="combat.toggleAutoAttack"
          >
            <span>⚔</span>
            <div>
              <strong>Автоатака</strong>
              <small>{{ snapshot.player.autoAttackEnabled ? 'Включена' : 'Выключена' }}</small>
            </div>
          </button>

          <button
            v-if="isTraining"
            type="button"
            class="utility-action"
            :disabled="combat.pending"
            @click="resetTrainingCombat"
          >
            <span>↻</span>
            <div><strong>Сброс</strong><small>Тренировка</small></div>
          </button>

          <button
            type="button"
            class="utility-action utility-action--leave"
            data-leave-combat
            :disabled="combat.pending"
            @click="leaveCombat"
          >
            <span>×</span>
            <div>
              <strong>{{ isTraining ? 'Завершить' : 'Покинуть бой' }}</strong>
              <small>{{ isTraining ? 'Тренировку' : 'Выход' }}</small>
            </div>
          </button>
        </div>
      </section>

      <section class="combat-log">
        <button
          class="combat-log__toggle"
          data-combat-log-toggle
          type="button"
          :aria-expanded="logOpen"
          @click="logOpen = !logOpen"
        >
          <span>
            <b>Журнал боя</b>
            <small>{{ logEntries[0]?.text ?? 'Событий пока нет' }}</small>
          </span>
          <strong>{{ logOpen ? '−' : '+' }}</strong>
        </button>
        <ol v-if="logOpen">
          <li v-for="entry in logEntries" :key="entry.key" :data-side="entry.side">
            <span class="actor">{{ entry.actor }}</span>
            <div>
              <strong>{{ entry.text }}</strong>
              <small v-if="entry.detail">↳ {{ entry.detail }}</small>
            </div>
          </li>
        </ol>
      </section>

      <p v-if="combat.errorCode" class="error">
        Не удалось выполнить действие: {{ combat.errorCode }}
      </p>
    </template>

    <div v-else class="missing">
      <h1>Бой прерван</h1>
      <UIButton @click="emit('leave')">Вернуться в мир</UIButton>
    </div>
  </section>
</template>

<style scoped>
.combat-screen {
  display: grid;
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  gap: 8px;
  padding: 8px 10px calc(var(--ui-space-5) + var(--ui-safe-area-bottom));
  background:
    radial-gradient(circle at 50% 22%, rgb(111 74 94 / 9%), transparent 18rem),
    rgb(5 7 12);
}

.combat-hud {
  position: relative;
  z-index: 4;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 7px;
}

.combat-hud__actor {
  display: grid;
  gap: 4px;
  min-width: 0;
  padding: 7px 8px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(8 12 20 / 92%);
  box-shadow: var(--ui-shadow-inset);
}

.combat-hud__actor--player {
  border-color: color-mix(in srgb, var(--ui-color-primary) 28%, var(--ui-color-border));
}

.combat-hud__actor--enemy {
  border-color: color-mix(in srgb, var(--ui-color-danger) 32%, var(--ui-color-border));
}

.combat-hud__identity {
  display: grid;
  min-width: 0;
  gap: 1px;
}

.combat-hud__identity small {
  color: var(--ui-color-text-muted);
  font-size: .47rem;
  font-weight: 800;
  letter-spacing: .07em;
}

.combat-hud__identity strong {
  overflow: hidden;
  font-family: var(--ui-font-display);
  font-size: .72rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-targets {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(7rem, 1fr));
  gap: 5px;
}

.combat-targets button {
  display: grid;
  min-width: 0;
  gap: 2px;
  padding: 6px 8px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: rgb(7 10 17 / 92%);
  color: var(--ui-color-text-secondary);
  font: inherit;
  text-align: left;
}

.combat-targets button.active {
  border-color: rgb(216 95 114 / 48%);
  background: rgb(216 95 114 / 8%);
  color: var(--ui-color-text-primary);
}

.combat-targets button span,
.combat-targets button small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-targets button span {
  font-size: .52rem;
  font-weight: 800;
}

.combat-targets button small {
  color: var(--ui-color-text-muted);
  font-size: .43rem;
}

.battlefield {
  position: relative;
  min-height: 18.5rem;
  overflow: hidden;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-lg);
  background:
    linear-gradient(180deg, rgb(5 7 12 / 18%), rgb(5 7 12 / 72%) 76%, rgb(5 7 12 / 94%)),
    var(--battlefield-art) center / cover;
  box-shadow:
    inset 0 0 35px rgb(0 0 0 / 42%),
    0 14px 30px rgb(0 0 0 / 28%);
  isolation: isolate;
}

.battlefield::before {
  position: absolute;
  inset: 0;
  z-index: -1;
  background:
    linear-gradient(90deg, transparent 49.7%, rgb(255 255 255 / 2%) 50%, transparent 50.3%),
    linear-gradient(180deg, transparent 71%, rgb(255 255 255 / 3%) 71.3%, transparent 72%);
  content: '';
}

.battlefield__vignette {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(180deg, rgb(0 0 0 / 10%), transparent 32%, rgb(0 0 0 / 45%)),
    radial-gradient(circle at 50% 45%, transparent 15%, rgb(2 4 8 / 44%) 84%);
  pointer-events: none;
}

.enemy-figure {
  position: absolute;
  top: 2.9rem;
  right: 10%;
  left: 10%;
  display: grid;
  height: 11.5rem;
  place-items: center;
}

.enemy-figure::after {
  position: absolute;
  right: 25%;
  bottom: 2%;
  left: 25%;
  height: 12%;
  border-radius: 50%;
  background: rgb(0 0 0 / 42%);
  filter: blur(9px);
  content: '';
}

.enemy-figure img {
  position: relative;
  z-index: 1;
  width: 100%;
  height: 100%;
  object-fit: contain;
  filter: drop-shadow(0 .8rem 1.2rem rgb(0 0 0 / 55%));
}

.enemy-placeholder,
.training-dummy {
  position: relative;
  z-index: 1;
  display: grid;
  place-items: center;
}

.enemy-placeholder {
  width: 6.5rem;
  height: 6.5rem;
  border: 1px solid rgb(216 95 114 / 28%);
  border-radius: 50%;
  background: rgb(7 10 17 / 80%);
  color: #c78591;
  font-size: 2rem;
}

.training-dummy {
  width: 6.2rem;
  height: 9rem;
  align-content: center;
  gap: 7px;
  border: 2px solid rgb(188 161 114 / 38%);
  border-radius: 45% 45% 18% 18%;
  background: linear-gradient(180deg, #705b42, #2a2119);
  color: #d5b783;
  text-align: center;
}

.training-dummy span {
  font-size: 2rem;
}

.training-dummy b {
  font-size: .52rem;
  letter-spacing: .12em;
}

.player-figure {
  position: absolute;
  bottom: -1.8rem;
  left: 5%;
  z-index: 2;
  display: grid;
  width: 7.5rem;
  height: 9.5rem;
  place-items: end center;
  opacity: .78;
  pointer-events: none;
}

.player-figure img {
  width: 100%;
  height: 100%;
  object-fit: contain;
  filter: drop-shadow(0 .5rem .9rem rgb(0 0 0 / 62%));
}

.player-figure__fallback {
  display: grid;
  width: 4.5rem;
  height: 7rem;
  place-items: center;
  border: 1px solid rgb(146 136 255 / 20%);
  border-radius: 46% 46% 25% 25%;
  background: rgb(55 50 87 / 35%);
  color: #aaa3ff;
  font-family: var(--ui-font-display);
  font-size: 1.5rem;
}

.effect-strip {
  display: flex;
  min-height: 28px;
  align-items: center;
  gap: 4px;
}

.effect-strip > span {
  display: grid;
  width: 27px;
  height: 27px;
  place-items: center;
  border: 1px solid var(--ui-color-border);
  border-radius: 6px;
  background: rgb(4 7 12 / 88%);
  color: var(--ui-color-text-secondary);
  font-size: .49rem;
}

.effect-strip > span b {
  font-size: .52rem;
  line-height: 1;
}

.effect-strip > span small {
  color: var(--ui-color-text-muted);
  font-size: .39rem;
  line-height: 1;
}

.effect-strip--enemy {
  position: absolute;
  top: 7px;
  right: 8px;
  z-index: 3;
  justify-content: flex-end;
}

.effect-strip--enemy > span {
  border-color: rgb(216 95 114 / 24%);
}

.effect-strip--player > span {
  border-color: rgb(146 136 255 / 26%);
}

.effect-strip__empty {
  color: var(--ui-color-text-muted);
  font-size: .52rem;
}

.cast-bar {
  display: grid;
  gap: 3px;
}

.cast-bar > div {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.cast-bar strong {
  overflow: hidden;
  font-size: .59rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.cast-bar small {
  color: var(--ui-color-text-muted);
  font-size: .5rem;
}

.cast-bar > i {
  display: block;
  height: 5px;
  overflow: hidden;
  border-radius: var(--ui-radius-round);
  background: rgb(2 4 8 / 82%);
}

.cast-bar > i > span {
  display: block;
  height: 100%;
}

.cast-bar--enemy {
  position: absolute;
  top: 2.4rem;
  right: 14%;
  left: 14%;
  z-index: 4;
  padding: 5px 7px;
  border: 1px solid rgb(216 95 114 / 25%);
  border-radius: var(--ui-radius-sm);
  background: rgb(12 7 12 / 82%);
}

.cast-bar--enemy > i > span {
  background: linear-gradient(90deg, #a64859, #e18996);
}

.cast-bar--player {
  padding: 6px 8px;
  border: 1px solid rgb(146 136 255 / 24%);
  border-radius: var(--ui-radius-sm);
  background: rgb(14 13 27 / 62%);
}

.cast-bar--player > i > span {
  background: linear-gradient(90deg, #655bc6, #aba3ff);
}

.combat-feedback {
  position: absolute;
  right: 8%;
  bottom: 3.8rem;
  z-index: 4;
  max-width: 68%;
  padding: 5px 8px;
  border-radius: var(--ui-radius-round);
  background: rgb(5 8 14 / 78%);
  color: #e8e6f2;
  font-size: .57rem;
  font-weight: 700;
  text-align: right;
  text-shadow: 0 1px 3px black;
  backdrop-filter: blur(5px);
}

.combat-feedback[data-side='enemy'] {
  color: #efa1ae;
}

.combat-feedback[data-side='player'] {
  color: #d1ccff;
}

.training-stats {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 1px;
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(255 255 255 / 5%);
}

.training-stats > div {
  display: grid;
  justify-items: center;
  gap: 1px;
  padding: 5px 3px;
  background: rgb(8 12 20 / 96%);
}

.training-stats small {
  color: var(--ui-color-text-muted);
  font-size: .4rem;
  letter-spacing: .04em;
}

.training-stats strong {
  font-family: var(--ui-font-display);
  font-size: .61rem;
  font-variant-numeric: tabular-nums;
}

.combat-actions {
  display: grid;
  gap: 7px;
  padding: 8px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background:
    radial-gradient(circle at 50% 0, rgb(146 136 255 / 7%), transparent 13rem),
    rgb(8 12 20 / 96%);
  box-shadow: var(--ui-shadow-inset);
}

.player-state-row {
  display: flex;
  min-width: 0;
  align-items: center;
  justify-content: space-between;
  gap: 7px;
}

.autoattack-state {
  flex: 0 0 auto;
  padding: 4px 6px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  color: var(--ui-color-text-muted);
  font-size: .46rem;
  font-weight: 800;
}

.autoattack-state.active {
  border-color: rgb(79 185 150 / 28%);
  color: #84d5bb;
}

.pyro-state {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
  padding: 5px 7px;
  border: 1px solid color-mix(in srgb, var(--ui-modifier-fire) 30%, var(--ui-color-border));
  border-radius: var(--ui-radius-sm);
  background: color-mix(in srgb, var(--ui-modifier-fire) 5%, transparent);
  color: var(--ui-color-text-muted);
  font-size: .5rem;
}

.pyro-state b,
.hot {
  color: #f08b63;
}

.ability-row {
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  gap: 4px;
}

.ability-slot {
  position: relative;
  display: grid;
  min-width: 0;
  min-height: 62px;
  place-items: center;
  align-content: center;
  gap: 2px;
  padding: 3px 2px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background:
    linear-gradient(180deg, rgb(255 255 255 / 2.5%), rgb(2 5 9 / 45%));
  color: var(--ui-color-text-primary);
  font: inherit;
}

.ability-slot[data-state='ready'] {
  border-color: rgb(146 136 255 / 36%);
  box-shadow: inset 0 0 0 1px rgb(146 136 255 / 4%);
}

.ability-slot[data-state='cooldown'],
.ability-slot[data-state='resource'] {
  opacity: .46;
}

.ability-slot--empty {
  opacity: .2;
}

.ability-slot--comet {
  border-color: color-mix(in srgb, var(--ui-modifier-fire) 65%, var(--ui-color-border));
}

.ability-slot__icon {
  display: grid;
  width: 38px;
  height: 38px;
  place-items: center;
  overflow: hidden;
  border: 1px solid rgb(255 255 255 / 7%);
  border-radius: 8px;
  background: rgb(3 5 10 / 90%);
  color: #aaa3ff;
}

.ability-slot__icon img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.ability-slot__icon > i {
  width: 11px;
  height: 11px;
  border: 1px solid var(--ui-color-border);
  border-radius: 50%;
}

.ability-slot > small {
  width: 100%;
  overflow: hidden;
  color: var(--ui-color-text-muted);
  font-size: .38rem;
  line-height: 1;
  text-align: center;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ability-slot__cooldown {
  position: absolute;
  inset: 3px;
  display: grid;
  place-items: center;
  border-radius: 8px;
  background: rgb(1 3 7 / 70%);
  color: white;
  font-size: .78rem;
}

.ability-slot__cost {
  position: absolute;
  right: 2px;
  bottom: 15px;
  padding: 1px 3px;
  border-radius: 5px;
  background: rgb(2 4 8 / 80%);
  color: #bdb7ff;
  font-size: .38rem;
  font-weight: 800;
}

.consumable-row,
.utility-row {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 5px;
}

.consumable-row {
  margin-bottom: 5px;
}

.utility-action {
  display: grid;
  grid-template-columns: 26px minmax(0, 1fr);
  align-items: center;
  gap: 5px;
  min-width: 0;
  min-height: 44px;
  padding: 5px 6px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(4 7 12 / 68%);
  color: var(--ui-color-text-secondary);
  font: inherit;
  text-align: left;
}

.utility-action > span {
  display: grid;
  width: 26px;
  height: 26px;
  place-items: center;
  border: 1px solid rgb(255 255 255 / 6%);
  border-radius: 7px;
  color: #aaa3ff;
}

.utility-action > div {
  display: grid;
  min-width: 0;
}

.utility-action strong {
  overflow: hidden;
  font-size: .54rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.utility-action small {
  color: var(--ui-color-text-muted);
  font-size: .42rem;
}

.utility-action.active {
  border-color: rgb(79 185 150 / 28%);
}

.utility-action--leave {
  border-color: rgb(216 95 114 / 24%);
}

.utility-action:disabled {
  opacity: .4;
}

.combat-log {
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(7 10 17 / 92%);
}

.combat-log__toggle {
  display: flex;
  width: 100%;
  min-height: 42px;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 6px 9px;
  border: 0;
  background: transparent;
  color: var(--ui-color-text-secondary);
  font: inherit;
  text-align: left;
}

.combat-log__toggle > span {
  display: grid;
  min-width: 0;
  gap: 1px;
}

.combat-log__toggle b {
  font-size: .58rem;
}

.combat-log__toggle small {
  overflow: hidden;
  color: var(--ui-color-text-muted);
  font-size: .47rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-log ol {
  display: grid;
  max-height: 13rem;
  gap: 3px;
  margin: 0;
  padding: 5px 7px 7px;
  overflow-y: auto;
  border-top: 1px solid rgb(255 255 255 / 5%);
  list-style: none;
}

.combat-log li {
  display: grid;
  grid-template-columns: 3.4rem minmax(0, 1fr);
  gap: 5px;
  padding: 5px;
  border-left: 2px solid var(--ui-color-border-strong);
  border-radius: 5px;
  background: rgb(255 255 255 / 1.5%);
}

.combat-log li[data-side='player'] {
  border-left-color: var(--ui-color-primary);
}

.combat-log li[data-side='enemy'] {
  border-left-color: var(--ui-color-danger);
}

.actor {
  color: var(--ui-color-text-muted);
  font-size: .46rem;
  font-weight: 800;
}

.combat-log li div {
  display: grid;
  gap: 1px;
}

.combat-log li strong {
  font-size: .53rem;
  font-weight: 600;
}

.combat-log li small {
  color: var(--ui-color-text-muted);
  font-size: .47rem;
}

.error {
  margin: 0;
  padding: 7px 9px;
  border: 1px solid rgb(216 95 114 / 30%);
  border-radius: var(--ui-radius-md);
  background: rgb(216 95 114 / 6%);
  color: #ef9bab;
  font-size: .58rem;
}

.missing {
  display: grid;
  gap: var(--ui-space-3);
  place-items: start;
}

@media (max-width: 390px) {
  .combat-screen {
    padding-inline: 7px;
  }

  .battlefield {
    min-height: 17rem;
  }

  .enemy-figure {
    height: 10.5rem;
  }

  .ability-row {
    gap: 3px;
  }

  .ability-slot {
    min-height: 58px;
  }

  .ability-slot__icon {
    width: 34px;
    height: 34px;
  }

  .utility-row {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 340px) {
  .combat-hud {
    grid-template-columns: 1fr;
  }

  .battlefield {
    min-height: 16rem;
  }

  .ability-slot__icon {
    width: 31px;
    height: 31px;
  }

  .ability-slot > small {
    display: none;
  }
}
</style>
