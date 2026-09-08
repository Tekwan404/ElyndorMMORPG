<script setup lang="ts">
import { ref, watch } from 'vue'

import FriendsView from '@/game/social/views/FriendsView.vue'
import PartyView from '@/game/party/views/PartyView.vue'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard } from '@/ui/components'

export type MenuSection = 'profile' | 'friends' | 'party'

const props = defineProps<{ initialSection: MenuSection }>()
const session = useGameSessionStore()
const activeSection = ref<MenuSection>(props.initialSection)
const copied = ref(false)

watch(() => props.initialSection, section => {
  activeSection.value = section
})

async function copyPublicCode(): Promise<void> {
  const code = session.snapshot?.character?.publicCode
  if (!code || !navigator.clipboard) return
  await navigator.clipboard.writeText(code)
  copied.value = true
  window.setTimeout(() => { copied.value = false }, 1600)
}
</script>

<template>
  <section class="menu-view">
    <header class="menu-view__header">
      <div>
        <small>СИСТЕМНОЕ МЕНЮ</small>
        <h1>Меню</h1>
      </div>
      <span>ELYNDOR</span>
    </header>

    <nav class="menu-tabs" aria-label="Разделы меню">
      <button type="button" :class="{ active: activeSection === 'profile' }" @click="activeSection = 'profile'">Профиль</button>
      <button type="button" :class="{ active: activeSection === 'friends' }" @click="activeSection = 'friends'">Друзья</button>
      <button type="button" :class="{ active: activeSection === 'party' }" @click="activeSection = 'party'">Группа</button>
    </nav>

    <UICard v-if="activeSection === 'profile' && session.snapshot?.character" class="profile-card">
      <div class="profile-card__heading">
        <small>ПУБЛИЧНЫЙ ПРОФИЛЬ</small>
        <strong>{{ session.snapshot.character.name }}</strong>
      </div>
      <div class="profile-card__code">
        <div>
          <small>ELY ID</small>
          <code>{{ session.snapshot.character.publicCode }}</code>
        </div>
        <UIButton variant="secondary" data-copy-ely-id @click="copyPublicCode">
          {{ copied ? 'Скопировано' : 'Копировать' }}
        </UIButton>
      </div>
      <p>Используй этот код, чтобы друзья могли найти тебя в Elyndor.</p>
    </UICard>

    <FriendsView v-else-if="activeSection === 'friends'" />
    <PartyView v-else-if="activeSection === 'party'" />
  </section>
</template>

<style scoped>
.menu-view { display: grid; gap: 12px; min-height: 100%; padding: 14px; }
.menu-view__header { display: flex; align-items: end; justify-content: space-between; gap: 12px; }
.menu-view__header small { color: var(--ui-color-primary); font-size: .55rem; letter-spacing: .14em; }
.menu-view__header h1 { margin: 2px 0 0; font-family: var(--ui-font-display); font-size: 1.35rem; }
.menu-view__header > span { color: var(--ui-color-text-muted); font-size: .65rem; letter-spacing: .12em; }
.menu-tabs { display: grid; grid-template-columns: repeat(3, 1fr); gap: 4px; padding: 4px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(255 255 255 / 2%); }
.menu-tabs button { min-height: 36px; border: 0; border-radius: var(--ui-radius-sm); background: transparent; color: var(--ui-color-text-muted); font: inherit; font-size: .72rem; }
.menu-tabs button.active { background: rgb(146 136 255 / 13%); color: var(--ui-color-text-primary); }
.profile-card { display: grid; gap: 14px; }
.profile-card__heading { display: grid; gap: 4px; }
.profile-card__heading small, .profile-card__code small { color: var(--ui-color-primary); font-size: .55rem; letter-spacing: .12em; }
.profile-card__heading strong { font-family: var(--ui-font-display); font-size: 1.3rem; }
.profile-card__code { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px; border: 1px solid rgb(146 136 255 / 32%); border-radius: var(--ui-radius-md); background: linear-gradient(135deg, rgb(146 136 255 / 9%), rgb(255 255 255 / 2%)); }
.profile-card__code > div { display: grid; gap: 5px; min-width: 0; }
.profile-card__code code { color: var(--ui-color-text-primary); font-size: .95rem; letter-spacing: .08em; }
.profile-card p { margin: 0; color: var(--ui-color-text-muted); font-size: .72rem; line-height: 1.5; }
</style>
