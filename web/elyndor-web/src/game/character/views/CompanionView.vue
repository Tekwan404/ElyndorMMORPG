<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import type { CharacterCompanionSnapshot, CompanionProfile } from '@/api/contracts'
import { companionArtUrl } from '@/assets/companionArt'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIPanel } from '@/ui/components'

const session = useGameSessionStore()
const snapshot = ref<CharacterCompanionSnapshot | null>(null)
const loading = ref(true)

const activeProfile = computed<CompanionProfile | null>(() =>
  snapshot.value?.availableProfiles.find(profile => profile.id === snapshot.value?.effectiveProfileId) ?? null,
)
const isSpiritOverride = computed(() =>
  snapshot.value !== null && snapshot.value.effectiveProfileId !== snapshot.value.selectedPhysicalProfileId,
)

onMounted(async () => {
  snapshot.value = await session.getCompanion()
  loading.value = false
})

async function select(profileId: string): Promise<void> {
  if (session.mutationPending || isSpiritOverride.value) return
  const updated = await session.selectCompanion(profileId)
  if (updated) snapshot.value = updated
}

function roleLabel(archetype: string): string {
  if (archetype === 'PREDATOR') return 'Хищник · урон и кровотечение'
  if (archetype === 'GUARDIAN') return 'Страж · защита хозяина'
  if (archetype === 'TRAPPER') return 'Ловчий · контроль и ослабление'
  return 'Магический спутник'
}

function errorMessage(): string | null {
  if (session.errorCode === 'companion_change_in_combat') return 'Сменить спутника можно после завершения боя.'
  if (session.errorCode === 'companion_invalid_profile') return 'Этот спутник недоступен персонажу.'
  return session.errorCode ? 'Не удалось сменить спутника. Повторите попытку.' : null
}
</script>

<template>
  <section class="companion-view">
    <UIPanel v-if="loading" class="companion-state">Загрузка спутника…</UIPanel>
    <UIPanel v-else-if="!snapshot" class="companion-state">Спутник сейчас недоступен.</UIPanel>
    <template v-else>
      <section class="companion-hero" data-companion-active>
        <div class="companion-hero__art">
          <img v-if="activeProfile && companionArtUrl(activeProfile.id)" :src="companionArtUrl(activeProfile.id)" :alt="activeProfile.name" />
          <span v-else aria-hidden="true">✦</span>
        </div>
        <div>
          <p class="eyebrow">Активный спутник</p>
          <h1>{{ activeProfile?.name ?? 'Эфирный дух' }}</h1>
          <p>{{ activeProfile ? roleLabel(activeProfile.archetype) : 'Дух арканического стрелка' }}</p>
          <small v-if="isSpiritOverride">Магический спутник активен из-за текущего арканического билда.</small>
          <small v-else>Спутник участвует в бою вместе с лучником.</small>
        </div>
      </section>

      <UIPanel class="companion-list">
        <template #title>Ваши спутники</template>
        <p>Выберите одного физического спутника. Смена доступна вне боя.</p>
        <div class="companion-list__grid">
          <article
            v-for="profile in snapshot.availableProfiles"
            :key="profile.id"
            class="companion-card"
            :class="{ active: snapshot.selectedPhysicalProfileId === profile.id }"
          >
            <img v-if="companionArtUrl(profile.id)" :src="companionArtUrl(profile.id)" :alt="profile.name" />
            <div>
              <strong>{{ profile.name }}</strong>
              <small>{{ roleLabel(profile.archetype) }}</small>
            </div>
            <UIButton
              :data-companion-profile="profile.id"
              :disabled="session.mutationPending || isSpiritOverride || snapshot.selectedPhysicalProfileId === profile.id"
              @click="select(profile.id)"
            >
              {{ snapshot.selectedPhysicalProfileId === profile.id ? 'Выбран' : 'Выбрать' }}
            </UIButton>
          </article>
        </div>
        <p v-if="errorMessage()" class="companion-error">{{ errorMessage() }}</p>
      </UIPanel>
    </template>
  </section>
</template>

<style scoped>
.companion-view { display: grid; width: min(100%, var(--ui-content-width)); gap: var(--ui-space-3); margin-inline: auto; padding: var(--ui-space-3) var(--ui-space-3) var(--ui-space-7); }
.companion-state { min-height: 9rem; display: grid; place-items: center; color: var(--ui-color-text-muted); }
.companion-hero { display: grid; grid-template-columns: minmax(8rem, .8fr) minmax(0, 1.2fr); align-items: center; min-height: 13rem; overflow: hidden; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-lg); background: radial-gradient(circle at 28% 35%, rgb(146 136 255 / 20%), transparent 42%), linear-gradient(145deg, rgb(15 21 35), rgb(5 9 15)); box-shadow: var(--ui-shadow-elevated); }
.companion-hero__art { height: 100%; min-height: 13rem; display: grid; place-items: end center; background: radial-gradient(ellipse at bottom, rgb(0 0 0 / 64%), transparent 66%); }
.companion-hero__art img { width: min(100%, 12rem); max-height: 12rem; object-fit: contain; filter: drop-shadow(0 .7rem 1rem rgb(0 0 0 / 60%)); }
.companion-hero__art span { color: #c9c5ff; font-size: 5rem; }
.companion-hero > div:last-child { display: grid; gap: var(--ui-space-1); padding: var(--ui-space-4); }
.companion-hero h1, .companion-hero p { margin: 0; }.companion-hero h1 { font-family: var(--ui-font-display); font-size: clamp(1.45rem, 7vw, 2rem); }.companion-hero p { color: var(--ui-color-text-secondary); }.companion-hero small { margin-top: var(--ui-space-2); color: #bcb6ff; line-height: 1.4; }
.eyebrow { color: #bcb6ff; font-size: .65rem; font-weight: 700; letter-spacing: .1em; text-transform: uppercase; }
.companion-list > p { margin: 0 0 var(--ui-space-3); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-sm); }.companion-list__grid { display: grid; gap: var(--ui-space-2); }.companion-card { display: grid; grid-template-columns: 3.75rem minmax(0, 1fr) auto; align-items: center; gap: var(--ui-space-2); min-height: 5rem; padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(255 255 255 / 2%); }.companion-card.active { border-color: #8c85ed; box-shadow: var(--ui-shadow-inset); }.companion-card img { width: 3.75rem; height: 3.75rem; object-fit: contain; }.companion-card > div { display: grid; gap: 3px; min-width: 0; }.companion-card small { color: var(--ui-color-text-muted); font-size: .7rem; }.companion-error { margin: var(--ui-space-3) 0 0 !important; color: #ef9bab !important; }
@media (max-width: 390px) { .companion-hero { grid-template-columns: 7rem minmax(0, 1fr); }.companion-card { grid-template-columns: 3.25rem minmax(0, 1fr); }.companion-card img { width: 3.25rem; height: 3.25rem; }.companion-card :deep(button) { grid-column: 1 / -1; width: 100%; } }
</style>
