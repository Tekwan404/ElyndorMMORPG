<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { resolveCharacterArt } from '@/assets/characterArt'
import { classLabel } from '@/game/character/characterPresentation'
import FriendsView from '@/game/social/views/FriendsView.vue'
import PartyView from '@/game/party/views/PartyView.vue'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton } from '@/ui/components'

export type MenuSection = 'profile' | 'friends' | 'party'

const props = defineProps<{ initialSection: MenuSection }>()
const session = useGameSessionStore()
const activeSection = ref<MenuSection>(props.initialSection)
const copied = ref(false)
const character = computed(() => session.snapshot?.character ?? null)
const portraitArt = computed(() => character.value
  ? resolveCharacterArt(character.value.classId, character.value.genderId, 'transparent')
  : null)

watch(() => props.initialSection, section => {
  activeSection.value = section
})

async function copyPublicCode(): Promise<void> {
  const code = character.value?.publicCode
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
        <small>СИСТЕМЫ И СВЯЗИ</small>
        <h1>Меню</h1>
      </div>
      <span>ELYNDOR</span>
    </header>

    <section v-if="character" class="menu-profile" aria-label="Профиль героя">
      <div class="menu-profile__portrait">
        <img v-if="portraitArt" :src="portraitArt" alt="" aria-hidden="true" />
        <span v-else>{{ character.name.slice(0, 1).toUpperCase() }}</span>
      </div>
      <div class="menu-profile__identity">
        <small>ГЕРОЙ · УР. {{ character.level }}</small>
        <strong>{{ character.name }}</strong>
        <span>{{ classLabel(character.classId) }} · {{ character.gold }} золота</span>
      </div>
      <div class="menu-profile__code">
        <small>ELY ID</small>
        <code>{{ character.publicCode ?? '—' }}</code>
        <UIButton
          data-copy-ely-id
          variant="ghost"
          :disabled="!character.publicCode"
          @click="copyPublicCode"
        >{{ copied ? 'Скопировано' : 'Копировать' }}</UIButton>
      </div>
    </section>

    <nav v-if="activeSection === 'profile'" class="menu-grid" aria-label="Игровые системы">
      <button class="menu-tile menu-tile--gold" type="button" @click="activeSection = 'friends'">
        <span class="menu-tile__icon" aria-hidden="true">♧</span>
        <span><strong>Друзья</strong><small>Поиск и заявки</small></span>
        <b aria-hidden="true">›</b>
      </button>
      <button class="menu-tile menu-tile--violet" type="button" @click="activeSection = 'party'">
        <span class="menu-tile__icon" aria-hidden="true">⚔</span>
        <span><strong>Группа</strong><small>Состав и поход</small></span>
        <b aria-hidden="true">›</b>
      </button>
    </nav>

    <section v-if="activeSection === 'profile'" class="menu-note">
      <span class="menu-note__mark" aria-hidden="true">i</span>
      <p>ELY ID нужен, чтобы друзья могли найти тебя и пригласить в группу. Гильдия и контракты находятся в городском представительстве.</p>
    </section>

    <section v-else class="menu-subsection">
      <button class="menu-back" type="button" @click="activeSection = 'profile'">‹ Все системы</button>
      <FriendsView v-if="activeSection === 'friends'" />
      <PartyView v-else />
    </section>
  </section>
</template>

<style scoped>
.menu-view {
  display: grid;
  width: min(100%, var(--ui-content-width));
  min-height: 100%;
  margin-inline: auto;
  align-content: start;
  gap: 12px;
  padding: 14px 14px calc(var(--ui-space-7) + var(--ui-safe-area-bottom));
}

.menu-view__header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 12px;
  padding: 2px 2px 7px;
  border-bottom: 1px solid rgb(205 177 113 / 28%);
}

.menu-view__header small {
  color: var(--ui-color-gold);
  font-size: .55rem;
  font-weight: 800;
  letter-spacing: .14em;
}

.menu-view__header h1 {
  margin: 2px 0 0;
  font-family: var(--ui-font-display);
  font-size: 1.55rem;
}

.menu-view__header > span {
  color: var(--ui-color-text-muted);
  font-size: .65rem;
  letter-spacing: .12em;
}

.menu-profile {
  display: grid;
  grid-template-columns: 4.1rem minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  padding: 10px;
  border: 1px solid rgb(205 177 113 / 32%);
  background:
    linear-gradient(90deg, rgb(205 177 113 / 10%), transparent 55%),
    var(--ui-gradient-panel);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 4%);
}

