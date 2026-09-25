<script setup lang="ts">
import { computed, onUnmounted, shallowRef } from 'vue'

import type { CombatAbility, CombatCastSnapshot, InventoryItem } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import { isAuraAbility } from '@/game/combat/combatAbilityGroups'
import { orderCombatAbilities } from '@/game/combat/combatHotbarSettings'
import BattleArena from '@/game/combat/components/BattleArena.vue'
import BattleControls from '@/game/combat/components/BattleControls.vue'
import BattleHeader from '@/game/combat/components/BattleHeader.vue'
import CombatLog from '@/game/combat/components/CombatLog.vue'
import ConsumableBar from '@/game/combat/components/ConsumableBar.vue'
import SkillPanel from '@/game/combat/components/SkillPanel.vue'
import { useBattle } from '@/game/combat/composables/useBattle'
import CombatEffectStrip from '@/game/combat/CombatEffectStrip.vue'
import ItemIcon from '@/game/items/components/ItemIcon.vue'
import { locationPresentation } from '@/game/world/locationPresentation'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIModal } from '@/ui/components'

const emit = defineEmits<{ leave: [] }>()
const battle = useBattle()
const session = useGameSessionStore()
const now = shallowRef(Date.now())
const fleeConfirmationOpen = shallowRef(false)
const timer = window.setInterval(() => {
  now.value = Date.now()
}, 100)

const snapshot = battle.snapshot
const localActor = computed(() => snapshot.value?.player ?? null)
const selectedEnemy = battle.selectedEnemy
const activeEnemies = computed(() => battle.enemies.value.filter((enemy) => enemy.hp > 0))
const displayEnemies = computed(() =>
  activeEnemies.value.length ? activeEnemies.value : battle.enemies.value,
)
const activeAbilities = computed(() => {
  const abilities = (localActor.value?.abilities ?? []).filter(
    (ability) => !isAuraAbility(ability.id),
  )
  return orderCombatAbilities(session.snapshot?.character?.id ?? '', abilities).slice(0, 12)
})
const queuedAbilityIds = computed(() => battle.abilityQueue.value.map((queued) => queued.abilityId))
const needsLayoutScroll = computed(() => activeAbilities.value.length > 4)
const battlefieldArt = computed(() => {
  const locationId = session.snapshot?.world?.currentLocation.id
  return locationId ? locationPresentation(locationId).art : gameArt.world.combatWhispering
})
const combatConsumables = computed(() =>
  (session.snapshot?.character?.inventory.items ?? [])
    .filter((item) => item.type === 'Consumable' && item.quantity > 0)
    .filter((item) =>
      item.consumableActions.every(
        (action) =>
          action.type !== 'RestoreResource' ||
          action.resourceType === localActor.value?.resourceType,
      ),
    ),
)
const playerCast = computed(() => localActor.value?.activeCast ?? null)
const enemyCast = computed(() => selectedEnemy.value?.activeCast ?? null)
const enemyCastUnblockable = computed(() => {
  const cast = enemyCast.value
  return (
    cast !== null &&
    selectedEnemy.value?.abilities.some(
      (ability) => ability.id === cast.abilityId && ability.isUnblockable === true,
    ) === true
  )
})
const isActive = computed(() => snapshot.value?.status === 'Active')
const combatErrorMessage = computed(() => {
  switch (battle.errorCode.value) {
    case 'combat_ability_on_cooldown':
      return 'Способность ещё восстанавливается.'
    case 'combat_insufficient_resource':
      return 'Недостаточно ресурса для этой способности.'
    case 'combat_invalid_target':
      return 'Выберите доступную цель.'
    case 'combat_actor_dead':
      return 'Павший герой не может действовать.'
    case 'combat_not_found':
      return 'Бой уже завершён. Вернитесь в локацию.'
    default:
      return 'Не удалось выполнить действие. Проверьте связь и попробуйте ещё раз.'
  }
})
const trainingElapsedSeconds = computed(() => {
  const startedAt = battle.trainingStats.value.startedAtUtc
  return startedAt ? Math.max(0, (now.value - Date.parse(startedAt)) / 1_000) : 0
})
const trainingDps = computed(() =>
  trainingElapsedSeconds.value > 0
    ? battle.trainingStats.value.totalDamage / trainingElapsedSeconds.value
    : 0,
)

