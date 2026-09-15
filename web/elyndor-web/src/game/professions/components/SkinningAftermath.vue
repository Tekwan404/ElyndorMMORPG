<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { ApiRequestError } from '@/api/apiClient'
import { type ProfessionStateSnapshot, getProfessionState, skinCorpse, type SkinnableCorpseState } from '@/game/professions/professionApi'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard } from '@/ui/components'

const session = useGameSessionStore()
const state = ref<ProfessionStateSnapshot | null>(null)
const pendingKey = ref<string | null>(null)
const error = ref<string | null>(null)

const skinning = computed(() => state.value?.learned?.find(item => item.id === 'SKINNING') ?? null)
const corpses = computed(() => state.value?.skinnableCorpses ?? [])
const visible = computed(() => skinning.value !== null && corpses.value.length > 0)

async function load(): Promise<void> {
  state.value = await getProfessionState()
}

async function skin(corpse: SkinnableCorpseState): Promise<void> {
  const key = `${corpse.combatSessionId}:${corpse.enemyActorId}`
  if (pendingKey.value) return
  pendingKey.value = key
  error.value = null
  try {
    const result = await skinCorpse(corpse.combatSessionId, corpse.enemyActorId)
    state.value = result.state ?? await getProfessionState()
    await session.refreshSnapshot()
  } catch (caught) {
    error.value = errorMessage(caught)
    await load().catch(() => undefined)
  } finally {
    pendingKey.value = null
  }
}

function errorMessage(caught: unknown): string {
  const code = caught instanceof ApiRequestError ? caught.code : 'network_unavailable'
  return ({
    profession_skill_too_low: 'Недостаточно навыка снятия шкур.',
    skinning_corpse_expired: 'Время для снятия шкуры истекло.',
    skinning_corpse_already_skinned: 'Шкура с этой туши уже снята.',
    network_unavailable: 'Не удалось снять шкуру. Проверьте соединение.',
  } as Record<string, string>)[code] ?? 'Не удалось снять шкуру.'
}

onMounted(() => void load().catch(() => undefined))
</script>

<template>
  <UICard v-if="visible" class="skinning-aftermath" data-skinning-aftermath>
    <header>
      <div><small>СНЯТИЕ ШКУР</small><strong>Трофей после боя</strong></div>
      <span>Навык {{ skinning?.skill }}</span>
    </header>
    <p v-if="error" role="alert">{{ error }}</p>
    <article v-for="corpse in corpses" :key="`${corpse.combatSessionId}:${corpse.enemyActorId}`">
      <div><strong>{{ corpse.monsterName }}</strong><small>Нужно навыка: {{ corpse.requiredSkill }}</small></div>
      <UIButton
        data-skin-corpse
        :disabled="skinning!.skill < corpse.requiredSkill || pendingKey !== null"
        :loading="pendingKey === `${corpse.combatSessionId}:${corpse.enemyActorId}`"
        @click="skin(corpse)"
      >Снять шкуру</UIButton>
    </article>
  </UICard>
</template>

<style scoped>
.skinning-aftermath{display:grid;gap:0;border-color:rgb(177 130 71 / 48%)}.skinning-aftermath header,.skinning-aftermath article{display:flex;align-items:center;justify-content:space-between;gap:10px;padding:11px}.skinning-aftermath header{border-bottom:1px solid var(--ui-color-border)}.skinning-aftermath header div,.skinning-aftermath article div{display:grid;gap:3px}.skinning-aftermath small{color:var(--ui-color-gold);font-size:.58rem;font-weight:800;letter-spacing:.08em}.skinning-aftermath article{border-bottom:1px solid rgb(255 255 255 / 6%)}.skinning-aftermath article:last-child{border-bottom:0}.skinning-aftermath article small{color:var(--ui-color-text-muted);font-weight:400;letter-spacing:0}.skinning-aftermath header>span{color:var(--ui-color-text-muted);font-size:.65rem}.skinning-aftermath>p{margin:0;padding:9px 11px;color:#efaaa4;font-size:.65rem}@media(max-width:520px){.skinning-aftermath article{align-items:stretch;flex-direction:column}.skinning-aftermath article :deep(.ui-button){width:100%}}
</style>