.menu-profile__portrait {
  display: grid;
  width: 4.1rem;
  height: 4.1rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid var(--ui-color-gold);
  border-radius: var(--ui-radius-md);
  background: #080b11;
  color: var(--ui-color-gold);
  font: 700 1.6rem var(--ui-font-display);
}

.menu-profile__portrait img {
  width: 132%;
  height: 132%;
  object-fit: cover;
  object-position: 50% 18%;
  transform: translateY(8%);
}

.menu-profile__identity,
.menu-profile__code {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.menu-profile__identity small,
.menu-profile__code small {
  color: var(--ui-color-gold);
  font-size: .5rem;
  font-weight: 800;
  letter-spacing: .08em;
}

.menu-profile__identity strong {
  overflow: hidden;
  font-family: var(--ui-font-display);
  font-size: 1rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.menu-profile__identity > span {
  color: var(--ui-color-text-muted);
  font-size: .61rem;
}

.menu-profile__code {
  justify-items: end;
  text-align: right;
}

.menu-profile__code code {
  color: #e8cb8a;
  font-size: .62rem;
  letter-spacing: .06em;
}

.menu-profile__code :deep(.ui-button) {
  min-height: 28px;
  padding: 3px 6px;
  font-size: .56rem;
}

.menu-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 6px;
}

.menu-tile {
  display: grid;
  grid-template-columns: 2.4rem minmax(0, 1fr) auto;
  align-items: center;
  gap: 8px;
  min-width: 0;
  min-height: 4.2rem;
  padding: 8px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: linear-gradient(150deg, rgb(28 34 41 / 96%), rgb(8 12 18 / 98%));
  color: var(--ui-color-text-primary);
  font: inherit;
  text-align: left;
  transition: border-color var(--ui-transition-fast), transform var(--ui-transition-fast), background var(--ui-transition-fast);
}

.menu-tile:not(:disabled):active {
  transform: translateY(1px);
}

.menu-tile:not(:disabled):hover {
  border-color: var(--ui-color-gold-muted);
  background: linear-gradient(150deg, rgb(47 42 35 / 96%), rgb(10 13 18 / 98%));
}

.menu-tile:disabled {
  opacity: .48;
}

.menu-tile--gold {
  border-color: rgb(205 177 113 / 38%);
}

.menu-tile--violet {
  border-color: rgb(182 161 236 / 34%);
}

.menu-tile__icon {
  display: grid;
  width: 2.4rem;
  height: 2.4rem;
  place-items: center;
  border: 1px solid rgb(205 177 113 / 32%);
  border-radius: var(--ui-radius-sm);
  background: rgb(5 8 13 / 88%);
  color: var(--ui-color-gold);
  font-size: 1.15rem;
}

.menu-tile--violet .menu-tile__icon {
  border-color: rgb(182 161 236 / 34%);
  color: var(--ui-color-primary);
}

.menu-tile > span:nth-child(2) {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.menu-tile strong {
  overflow: hidden;
  font-family: var(--ui-font-display);
  font-size: .78rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.menu-tile small {
  color: var(--ui-color-text-muted);
  font-size: .56rem;
}

.menu-tile > b {
  color: var(--ui-color-gold-muted);
  font-size: 1.1rem;
  font-weight: 400;
}

.menu-note {
  display: grid;
  grid-template-columns: 1.4rem minmax(0, 1fr);
  align-items: start;
  gap: 8px;
  padding: 9px;
  border-top: 1px solid rgb(205 177 113 / 20%);
  color: var(--ui-color-text-muted);
}

.menu-note__mark {
  display: grid;
  width: 1.25rem;
  height: 1.25rem;
  place-items: center;
  border: 1px solid var(--ui-color-gold-muted);
  border-radius: 50%;
  color: var(--ui-color-gold);
  font: 700 .7rem var(--ui-font-display);
}

.menu-note p {
  margin: 0;
  font-size: .62rem;
  line-height: 1.45;
}

.menu-subsection {
  display: grid;
  gap: 8px;
}

.menu-back {
  justify-self: start;
  padding: 2px 0;
  border: 0;
  background: transparent;
  color: var(--ui-color-gold);
  font: inherit;
  font-size: .68rem;
}

@media (max-width: 380px) {
  .menu-profile {
    grid-template-columns: 3.5rem minmax(0, 1fr);
  }

  .menu-profile__portrait {
    width: 3.5rem;
    height: 3.5rem;
  }

  .menu-profile__code {
    grid-column: 1 / -1;
    grid-template-columns: auto 1fr auto;
    align-items: center;
    justify-items: start;
    gap: 7px;
    text-align: left;
  }

  .menu-grid {
    grid-template-columns: 1fr;
  }
}
</style>
