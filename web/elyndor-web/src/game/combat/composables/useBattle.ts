import { computed } from 'vue'
import { storeToRefs } from 'pinia'

import type { CombatActorSnapshot } from '@/api/contracts'
import { projectBattleEvents } from '@/game/combat/battleEventPresentation'
import { useCombatSessionStore } from '@/stores/combatSession'

export function useBattle() {
  const combat = useCombatSessionStore()
  const {
    snapshot,
    events,
    selectedFriendlyTargetActorId,
    connectionState,
    pending,
    abilityPending,
    targetPending,
    autoAttackPending,
    fleePending,
    lifecyclePending,
    reward,
    lootRolls,
    errorCode,
    diagnostic,
    trainingStats,
    encounterPresentation,
    abilityQueue,
    isParticipantActive,
    isAwaitingAttachment,
    isTraining,
  } = storeToRefs(combat)

  const allies = computed<CombatActorSnapshot[]>(() => {
    const current = snapshot.value
    if (!current) return []

    const byActorId = new Map<string, CombatActorSnapshot>()
    byActorId.set(current.player.actorId, current.player)
    for (const actor of current.players ?? []) {
      if (actor.kind === 'Player' && !byActorId.has(actor.actorId)) {
        byActorId.set(actor.actorId, actor)
      }
    }
    return [...byActorId.values()]
  })

  const enemies = computed(() => {
    const current = snapshot.value
    if (!current) return []
    return current.enemies ?? [current.enemy]
  })
  const selectedEnemy = computed(() => {
    const current = snapshot.value
    if (!current) return null
    const selectedActorId = current.selectedTargetActorId ?? current.enemy.actorId
    return enemies.value.find((actor) => actor.actorId === selectedActorId) ?? current.enemy
  })
  const selectedFriendlyActorId = computed(() => selectedFriendlyTargetActorId.value)
  const selectedFriendlyActor = computed(
    () => allies.value.find((actor) => actor.actorId === selectedFriendlyActorId.value) ?? null,
  )
  const aggroActorIds = computed(() => [
    ...new Set(
      enemies.value
        .map((enemy) => enemy.currentAggroTargetActorId)
        .filter((actorId): actorId is string => Boolean(actorId)),
    ),
  ])
  const companion = computed(() => snapshot.value?.companion ?? null)
  const actorNames = computed(
    () =>
      new Map(
        [...allies.value, ...enemies.value, ...(companion.value ? [companion.value] : [])].map(
          (actor) => [actor.actorId, actor.name],
        ),
      ),
  )
  const abilityNames = computed(
    () =>
      new Map(
        [
          ...(snapshot.value?.player.abilities ?? []),
          ...enemies.value.flatMap((enemy) => enemy.abilities),
        ].map((ability) => [ability.id, ability.displayName]),
      ),
  )
  const eventProjection = computed(() =>
    projectBattleEvents(events.value, {
      actorNames: actorNames.value,
      abilityNames: abilityNames.value,
      enemyActorIds: new Set(enemies.value.map((enemy) => enemy.actorId)),
      localActorId: snapshot.value?.player.actorId ?? '',
    }),
  )

  function selectFriendlyActor(actorId: string): void {
    combat.selectFriendlyTarget(actorId)
  }

  return {
    snapshot,
    events,
    connectionState,
    pending,
    abilityPending,
    targetPending,
    autoAttackPending,
    fleePending,
    lifecyclePending,
    reward,
    lootRolls,
    errorCode,
    diagnostic,
    trainingStats,
    encounterPresentation,
    abilityQueue,
    isParticipantActive,
    isAwaitingAttachment,
    isTraining,
    allies,
    enemies,
    selectedEnemy,
    selectedFriendlyActorId,
    selectedFriendlyActor,
    aggroActorIds,
    companion,
    eventProjection,
    selectFriendlyActor,
    selectEnemyActor: combat.selectTarget,
    useAbility: combat.useAbility,
    useConsumable: combat.useConsumable,
    toggleAutoAttack: combat.toggleAutoAttack,
    resetTraining: combat.resetTraining,
    attachCombat: combat.attachCombat,
    flee: combat.flee,
    leave: combat.leave,
    chooseLootRoll: combat.chooseLootRoll,
    isConsumablePending: combat.isConsumablePending,
    isLootPending: combat.isLootPending,
  }
}
