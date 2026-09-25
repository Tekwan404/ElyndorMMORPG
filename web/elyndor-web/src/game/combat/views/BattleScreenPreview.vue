<script setup lang="ts">
import { computed, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'

import type { CombatAbility, CombatActorSnapshot } from '@/api/contracts'
import { useCombatSessionStore } from '@/stores/combatSession'
import BattleScreen from './BattleScreen.vue'

const route = useRoute()
const combat = useCombatSessionStore()
const partySize = computed(() => Math.max(1, Math.min(5, Number(route.query.party) || 3)))

const abilities: CombatAbility[] = [
  {
    id: 'HEROIC_STRIKE',
    displayName: 'Героический удар',
    description: 'Мощный удар оружием.',
    iconId: 'warrior-heroic-strike',
    resourceCost: 15,
    cooldownSeconds: 0,
    targetType: 'SingleEnemy',
  },
  {
    id: 'SHIELD_SLAM',
    displayName: 'Удар щитом',
    description: 'Удар и защита.',
    iconId: 'warrior-shield-slam',
    resourceCost: 20,
    cooldownSeconds: 6,
    targetType: 'SingleEnemy',
  },
  {
    id: 'BATTLE_CRY',
    displayName: 'Боевой клич',
    description: 'Усиливает союзников.',
    iconId: 'warrior-battle-shout',
    resourceCost: 10,
    cooldownSeconds: 12,
    targetType: 'SelfAndPartyMembersInCombat',
  },
  {
    id: 'LAST_STAND',
    displayName: 'Последний рубеж',
    description: 'Защитное усиление.',
    iconId: 'warrior-last-stand',
    resourceCost: 0,
    cooldownSeconds: 24,
    targetType: 'Self',
  },
]

function player(index: number): CombatActorSnapshot {
  const classes = ['WARRIOR', 'MAGE', 'ARCHER', 'PALADIN', 'MAGE']
  const names = ['Текван', 'Мира', 'Элвен', 'Рейнар', 'Селеста']
  return {
    actorId: index === 0 ? 'local' : `ally-${index}`,
    kind: 'Player',
    definitionId: classes[index]!,
    name: names[index]!,
    hp: 920 - index * 85,
    maxHp: 960,
    resourceType: index === 1 || index === 4 ? 'MANA' : index === 2 ? 'FOCUS' : 'RAGE',
    resource: 60,
    maxResource: 141,
    autoAttackEnabled: true,
    cooldowns: { SHIELD_SLAM: new Date(Date.now() + 3_000).toISOString() },
    knownAbilityIds: index === 0 ? abilities.map((ability) => ability.id) : [],
    abilities: index === 0 ? abilities : [],
    effects: [],
    level: 30,
    genderId: index === 0 || index === 1 || index === 4 ? 'FEMALE' : 'MALE',
  }
}

const players = Array.from({ length: partySize.value }, (_, index) => player(index))
const enemy: CombatActorSnapshot = {
  actorId: 'enemy',
  kind: 'Monster',
  definitionId: 'ECLIPSED_PRIEST',
  name: 'Жрец отражений',
  hp: 2670,
  maxHp: 3550,
  resourceType: 'MANA',
  resource: 80,
  maxResource: 100,
  autoAttackEnabled: false,
  cooldowns: {},
  knownAbilityIds: [],
  abilities: [],
  effects: [],
  level: 29,
  artId: 'zhrets-ugasshego-sveta',
  monsterRank: 'Elite',
  currentAggroTargetActorId: players[Math.min(1, players.length - 1)]!.actorId,
}

combat.snapshot = {
  sessionId: 'battle-preview',
  sequence: 12,
  status: 'Active',
  serverTimeUtc: new Date().toISOString(),
  contentVersion: 'preview',
  balanceVersion: 'preview',
  player: players[0]!,
  players,
  enemy,
  enemies: [enemy],
  selectedTargetActorId: enemy.actorId,
}
combat.selectedFriendlyTargetActorId = players[Math.min(2, players.length - 1)]!.actorId
combat.events = [
  {
    sequence: 10,
    type: 'DamageDealt',
    actorId: enemy.actorId,
    sourceActorId: 'local',
    targetActorId: enemy.actorId,
    definitionId: 'HEROIC_STRIKE',
    amount: 176,
    amountBeforeShields: 176,
    serverTimeUtc: new Date().toISOString(),
  },
  {
    sequence: 11,
    type: 'DamageDealt',
    actorId: players[0]!.actorId,
    sourceActorId: enemy.actorId,
    targetActorId: players[0]!.actorId,
    definitionId: 'AUTO_ATTACK',
    amount: 92,
    amountBeforeShields: 92,
    serverTimeUtc: new Date().toISOString(),
  },
]

function transferAggro(): void {
  if (!combat.snapshot) return
  const target = players[players.length - 1]!
  const nextEnemy = { ...combat.snapshot.enemy, currentAggroTargetActorId: target.actorId }
  combat.snapshot = {
    ...combat.snapshot,
    enemy: nextEnemy,
    enemies: (combat.snapshot.enemies ?? [combat.snapshot.enemy]).map((candidate) =>
      candidate.actorId === nextEnemy.actorId ? nextEnemy : candidate,
    ),
  }
}

onUnmounted(() => {
  if (combat.snapshot?.sessionId === 'battle-preview') combat.snapshot = null
})
</script>

<template>
  <main
    class="battle-preview"
    :data-preview-aggro-target="combat.snapshot?.enemy.currentAggroTargetActorId"
  >
    <button class="battle-preview__test-control" data-preview-transfer-aggro @click="transferAggro">
      Transfer aggro
    </button>
    <BattleScreen />
  </main>
</template>

<style scoped>
.battle-preview {
  min-height: 100svh;
  background: #05070c;
}
.battle-preview__test-control {
  position: fixed;
  z-index: 999;
  top: 0;
  left: 0;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip-path: inset(50%);
  opacity: 0;
}
</style>
