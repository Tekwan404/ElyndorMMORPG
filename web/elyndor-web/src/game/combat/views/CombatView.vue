<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'

import type { CombatAbility, CombatCastSnapshot, CombatEvent, CombatEffectSnapshot, InventoryItem } from '@/api/contracts'
import { abilityArtUrl } from '@/assets/abilityArt'
import { resolveCharacterArt } from '@/assets/characterArt'
import { gameArt } from '@/assets/gameArt'
import { monsterArtUrl } from '@/assets/monsterArt'
import { resolveAbilityArt } from '@/game/talents/talentArt'
import { locationKind, locationPresentation } from '@/game/world/locationPresentation'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName } from '@/ui/icons/icon.types'
import { UIButton, UIHealthBar } from '@/ui/components'

const emit = defineEmits<{ leave: [] }>()
const combat = useCombatSessionStore()
const combatErrorMessage = computed(() => {
  switch (combat.errorCode) {
    case 'combat_ability_on_cooldown': return 'Способность ещё восстанавливается.'
    case 'combat_insufficient_resource': return 'Недостаточно ресурса для этой способности.'
    case 'combat_invalid_target': return 'Выберите доступную цель.'
    case 'combat_actor_dead': return 'Павший герой не может действовать.'
    case 'combat_not_found': return 'Бой уже завершён. Вернитесь в локацию.'
    default: return 'Не удалось выполнить действие. Проверьте связь и попробуйте ещё раз.'
  }
})
const session = useGameSessionStore()
const now = ref(Date.now())
const logOpen = ref(false)
const timer = window.setInterval(() => (now.value = Date.now()), 100)
const snapshot = computed(() => combat.snapshot)
const combatEnemies = computed(() => snapshot.value?.enemies ?? (snapshot.value ? [snapshot.value.enemy] : []))
const aliveEnemies = computed(() => combatEnemies.value.filter((enemy) => enemy.hp > 0))
const combatPlayers = computed(() => snapshot.value?.players ?? (snapshot.value ? [snapshot.value.player] : []))
const combatAllies = computed(() => combatPlayers.value.filter((player) => player.actorId !== snapshot.value?.player.actorId))
const companion = computed(() => snapshot.value?.companion ?? null)
const companionArt = computed(() => monsterArtUrl(companion.value?.artId))
const isParticipantActive = computed(() => combat.isParticipantActive)
const lootRolls = computed(() => combat.lootRolls)
const battlefieldArt = computed(() => {
  const locationId = session.snapshot?.world?.currentLocation.id
  return locationKind(locationId) === 'city'
    ? gameArt.world.combatWhispering
    : locationPresentation(locationId).art
})

type LogSide = 'player' | 'ally' | 'enemy' | 'system'
interface CombatLogEntry {
  key: number
  side: LogSide
  actor: string
  text: string
  detail?: string
  occurredAtUtc: string
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
  snapshot.value
    ? resolveCharacterArt(
        snapshot.value.player.definitionId,
        session.snapshot?.character?.genderId ?? 'MALE',
        'transparent',
      )
    : null,
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
      occurredAtUtc: event.serverTimeUtc,
    })
  }
  return entries.slice(-12).reverse()
})
const recentFeedback = computed(() => {
  const entry = logEntries.value.find((candidate) => candidate.side !== 'system') ?? logEntries.value[0] ?? null
  if (!entry) return null
  return now.value - Date.parse(entry.occurredAtUtc) <= 4_500 ? entry : null
})

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

const autoAttackRemaining = computed(() => {
  const player = snapshot.value?.player
  if (!player?.autoAttackEnabled || !player.nextAutoAttackAtUtc) return 0
  return Math.max(0, (Date.parse(player.nextAutoAttackAtUtc) - now.value) / 1_000)
})

const autoAttackProgress = computed(() => {
  const player = snapshot.value?.player
  const interval = player?.autoAttackIntervalSeconds ?? 0
  if (!player?.autoAttackEnabled || interval <= 0 || !player.nextAutoAttackAtUtc) return 0
  return Math.min(100, Math.max(0, (1 - autoAttackRemaining.value / interval) * 100))
})

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

function abilityGlyph(ability: CombatAbility): GlyphName {
  if (ability.id.includes('FIRE') || ability.id.includes('FLAME') || ability.id === 'COMBUSTION') return 'fire'
  if (ability.id.includes('ICE') || ability.id.includes('FROST')) return 'ice'
  if (ability.id.includes('SHIELD') || ability.id.includes('BASTION')) return 'shield'
  if (ability.id.includes('BOW') || ability.id.includes('ARROW')) return 'bow'
  return 'sword'
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
  if (current.companion?.actorId === source) return 'ally'
  if (combatEnemies.value.some((enemy) => enemy.actorId === source)) return 'enemy'
  return 'system'
}

