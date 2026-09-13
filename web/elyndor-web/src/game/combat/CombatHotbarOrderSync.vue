<script setup lang="ts">
import { watch } from 'vue'

import { orderCombatAbilities } from '@/game/combat/combatHotbarSettings'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'

const combat = useCombatSessionStore()
const session = useGameSessionStore()

watch(
  [
    () => session.snapshot?.character?.id ?? '',
    () => combat.snapshot?.player.abilities.map(ability => ability.id).join('|') ?? '',
  ],
  () => {
    const characterId = session.snapshot?.character?.id
    const player = combat.snapshot?.player
    if (!characterId || !player || player.abilities.length < 2) return

    const ordered = orderCombatAbilities(characterId, player.abilities)
    const currentIds = player.abilities.map(ability => ability.id).join('|')
    const orderedIds = ordered.map(ability => ability.id).join('|')
    if (currentIds !== orderedIds) player.abilities = ordered
  },
  { immediate: true },
)
</script>

<template></template>