function abilityName(abilityId: string): string {
  const ability = [
    ...(localActor.value?.abilities ?? []),
    ...(selectedEnemy.value?.abilities ?? []),
  ].find((candidate) => candidate.id === abilityId)
  if (ability) return ability.displayName
  if (abilityId === 'AUTO_ATTACK') return 'Автоатака'
  return 'Способность'
}

function castRemaining(cast: CombatCastSnapshot | null): number {
  return cast ? Math.max(0, (Date.parse(cast.resolvesAtUtc) - now.value) / 1_000) : 0
}

function castProgress(cast: CombatCastSnapshot | null): number {
  if (!cast) return 0
  const start = Date.parse(cast.startedAtUtc)
  const duration = Math.max(1, Date.parse(cast.resolvesAtUtc) - start)
  return Math.max(0, Math.min(100, ((now.value - start) / duration) * 100))
}

function consumableCooldownRemaining(item: InventoryItem): number {
  const category = item.consumableCooldownCategoryId
  const readyAt = category ? localActor.value?.consumableCooldowns?.[category] : null
  return readyAt ? Math.max(0, Date.parse(readyAt) - now.value) : 0
}

function lootRollRemaining(endsAtUtc: string): number {
  return Math.max(0, (Date.parse(endsAtUtc) - now.value) / 1_000)
}

function consumableCanAffect(item: InventoryItem): boolean {
  const player = localActor.value
  if (!player) return false
  return item.consumableActions.some((action) => {
    if (action.type === 'RestoreHp') return player.hp < player.maxHp
    if (action.type === 'RestoreResource') {
      return action.resourceType === player.resourceType && player.resource < player.maxResource
    }
    return true
  })
}

async function useAbility(ability: CombatAbility): Promise<void> {
  await battle.useAbility(ability.id)
}

async function useConsumable(item: InventoryItem): Promise<void> {
  await battle.useConsumable(item.definitionId)
  await session.refreshSnapshot()
}

async function leaveBattle(): Promise<void> {
  if (await battle.leave()) emit('leave')
}

async function fleeBattle(): Promise<void> {
  fleeConfirmationOpen.value = false
  if (await battle.flee()) emit('leave')
}

function rarityLabel(rarity: string): string {
  return (
    (
      {
        Common: 'Обычная',
        Uncommon: 'Необычная',
        Rare: 'Редкая',
        Epic: 'Эпическая',
        Legendary: 'Легендарная',
        Unique: 'Уникальная',
      } as Record<string, string>
    )[rarity] ?? 'Ценная'
  )
}

onUnmounted(() => window.clearInterval(timer))
</script>