function actorLabel(side: LogSide, event?: CombatEvent): string {
  if (side === 'player') return 'ВЫ'
  if (side === 'ally') return companion.value?.name.toUpperCase() ?? 'СПУТНИК'
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
      if (deadEnemy) return `${deadEnemy.name} повержен`
      if (companion.value?.actorId === event.actorId) return `${companion.value.name} повержен`
      return 'Вы повержены'
    }
    case 'EnemyKilled':
      return `${eventEnemy?.name ?? enemyName} повержен`
    case 'TargetChanged':
      return `Новая цель · ${eventEnemy?.name ?? enemyName}`
    case 'ActorSummoned':
      return `${actorLabel('enemy', event)} призывает · ${eventEnemy?.name ?? definition}`
    case 'CombatEnded':
      return event.definitionId === 'Victory'
        ? 'Победа'
        : event.definitionId === 'Defeat'
          ? 'Поражение'
          : event.definitionId === 'FLED'
            ? 'Вы сбежали из боя'
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

function consumableGlyph(item: InventoryItem): GlyphName {
  if (item.consumableActions.some((action) => action.type === 'RestoreHp')) return 'potion'
  if (item.consumableActions.some((action) => action.type === 'RestoreResource')) return 'star'
  if (item.consumableActions.some((action) => action.type === 'RemoveEffect')) return 'shadow'
  return 'scroll'
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

async function fleeCombat(): Promise<void> {
  if (isTraining.value || combat.pending) return
  await combat.flee()
}

function combatParticipantStatus(actorId: string): string {
  const status = snapshot.value?.participantRoster?.find((participant) => participant.actorId === actorId)?.status
  if (status === 'Fled') return 'Сбежал'
  if (status === 'Dead') return 'Пал'
  if (status === 'Completed') return 'Завершил'
  if (status === 'Rostered') return 'В пути'
  return 'В бою'
}

function combatParticipantGlyph(actorId: string): GlyphName {
  const status = snapshot.value?.participantRoster?.find((participant) => participant.actorId === actorId)?.status
  if (status === 'Fled') return 'shadow'
  if (status === 'Dead') return 'skull'
  if (status === 'Completed') return 'holy'
  if (status === 'Rostered') return 'scroll'
  return 'star'
}

function combatPlayerRole(player: { definitionId: string }): string {
  if (player.definitionId === 'WARRIOR') return 'Страж'
  if (player.definitionId === 'MAGE') return 'Маг'
  if (player.definitionId === 'ARCHER') return 'Следопыт'
  return player.definitionId
}

function combatPlayerHealthRatio(player: { hp: number; maxHp: number }): number {
  if (player.maxHp <= 0) return 0
  return Math.min(100, Math.max(0, (player.hp / player.maxHp) * 100))
}

function combatEnemyHealthRatio(enemy: { hp: number; maxHp: number }): number {
  if (enemy.maxHp <= 0) return 0
  return Math.min(100, Math.max(0, (enemy.hp / enemy.maxHp) * 100))
}

async function attachCombat(): Promise<void> {
  if (!snapshot.value || isParticipantActive.value) return
  await combat.attachCombat(snapshot.value.sessionId)
}

function lootRollRemaining(endsAtUtc: string): number {
  return Math.max(0, (Date.parse(endsAtUtc) - now.value) / 1_000)
}

async function chooseLootRoll(lootRollId: string, choice: 'Need' | 'Greed' | 'Pass'): Promise<void> {
  await combat.chooseLootRoll(lootRollId, choice)
}

onUnmounted(() => window.clearInterval(timer))
</script>

<template>
  <section
    class="combat-screen"
    :class="{ 'combat-screen--party': combatPlayers.length > 1 }"
    :data-party-size="combatPlayers.length > 1 ? combatPlayers.length : undefined"
  >
    <template v-if="snapshot && enemyPresentation">
      <header class="combat-hud">
        <section class="combat-hud__actor combat-hud__actor--player" aria-label="Состояние игрока">
          <div class="combat-hud__identity">
            <small>ВЫ</small>
            <strong>{{ snapshot.player.name }}</strong>
          </div>
          <UIHealthBar
            label="Здоровье"
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
            label="Здоровье"
            :value="snapshot.enemy.hp"
            :max="snapshot.enemy.maxHp"
          />
        </section>
      </header>

      <section
        v-if="combatAllies.length > 0"
        class="combat-party-roster"
        aria-label="Состав группы в бою"
        data-combat-party-roster
      >
        <header class="combat-party-roster__header">
          <div>
            <small>СОЮЗНИКИ В БОЮ</small>
            <strong>Союзники · {{ combatAllies.length }}</strong>
          </div>
          <span>Общий фронт</span>
        </header>
        <div class="combat-party-roster__grid">
          <article
            v-for="player in combatAllies"
            :key="player.actorId"
            class="combat-party-roster__member"
            :class="{ 'combat-party-roster__member--self': player.actorId === snapshot.player.actorId }"
            :data-status="snapshot.participantRoster?.find((participant) => participant.actorId === player.actorId)?.status"
          >
            <span class="combat-party-roster__crest" aria-hidden="true">
              {{ player.name.slice(0, 1).toUpperCase() }}
            </span>
            <span class="combat-party-roster__body">
              <span class="combat-party-roster__identity">
                <strong>{{ player.name }}</strong>
                <small>{{ combatPlayerRole(player) }}</small>
              </span>
              <span class="combat-party-roster__bar" aria-hidden="true">
                <i :style="{ width: `${combatPlayerHealthRatio(player)}%` }" />
              </span>
              <span class="combat-party-roster__vitals">
                {{ Math.ceil(player.hp) }} / {{ Math.ceil(player.maxHp) }} · {{ combatParticipantStatus(player.actorId) }}
              </span>
            </span>
            <b class="combat-party-roster__state" aria-hidden="true">
              <IconGenerator :config="{ id: `combat-player-state-${player.actorId}`, glyph: combatParticipantGlyph(player.actorId), category: 'utility' }" />
            </b>
          </article>
        </div>
      </section>

      <section
        v-if="!isTraining && snapshot.status === 'Active' && !isParticipantActive"
        class="combat-join"
        data-combat-join
      >
        <strong>Бой уже идёт</strong>
        <span>Ты можешь присоединиться, когда находишься в этой локации.</span>
        <button type="button" :disabled="combat.pending" @click="attachCombat">
          Войти в бой
        </button>
      </section>

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
          <div class="combat-targets__vitals">
            <i aria-hidden="true"><b :style="{ width: `${combatEnemyHealthRatio(enemy)}%` }" /></i>
            <small>{{ Math.ceil(enemy.hp) }} / {{ Math.ceil(enemy.maxHp) }} · {{ Math.round(combatEnemyHealthRatio(enemy)) }}%</small>
          </div>
          <span class="combat-targets__portrait" aria-hidden="true">
            <IconGenerator :config="{ id: `target-${enemy.actorId}`, glyph: 'skull', category: 'utility' }" />
          </span>
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
            <IconGenerator :config="{ id: 'training-dummy-target', glyph: 'star', category: 'utility' }" />
            <b>ЦЕЛЬ</b>
          </div>
          <div
            v-else
            class="enemy-placeholder"
            role="img"
            :aria-label="enemyPresentation.name"
          >
            <IconGenerator :config="{ id: 'enemy-placeholder', glyph: 'skull', category: 'utility' }" />
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

        <div v-if="companion" class="companion-figure" :data-dead="companion.hp <= 0">
          <div class="companion-figure__portrait">
            <img v-if="companionArt" :src="companionArt" alt="" />
            <IconGenerator v-else :config="{ id: `companion-${companion.actorId}`, glyph: 'star', category: 'utility' }" />
          </div>
          <div class="companion-figure__state">
            <strong>{{ companion.name }}</strong>
            <i><span :style="{ width: `${combatPlayerHealthRatio(companion)}%` }" /></i>
            <small>{{ Math.ceil(companion.hp) }} / {{ Math.ceil(companion.maxHp) }}</small>
          </div>
        </div>

      </section>

      <section v-if="isTraining" class="training-stats" aria-label="Статистика тренировки">
        <div><small>ВРЕМЯ</small><strong>{{ trainingElapsedSeconds.toFixed(1) }}с</strong></div>
        <div><small>Урон/с</small><strong>{{ Math.round(trainingDps).toLocaleString('ru-RU') }}</strong></div>
        <div><small>УРОН</small><strong>{{ Math.round(combat.trainingStats.totalDamage).toLocaleString('ru-RU') }}</strong></div>
        <div><small>КРИТЫ</small><strong>{{ combat.trainingStats.criticalHits }}</strong></div>
        <div><small>МАКС.</small><strong>{{ Math.round(combat.trainingStats.maxHit).toLocaleString('ru-RU') }}</strong></div>
      </section>

      <section v-if="isParticipantActive" class="combat-actions">
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
            {{ snapshot.player.autoAttackEnabled ? 'Автоатака · ВКЛ' : 'Автоатака · ВЫКЛ' }}
          </span>
        </div>

        <div v-if="isMage" class="pyro-state" aria-label="Состояние пироманта">
          <span v-if="heatLimit" class="hot">
            Предел жара · {{ effectRemaining(heatLimit.expiresAtUtc).toFixed(1) }}с
          </span>
          <span v-if="combustion" class="hot">
            Возгорание · {{ effectRemaining(combustion.expiresAtUtc).toFixed(1) }}с
          </span>
        </div>

        <div v-if="playerCast" class="cast-bar cast-bar--player" data-player-cast>
          <div>
            <strong>{{ abilityName(playerCast.abilityId) }}</strong>
            <small>{{ castRemaining(playerCast).toFixed(1) }}с</small>
          </div>
          <i><span :style="{ width: `${castProgress(playerCast)}%` }" /></i>
        </div>

        <div
          class="cast-bar cast-bar--autoattack"
          :class="{ inactive: !snapshot.player.autoAttackEnabled }"
          data-autoattack-cast
        >
          <div>
            <strong>Автоатака</strong>
            <small v-if="snapshot.player.autoAttackEnabled">
              {{ playerCast ? 'ПАУЗА' : `${autoAttackRemaining.toFixed(1)}с` }}
            </small>
            <small v-else>ВЫКЛ</small>
          </div>
          <i>
            <span :style="{ width: `${autoAttackProgress}%` }" />
          </i>
        </div>

        <div class="ability-row" aria-label="Боевые способности">
          <button
            v-for="(ability, index) in abilitySlots"
            :key="ability?.id ?? `empty-${index}`"
            type="button"
            class="ability-slot"
            :class="{ 'ability-slot--empty': !ability, 'ability-slot--comet': ability?.id === 'FIRE_COMET', 'ability-slot--queued': ability && combat.abilityQueue.includes(ability.id) }"
            :data-ability-slot="ability?.id ?? ''"
            :data-state="ability ? abilityState(ability) : 'empty'"
            :disabled="!ability || abilityState(ability) !== 'ready'"
            :aria-label="ability?.displayName ?? 'Пустой слот способности'"
            @click="ability && combat.useAbility(ability.id)"
          >
            <span class="ability-slot__icon">
              <img v-if="ability && abilityIcon(ability)" :src="abilityIcon(ability)" alt="" />
              <IconGenerator
                v-else-if="ability"
                :config="{ id: `ability-${ability.id}`, glyph: abilityGlyph(ability), category: 'skill' }"
              />
              <i v-else />
            </span>
            <span
              v-if="ability?.id === 'MAGE_FIREBALL'"
              class="ability-slot__markers"
              :aria-label="`Криты Огненного шара: ${fireballStreak} из 3`"
            >
              <i v-for="marker in 3" :key="marker" :data-filled="marker <= fireballStreak" />
            </span>
            <span v-if="ability?.id === 'FIRE_COMET' && heatLimit" class="ability-slot__proc" aria-label="Предел жара активен">ЖАР</span>
            <span v-if="ability?.id === 'COMBUSTION' && combustion" class="ability-slot__proc" aria-label="Возгорание активно">АКТ.</span>
            <span v-if="ability && combat.abilityQueue.includes(ability.id)" class="ability-slot__queue">{{ combat.abilityQueue.indexOf(ability.id) + 1 }}</span>
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
            <span class="utility-action__icon">
              <IconGenerator :config="{ id: `consumable-${item.definitionId}`, glyph: consumableGlyph(item), category: 'consumable' }" />
            </span>
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
            <span class="utility-action__icon">
              <IconGenerator :config="{ id: 'combat-auto-attack', glyph: 'sword', category: 'utility' }" />
            </span>
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
            <span class="utility-action__icon">
              <IconGenerator :config="{ id: 'combat-training-reset', glyph: 'refresh', category: 'utility' }" />
            </span>
            <div><strong>Сброс</strong><small>Тренировка</small></div>
          </button>

          <button
            v-if="!isTraining"
            type="button"
            class="utility-action utility-action--flee"
            data-flee-combat
            :disabled="combat.pending"
            @click="fleeCombat"
          >
            <span class="utility-action__icon">
              <IconGenerator :config="{ id: 'combat-flee', glyph: 'boots', category: 'utility' }" />
            </span>
            <div>
              <strong>Сбежать</strong>
              <small>Остаться в локации</small>
            </div>
          </button>

          <button
            v-if="isTraining"
            type="button"
            class="utility-action utility-action--leave"
            data-leave-combat
            :disabled="combat.pending"
            @click="leaveCombat"
          >
            <span class="utility-action__icon">
              <IconGenerator :config="{ id: 'combat-leave', glyph: 'close', category: 'utility' }" />
            </span>
            <div>
              <strong>{{ isTraining ? 'Завершить' : 'Покинуть бой' }}</strong>
              <small>{{ isTraining ? 'Тренировку' : 'Выход' }}</small>
            </div>
          </button>
        </div>
      </section>

      <section
        v-if="lootRolls.length"
        class="loot-rolls"
        aria-label="Розыгрыш ценной добычи"
        data-loot-rolls
      >
        <article v-for="roll in lootRolls" :key="roll.lootRollId" class="loot-roll">
          <div class="loot-roll__heading">
            <div>
              <small>ЦЕННАЯ ДОБЫЧА · {{ roll.rarity }}</small>
              <strong>{{ roll.name }}<span v-if="roll.quantity > 1"> ×{{ roll.quantity }}</span></strong>
            </div>
            <time>{{ Math.ceil(lootRollRemaining(roll.endsAtUtc)) }}с</time>
          </div>
          <div class="loot-roll__actions">
            <button
              type="button"
              :disabled="combat.pending || !roll.canNeed"
              :title="roll.canNeed ? 'Приоритетный бросок' : 'Персонаж не может использовать этот предмет'"
              @click="chooseLootRoll(roll.lootRollId, 'Need')"
            >
              <strong>НУЖНО</strong><small>Приоритетный бросок</small>
            </button>
            <button type="button" :disabled="combat.pending" @click="chooseLootRoll(roll.lootRollId, 'Greed')">
              <strong>ПРЕТЕНДОВАТЬ</strong><small>Обычный бросок</small>
            </button>
            <button type="button" :disabled="combat.pending" @click="chooseLootRoll(roll.lootRollId, 'Pass')">
              <strong>ОТКАЗАТЬСЯ</strong><small>Не участвовать</small>
            </button>
          </div>
        </article>
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
        {{ combatErrorMessage }}
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

.companion-figure { position: absolute; bottom: .6rem; left: 7.6rem; z-index: 3; display: grid; grid-template-columns: 2.5rem minmax(5rem, 1fr); align-items: center; gap: 6px; max-width: 10rem; padding: 5px 7px; border: 1px solid rgb(79 185 150 / 34%); border-radius: var(--ui-radius-md); background: rgb(5 12 14 / 84%); backdrop-filter: blur(6px); }
.companion-figure[data-dead='true'] { opacity: .48; }
.companion-figure__portrait { display: grid; width: 2.5rem; height: 2.5rem; place-items: center; overflow: hidden; border-radius: 50%; background: rgb(79 185 150 / 12%); color: #9be2c9; }
.companion-figure__portrait img { width: 100%; height: 100%; object-fit: contain; }
.companion-figure__state { display: grid; min-width: 0; gap: 2px; }
.companion-figure__state strong { overflow: hidden; font-size: .55rem; text-overflow: ellipsis; white-space: nowrap; }
.companion-figure__state small { color: var(--ui-color-text-muted); font-size: .44rem; }
.companion-figure__state i { display: block; height: 4px; overflow: hidden; border-radius: 999px; background: rgb(0 0 0 / 55%); }
.companion-figure__state i span { display: block; height: 100%; background: #4fb996; }

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

.cast-bar--autoattack {
  padding: 6px 8px;
  border: 1px solid rgb(79 185 150 / 28%);
  border-radius: var(--ui-radius-sm);
  background: rgb(10 24 22 / 54%);
}

.cast-bar--autoattack > div strong {
  color: #9be2c9;
}

.cast-bar--autoattack > i > span {
  background: linear-gradient(90deg, #3d8f76, #84d5bb);
}

.cast-bar--autoattack.inactive {
  border-color: var(--ui-color-border);
  background: rgb(4 7 12 / 52%);
  opacity: .58;
}

.cast-bar--autoattack.inactive > div strong {
  color: var(--ui-color-text-muted);
}

.cast-bar--autoattack.inactive > i > span {
  width: 0 !important;
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

.combat-feedback[data-side='ally'] { color: #9be2c9; }

.ability-slot--queued { border-color: rgb(155 226 201 / 55%); box-shadow: inset 0 0 0 1px rgb(155 226 201 / 18%); }
.ability-slot__queue { position: absolute; top: 3px; left: 3px; z-index: 5; display: grid; width: 1rem; height: 1rem; place-items: center; border-radius: 50%; background: #9be2c9; color: #07110e; font-size: .48rem; font-weight: 900; }

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

.combat-join {
  display: grid;
  gap: 6px;
  padding: 12px;
  border: 1px solid rgb(215 168 88 / 38%);
  border-radius: var(--ui-radius-lg);
  background: rgb(35 25 14 / 92%);
  color: var(--ui-color-text-muted);
  font-size: .68rem;
}

.combat-join strong {
  color: #f3d18c;
  font-family: var(--ui-font-display);
  font-size: .8rem;
}

.combat-join button {
  min-height: 38px;
  border: 1px solid rgb(215 168 88 / 58%);
  border-radius: var(--ui-radius-md);
  background: rgb(215 168 88 / 16%);
  color: #ffe8b2;
  font: inherit;
  font-weight: 800;
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

.utility-action--flee {
  border-color: rgb(219 164 83 / 28%);
}

.utility-action:disabled {
  opacity: .4;
}

.loot-rolls {
  display: grid;
  gap: 6px;
}

.loot-roll {
  display: grid;
  gap: 8px;
  padding: 9px;
  border: 1px solid rgb(219 164 83 / 42%);
  border-radius: var(--ui-radius-md);
  background: linear-gradient(135deg, rgb(72 48 21 / 72%), rgb(12 13 20 / 96%));
}

.loot-roll__heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 8px;
}

.loot-roll__heading > div {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.loot-roll__heading small {
  color: #e4bc76;
  font-size: .43rem;
  font-weight: 800;
  letter-spacing: .06em;
}

.loot-roll__heading strong {
  overflow: hidden;
  color: var(--ui-color-text-primary);
  font-size: .64rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.loot-roll__heading time {
  flex: 0 0 auto;
  color: #e4bc76;
  font-size: .54rem;
  font-weight: 800;
}

.loot-roll__actions {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 5px;
}

.loot-roll__actions button {
  display: grid;
  min-width: 0;
  gap: 2px;
  padding: 6px 5px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: rgb(5 7 12 / 72%);
  color: var(--ui-color-text-secondary);
  font: inherit;
  text-align: left;
}

.loot-roll__actions button:first-child {
  border-color: rgb(219 164 83 / 50%);
}

.loot-roll__actions button strong {
  font-size: .47rem;
}

.loot-roll__actions button small {
  color: var(--ui-color-text-muted);
  font-size: .39rem;
}

.loot-roll__actions button:disabled {
  opacity: .45;
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

/* Combat is a focused scene: the battlefield leads, the action dock follows. */
.combat-screen {
  gap: 6px;
  padding-top: 6px;
  background:
    radial-gradient(circle at 50% 18%, rgb(115 74 94 / 13%), transparent 19rem),
    linear-gradient(180deg, #08090e, #04060a);
}

.combat-hud__actor {
  border-radius: var(--ui-radius-sm);
  background: rgb(8 12 17 / 88%);
}

.combat-hud__actor--player {
  border-color: rgb(182 161 236 / 34%);
}

.combat-hud__actor--enemy {
  border-color: rgb(216 95 114 / 42%);
}

.combat-targets button {
  border-radius: var(--ui-radius-sm);
  background: rgb(8 11 16 / 88%);
}

.battlefield {
  min-height: 21rem;
  border-color: rgb(205 177 113 / 38%);
  border-radius: var(--ui-radius-md);
  box-shadow:
    inset 0 0 42px rgb(0 0 0 / 48%),
    0 16px 34px rgb(0 0 0 / 34%);
}

.battlefield::after {
  position: absolute;
  right: 7%;
  bottom: 10%;
  left: 7%;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgb(205 177 113 / 34%), transparent);
  content: '';
}

.enemy-figure {
  top: 3.2rem;
  height: 13rem;
}

.player-figure {
  bottom: -1.1rem;
  width: 8.2rem;
  height: 10.5rem;
  opacity: .88;
}

.combat-feedback {
  bottom: 4.5rem;
  border: 1px solid rgb(205 177 113 / 24%);
  border-radius: var(--ui-radius-sm);
  background: rgb(5 8 13 / 84%);
}

.combat-actions {
  position: sticky;
  bottom: 6px;
  z-index: 5;
  gap: 6px;
  padding: 7px;
  border-color: rgb(205 177 113 / 32%);
  border-radius: var(--ui-radius-md);
  background:
    linear-gradient(180deg, rgb(22 25 30 / 97%), rgb(7 10 15 / 98%));
  box-shadow: 0 -8px 24px rgb(0 0 0 / 32%), inset 0 1px 0 rgb(255 255 255 / 5%);
  backdrop-filter: blur(10px);
}

.ability-row {
  gap: 3px;
}

.ability-slot {
  min-height: 66px;
  border-color: rgb(205 177 113 / 19%);
  border-radius: var(--ui-radius-sm);
  background: linear-gradient(180deg, rgb(35 38 42 / 80%), rgb(5 8 12 / 92%));
}

.ability-slot[data-state='ready'] {
  border-color: rgb(182 161 236 / 48%);
}

.ability-slot__icon {
  border-radius: var(--ui-radius-sm);
}

.utility-action {
  min-height: var(--ui-touch-target);
  border-radius: var(--ui-radius-sm);
  background: rgb(5 8 12 / 78%);
}

.loot-roll {
  border-radius: var(--ui-radius-sm);
  background: linear-gradient(135deg, rgb(62 43 22 / 88%), rgb(10 12 17 / 97%));
}

.combat-log {
  border: 0;
  border-top: 1px solid rgb(205 177 113 / 18%);
  border-radius: 0;
  background: transparent;
}

.combat-log__toggle {
  min-height: var(--ui-touch-target);
  padding-inline: 2px;
}

.combat-log ol {
  padding-inline: 2px;
}

@media (max-width: 390px) {
  .battlefield {
    min-height: 19rem;
  }

  .enemy-figure {
    height: 11.5rem;
  }

  .ability-slot {
    min-height: 60px;
  }
}

@media (max-width: 340px) {
  .battlefield {
    min-height: 18rem;
  }
}

.combat-party-roster {
  display: grid;
  gap: 6px;
  padding: 7px;
  border: 1px solid rgb(205 177 113 / 28%);
  border-radius: var(--ui-radius-md);
  background: linear-gradient(110deg, rgb(28 28 35 / 96%), rgb(8 11 17 / 96%));
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 5%);
}

.combat-party-roster__header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 8px;
  padding-inline: 2px;
}

.combat-party-roster__header > div {
  display: grid;
  gap: 1px;
}

.combat-party-roster__header small {
  color: var(--ui-color-gold);
  font-size: .48rem;
  font-weight: 800;
  letter-spacing: .1em;
}

.combat-party-roster__header strong {
  font-family: var(--ui-font-display);
  font-size: .7rem;
}

.combat-party-roster__header > span {
  color: var(--ui-color-text-muted);
  font-size: .5rem;
}

.combat-party-roster__grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 5px;
}

.combat-party-roster__member {
  display: grid;
  grid-template-columns: 2rem minmax(0, 1fr) auto;
  align-items: center;
  gap: 6px;
  min-width: 0;
  padding: 6px;
  border: 1px solid rgb(255 255 255 / 8%);
  border-radius: var(--ui-radius-sm);
  background: rgb(5 8 13 / 82%);
}

.combat-party-roster__member--self {
  border-color: rgb(170 163 255 / 42%);
  background: linear-gradient(105deg, rgb(146 136 255 / 11%), rgb(5 8 13 / 88%));
}

.combat-party-roster__member[data-status='Fled'],
.combat-party-roster__member[data-status='Dead'] {
  opacity: .58;
}

.combat-party-roster__crest {
  display: grid;
  width: 2rem;
  height: 2rem;
  place-items: center;
  border: 1px solid rgb(205 177 113 / 42%);
  border-radius: 50%;
  background: radial-gradient(circle, rgb(205 177 113 / 20%), rgb(9 12 18 / 96%) 68%);
  color: #ecd797;
  font-family: var(--ui-font-display);
  font-size: .8rem;
}

.combat-party-roster__member--self .combat-party-roster__crest {
  border-color: rgb(170 163 255 / 56%);
  color: #d6d2ff;
}

.combat-party-roster__body {
  display: grid;
  min-width: 0;
  gap: 3px;
}

.combat-party-roster__identity {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 5px;
  min-width: 0;
}

.combat-party-roster__identity strong {
  overflow: hidden;
  font-size: .62rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-party-roster__identity small,
.combat-party-roster__vitals {
  color: var(--ui-color-text-muted);
  font-size: .46rem;
  white-space: nowrap;
}

.combat-party-roster__bar {
  display: block;
  height: 4px;
  overflow: hidden;
  border-radius: var(--ui-radius-round);
  background: rgb(255 255 255 / 8%);
}

.combat-party-roster__bar i {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, #7acda8, #d4cf8b);
  transition: width .25s ease;
}

.combat-party-roster__member[data-status='Fled'] .combat-party-roster__bar i,
.combat-party-roster__member[data-status='Dead'] .combat-party-roster__bar i {
  background: #a76b78;
}

.combat-party-roster__state {
  color: #d4cf8b;
  font-size: .8rem;
  font-weight: 700;
}

.combat-screen--party .player-figure {
  left: 3%;
}

@media (max-width: 520px) {
  .combat-party-roster__grid {
    display: flex;
    overflow-x: auto;
    padding-bottom: 2px;
  }

  .combat-party-roster__member {
    flex: 0 0 min(12rem, 78vw);
  }
}

/* Combat controls stay readable and tappable on Telegram-sized screens. */
.combat-targets button {
  min-height: var(--ui-touch-target);
  grid-template-columns: 2.1rem minmax(0, 1fr);
  grid-template-rows: auto auto;
  align-items: center;
  column-gap: 7px;
  padding: 5px 7px;
}

.combat-targets button > span:first-child {
  grid-column: 2;
  font-size: var(--ui-font-size-xs);
}

.combat-targets__vitals {
  display: grid;
  grid-column: 2;
  gap: 2px;
}

.combat-targets__vitals i {
  display: block;
  height: 4px;
  overflow: hidden;
  border-radius: var(--ui-radius-round);
  background: rgb(255 255 255 / 9%);
}

.combat-targets__vitals i b {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, #a44e62, #e38d98);
}

.combat-targets__vitals small {
  font-size: var(--ui-font-size-xs);
}

.combat-targets__portrait {
  display: grid;
  width: 2rem;
  height: 2rem;
  grid-column: 1;
  grid-row: 1 / 3;
  place-items: center;
}

.combat-targets__portrait :deep(.icon-generator) {
  border-color: rgb(216 95 114 / 35%);
  color: #efa1ae;
}

.combat-join button,
.combat-log__toggle,
.utility-action {
  min-height: var(--ui-touch-target);
}

.combat-join,
.combat-join strong,
.combat-party-roster__identity strong,
.combat-party-roster__identity small,
.combat-party-roster__vitals,
.utility-action strong,
.utility-action small,
.combat-log__toggle small,
.combat-log li,
.combat-log li strong,
.combat-log li small,
.cast-bar strong,
.cast-bar small,
.pyro-state,
.autoattack-state,
.ability-slot > small {
  font-size: var(--ui-font-size-xs);
}

.combat-party-roster__header strong {
  font-size: var(--ui-font-size-sm);
}

.ability-slot {
  min-height: var(--ui-control-height-md);
}

.ability-slot > small {
  line-height: 1.15;
}

.ability-slot__icon {
  position: relative;
}

.ability-slot__markers {
  position: absolute;
  top: 1px;
  left: 50%;
  z-index: 2;
  display: flex;
  gap: 2px;
  padding: 2px 3px;
  border: 1px solid rgb(240 139 99 / 42%);
  border-radius: var(--ui-radius-round);
  background: rgb(18 8 7 / 90%);
  transform: translate(-50%, -50%);
}

.ability-slot__markers i {
  width: 5px;
  height: 5px;
  border: 1px solid #f08b63;
  border-radius: 50%;
  background: transparent;
}

.ability-slot__markers i[data-filled='true'] {
  background: #f08b63;
  box-shadow: 0 0 5px rgb(240 139 99 / 70%);
}

.ability-slot__proc {
  position: absolute;
  right: 2px;
  top: 2px;
  z-index: 2;
  padding: 1px 3px;
  border-radius: 4px;
  background: rgb(240 139 99 / 86%);
  color: #170b08;
  font-size: .55rem;
  font-weight: 900;
}

.utility-action__icon {
  display: grid;
  width: 2rem;
  height: 2rem;
  flex: 0 0 2rem;
  place-items: center;
}

.utility-action__icon :deep(.icon-generator) {
  border: 0;
  background: transparent;
  box-shadow: none;
}

.training-stats small {
  font-size: var(--ui-font-size-xs);
}

/* Actionable combat information stays readable at Telegram's smallest viewport. */
.combat-hud__identity small,
.combat-targets button > span:first-child,
.combat-targets button small,
.effect-strip > span,
.effect-strip > span b,
.effect-strip > span small,
.effect-strip__empty,
.combat-feedback,
.training-stats small,
.training-stats strong,
.loot-roll__heading small,
.loot-roll__heading strong,
.loot-roll__heading time,
.loot-roll__actions button strong,
.loot-roll__actions button small,
.combat-log__toggle b,
.combat-log__toggle small,
.combat-log li .actor,
.combat-log li strong,
.combat-log li small,
.error,
.combat-party-roster__header small,
.combat-party-roster__header > span,
.combat-party-roster__identity strong,
.combat-party-roster__identity small,
.combat-party-roster__vitals,
.utility-action strong,
.utility-action small,
.cast-bar strong,
.cast-bar small,
.pyro-state,
.autoattack-state,
.ability-slot > small,
.ability-slot__proc {
  font-size: var(--ui-font-size-xs);
}

.combat-targets button > span:first-child {
  font-weight: 800;
}
</style>