<template>
  <section
    class="battle-screen"
    :class="{ 'battle-screen--scroll-fallback': needsLayoutScroll }"
    :data-party-size="battle.allies.value.length || undefined"
    :data-skill-rows="Math.max(1, Math.ceil(activeAbilities.length / 4))"
    data-battle-screen
  >
    <template v-if="snapshot && localActor && selectedEnemy">
      <BattleHeader
        :local-actor="localActor"
        :enemy="selectedEnemy"
        :allies="battle.allies.value"
        :selected-friendly-actor-id="battle.selectedFriendlyActorId.value"
        :aggro-actor-ids="battle.aggroActorIds.value"
        :disabled="battle.pending.value || !isActive"
        @select-friendly="battle.selectFriendlyActor"
      />

      <section
        v-if="battle.isAwaitingAttachment.value"
        class="battle-screen__join"
        data-combat-join
      >
        <div>
          <strong>Бой уже идёт</strong><small>Войдите, чтобы присоединиться к группе.</small>
        </div>
        <UIButton :disabled="battle.pending.value" @click="battle.attachCombat(snapshot.sessionId)"
          >Войти в бой</UIButton
        >
      </section>

      <div class="battle-screen__arena-wrap">
        <BattleArena
          :allies="battle.allies.value"
          :enemies="displayEnemies"
          :local-actor-id="localActor.actorId"
          :selected-friendly-actor-id="battle.selectedFriendlyActorId.value"
          :selected-enemy-actor-id="selectedEnemy.actorId"
          :aggro-actor-ids="battle.aggroActorIds.value"
          :numbers="battle.eventProjection.value.numbers"
          :companion="battle.companion.value"
          :battlefield-art="battlefieldArt"
          :disabled="battle.pending.value || !isActive"
          @select-friendly="battle.selectFriendlyActor"
          @select-enemy="battle.selectEnemyActor"
        />

        <CombatEffectStrip
          v-if="selectedEnemy.effects.length"
          class="battle-screen__enemy-effects"
          :effects="selectedEnemy.effects"
          :now="now"
          side="enemy"
        />

        <div
          v-if="enemyCast"
          class="battle-cast battle-cast--enemy"
          :class="{ 'battle-cast--unblockable': enemyCastUnblockable }"
          data-enemy-cast
        >
          <div>
            <strong>{{ abilityName(enemyCast.abilityId) }}</strong
            ><small
              >{{ enemyCastUnblockable ? 'НЕБЛОКИРУЕМО · ' : ''
              }}{{ castRemaining(enemyCast).toFixed(1) }}с</small
            >
          </div>
          <i><span :style="{ width: `${castProgress(enemyCast)}%` }" /></i>
        </div>
      </div>

      <section
        v-if="battle.isTraining.value"
        class="battle-screen__training"
        aria-label="Статистика тренировки"
      >
        <div>
          <small>Время</small><b>{{ trainingElapsedSeconds.toFixed(1) }}с</b>
        </div>
        <div>
          <small>Урон/с</small><b>{{ Math.round(trainingDps).toLocaleString('ru-RU') }}</b>
        </div>
        <div>
          <small>Урон</small
          ><b>{{ Math.round(battle.trainingStats.value.totalDamage).toLocaleString('ru-RU') }}</b>
        </div>
        <div>
          <small>Криты</small><b>{{ battle.trainingStats.value.criticalHits }}</b>
        </div>
        <div>
          <small>Макс. удар</small
          ><b>{{ Math.round(battle.trainingStats.value.maxHit).toLocaleString('ru-RU') }}</b>
        </div>
      </section>

      <section v-if="battle.isParticipantActive.value && isActive" class="battle-screen__actions">
        <CombatEffectStrip
          v-if="localActor.effects.length"
          :effects="localActor.effects"
          :now="now"
          side="player"
        />
        <div v-if="playerCast" class="battle-cast battle-cast--player" data-player-cast>
          <div>
            <strong>{{ abilityName(playerCast.abilityId) }}</strong
            ><small>{{ castRemaining(playerCast).toFixed(1) }}с</small>
          </div>
          <i><span :style="{ width: `${castProgress(playerCast)}%` }" /></i>
        </div>
        <SkillPanel
          :abilities="activeAbilities"
          :cooldowns="localActor.cooldowns"
          :resource="localActor.resource"
          :queued-ability-ids="queuedAbilityIds"
          :now="now"
          :disabled="battle.pending.value"
          @use="useAbility"
        />
        <ConsumableBar
          v-if="!battle.isTraining.value"
          :items="combatConsumables"
          :cooldown-remaining="consumableCooldownRemaining"
          :can-use="consumableCanAffect"
          :disabled="battle.pending.value"
          @use="useConsumable"
        />
        <BattleControls
          :auto-attack-enabled="localActor.autoAttackEnabled"
          :disabled="battle.pending.value"
          :training="battle.isTraining.value"
          @toggle-auto-attack="battle.toggleAutoAttack"
          @flee="fleeConfirmationOpen = true"
          @reset-training="battle.resetTraining"
          @leave="leaveBattle"
        />
      </section>

      <section v-if="!isActive" class="battle-screen__result" :data-result="snapshot.status">
        <strong>{{
          snapshot.status === 'Victory'
            ? 'Победа'
            : snapshot.status === 'Defeat'
              ? 'Поражение'
              : 'Бой завершён'
        }}</strong>
        <span v-if="battle.reward.value">Награда получена</span>
        <UIButton @click="leaveBattle">Вернуться в мир</UIButton>
      </section>

      <section
        v-if="battle.lootRolls.value.length"
        class="battle-screen__loot"
        data-loot-rolls
        aria-label="Розыгрыш добычи"
      >
        <article v-for="roll in battle.lootRolls.value" :key="roll.lootRollId">
          <ItemIcon
            :icon-id="roll.iconId"
            :item-id="roll.itemId"
            :name="roll.name"
            :type="roll.type ?? 'Equipment'"
            :rarity="roll.rarity"
          />
          <div>
            <small>{{ rarityLabel(roll.rarity) }}</small
            ><strong
              >{{ roll.name }}<span v-if="roll.quantity > 1"> ×{{ roll.quantity }}</span></strong
            ><time :datetime="roll.endsAtUtc"
              >{{ Math.ceil(lootRollRemaining(roll.endsAtUtc)) }}с</time
            >
          </div>
          <div class="loot-roll__actions">
            <button
              type="button"
              :disabled="battle.pending.value || !roll.canNeed"
              @click="battle.chooseLootRoll(roll.lootRollId, 'Need')"
            >
              Нужно
            </button>
            <button
              type="button"
              :disabled="battle.pending.value"
              @click="battle.chooseLootRoll(roll.lootRollId, 'Greed')"
            >
              Претендовать
            </button>
            <button
              type="button"
              :disabled="battle.pending.value"
              @click="battle.chooseLootRoll(roll.lootRollId, 'Pass')"
            >
              Отказаться
            </button>
          </div>
        </article>
      </section>

      <CombatLog :entries="battle.eventProjection.value.logEntries" />
      <p v-if="battle.errorCode.value" class="battle-screen__error" role="alert">
        {{ combatErrorMessage }} <small>{{ battle.errorCode.value }}</small>
      </p>

      <UIModal
        :open="fleeConfirmationOpen"
        title="Сбежать из боя?"
        @close="fleeConfirmationOpen = false"
      >
        <p>После выхода вернуться в этот бой нельзя.</p>
        <template #actions>
          <UIButton variant="ghost" @click="fleeConfirmationOpen = false">Остаться</UIButton>
          <UIButton variant="danger" :loading="battle.pending.value" @click="fleeBattle"
            >Сбежать</UIButton
          >
        </template>
      </UIModal>
    </template>

    <div v-else class="battle-screen__missing">
      <h1>Бой прерван</h1>
      <UIButton @click="emit('leave')">Вернуться в мир</UIButton>
    </div>
  </section>
</template>

<style scoped>
.battle-screen {
  --battle-gap: clamp(0.28rem, 1vw, 0.46rem);
  display: grid;
  width: min(100%, 62rem);
  height: 100svh;
  min-height: 40rem;
  grid-template-rows: auto minmax(0, 1fr) auto auto;
  gap: var(--battle-gap);
  margin-inline: auto;
  padding: max(0.35rem, env(safe-area-inset-top)) max(0.4rem, env(safe-area-inset-right))
    max(0.35rem, env(safe-area-inset-bottom)) max(0.4rem, env(safe-area-inset-left));
  overflow: hidden;
  background: radial-gradient(circle at 50% 18%, rgb(82 59 119 / 13%), transparent 26rem), #05070c;
  color: #eee6da;
}
.battle-screen--scroll-fallback {
  height: auto;
  min-height: 100svh;
  overflow-y: auto;
}
.battle-screen__arena-wrap {
  position: relative;
  min-height: 0;
  max-height: 61.8svh;
}
.battle-screen__arena-wrap :deep(.battle-arena) {
  height: 100%;
  min-height: 0;
}
.battle-screen__enemy-effects {
  position: absolute;
  z-index: 80;
  top: 0.45rem;
  right: 0.45rem;
  max-width: 44%;
}
.battle-screen__actions {
  display: grid;
  gap: 0.3rem;
}
.battle-screen__join,
.battle-screen__result {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  padding: 0.5rem;
  border: 1px solid rgb(182 153 87 / 35%);
  border-radius: 8px;
  background: #0c1018;
}
.battle-screen__join div {
  display: grid;
}
.battle-screen__join small,
.battle-screen__result span {
  color: #99938b;
  font-size: 0.52rem;
}
.battle-screen__training {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 0.25rem;
}
.battle-screen__training div {
  display: grid;
  gap: 0.1rem;
  padding: 0.32rem;
  border: 1px solid rgb(151 124 201 / 24%);
  border-radius: 6px;
  background: #0b0e16;
  text-align: center;
}
.battle-screen__training small {
  color: #8e8991;
  font-size: 0.42rem;
}
.battle-screen__training b {
  font-size: 0.64rem;
}
.battle-cast {
  display: grid;
  gap: 0.2rem;
  padding: 0.3rem 0.45rem;
  border: 1px solid rgb(126 105 190 / 42%);
  border-radius: 7px;
  background: rgb(7 9 16 / 94%);
}
.battle-cast--enemy {
  position: absolute;
  z-index: 90;
  right: 6%;
  bottom: 1.5rem;
  left: 48%;
  border-color: rgb(213 78 99 / 50%);
}
.battle-cast--unblockable {
  border-color: #ff536d;
  box-shadow: 0 0 16px rgb(224 53 79 / 28%);
}
.battle-cast div {
  display: flex;
  justify-content: space-between;
  gap: 0.5rem;
}
.battle-cast strong {
  overflow: hidden;
  font-size: 0.56rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.battle-cast small {
  color: #aaa4a0;
  font-size: 0.48rem;
}
.battle-cast > i {
  display: block;
  height: 4px;
  overflow: hidden;
  border-radius: 999px;
  background: #08090d;
}
.battle-cast > i span {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, #6553ba, #b497f0);
}
.battle-cast--enemy > i span {
  background: linear-gradient(90deg, #9d3148, #eb7182);
}
.battle-screen__loot {
  display: grid;
  gap: 0.35rem;
  padding: 0.45rem;
  border: 1px solid rgb(194 155 69 / 36%);
  border-radius: 8px;
  background: #0d1017;
}
.battle-screen__loot article {
  display: grid;
  grid-template-columns: 44px minmax(0, 1fr);
  gap: 0.4rem;
  align-items: center;
}
.battle-screen__loot :deep(.item-icon) {
  width: 44px;
  height: 44px;
}
.battle-screen__loot article > div {
  display: grid;
}
.battle-screen__loot article small {
  color: #cbb16b;
  font-size: 0.45rem;
}
.battle-screen__loot article strong {
  font-size: 0.62rem;
}
.battle-screen__loot article time {
  color: #aaa4a0;
  font-size: 0.46rem;
}
.loot-roll__actions {
  display: grid !important;
  grid-column: 1 / -1;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.25rem;
}
.loot-roll__actions button {
  min-height: 40px;
  border: 1px solid rgb(190 159 87 / 30%);
  border-radius: 6px;
  background: #090c12;
  color: #ddd5ca;
  font: inherit;
  font-size: 0.5rem;
}
.battle-screen__error {
  margin: 0;
  padding: 0.35rem;
  border: 1px solid rgb(209 75 93 / 42%);
  border-radius: 6px;
  color: #f1a0aa;
  font-size: 0.54rem;
}
.battle-screen__error small {
  color: #98777d;
}
.battle-screen__missing {
  display: grid;
  min-height: 50svh;
  place-content: center;
  gap: 1rem;
  text-align: center;
}

@media (max-width: 390px) and (min-height: 760px) {
  .battle-screen:not(.battle-screen--scroll-fallback) :deep(.skill-panel__header) {
    display: none;
  }
  .battle-screen:not(.battle-screen--scroll-fallback) :deep(.skill-panel) {
    padding: 0.3rem;
  }
  .battle-screen:not(.battle-screen--scroll-fallback) :deep(.skill-panel__ability) {
    min-height: 64px;
  }
}
@media (max-height: 740px) {
  .battle-screen {
    height: auto;
    min-height: 100svh;
    overflow-y: auto;
  }
}
</style>
