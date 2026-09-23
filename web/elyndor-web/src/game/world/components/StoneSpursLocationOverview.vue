<script setup lang="ts">
import { resolveCharacterArt } from '@/assets/characterArt'
import { monsterArtUrl } from '@/assets/monsterArt'
import { STONE_SPURS_OVERVIEW, type StoneSpursNearbyPlayer, type StoneSpursResident } from '@/game/world/stoneSpursOverview'
import { UIButton } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

withDefaults(defineProps<{
  backgroundUrl?: string
  canExplore: boolean
  canAutoHunt: boolean
  actionsDisabled?: boolean
  exploreLoading?: boolean
}>(), {
  backgroundUrl: '',
  actionsDisabled: false,
  exploreLoading: false,
})

const emit = defineEmits<{
  explore: []
  'auto-hunt': []
}>()

const overview = STONE_SPURS_OVERVIEW

function residentArt(resident: StoneSpursResident): string | undefined {
  if (resident.hidden || !resident.artId) return undefined
  return monsterArtUrl(resident.artId, resident.monsterId)
}

function playerArt(player: StoneSpursNearbyPlayer): string | null {
  return resolveCharacterArt(player.classId, player.genderId, 'transparent')
}

function rewardLabel(reward: 'xp' | 'gold' | 'item' | 'magic'): string {
  if (reward === 'xp') return 'XP'
  if (reward === 'gold') return 'Золото'
  if (reward === 'magic') return 'Магический предмет'
  return 'Предмет'
}

function rewardGlyph(reward: 'xp' | 'gold' | 'item' | 'magic'): 'star' | 'ring' | 'chest' | 'scroll' {
  if (reward === 'xp') return 'star'
  if (reward === 'gold') return 'ring'
  if (reward === 'magic') return 'scroll'
  return 'chest'
}
</script>

<template>
  <section class="stone-spurs-overview" aria-labelledby="stone-spurs-title">
    <div class="location-breadcrumb" aria-label="Текущая локация">
      <span aria-hidden="true">◆</span>
      <strong>{{ overview.title }}</strong>
    </div>

    <section
      class="location-hero"
      :style="backgroundUrl ? { backgroundImage: `url(${backgroundUrl})` } : undefined"
      aria-label="Каменные отроги"
    >
      <div class="location-hero__shade" />
      <div class="location-hero__content">
        <div class="location-hero__badges">
          <span class="location-hero__risk">{{ overview.riskLabel }}</span>
          <span>{{ overview.levelLabel }}</span>
        </div>

        <div class="location-hero__copy">
          <h1 id="stone-spurs-title">{{ overview.title }}</h1>
          <p>{{ overview.description }}</p>
        </div>

        <div class="location-hero__meta" aria-label="Состояние локации">
          <span><b>{{ overview.playersCount }}</b> игроков</span>
          <span>Обитатели: <b>{{ overview.residents.length }}</b></span>
        </div>
      </div>
    </section>

    <div class="location-actions" aria-label="Основные действия локации">
      <UIButton
        class="location-actions__explore"
        data-explore
        :loading="exploreLoading"
        :disabled="!canExplore || actionsDisabled"
        @click="emit('explore')"
      >
        Исследовать
      </UIButton>
      <UIButton
        class="location-actions__afk"
        data-afk-farming
        variant="secondary"
        :disabled="!canAutoHunt || actionsDisabled"
        @click="emit('auto-hunt')"
      >
        Автоматическая охота
      </UIButton>
    </div>

    <div class="location-overview-grid">
      <section class="location-panel residents-panel" aria-labelledby="residents-title">
        <header class="location-panel__heading">
          <h2 id="residents-title">Обитатели</h2>
          <span>{{ overview.residentsSummary }}</span>
        </header>

        <div class="resident-strip">
          <article
            v-for="resident in overview.residents"
            :key="resident.id"
            class="resident-card"
            :class="[`resident-card--${resident.kind}`, { 'resident-card--hidden': resident.hidden }]"
          >
            <div class="resident-card__portrait">
              <img
                v-if="residentArt(resident)"
                :src="residentArt(resident)"
                :alt="resident.name"
                loading="lazy"
              />
              <div v-else class="resident-card__silhouette" aria-hidden="true">
                <IconGenerator :config="{ id: `resident-${resident.id}`, glyph: 'skull', category: 'utility', rarity: resident.kind === 'rare' ? 'rare' : 'common' }" />
              </div>
              <span v-if="resident.kind === 'boss'" class="resident-card__boss">БОСС</span>
            </div>
            <strong>{{ resident.hidden ? '???' : resident.name }}</strong>
            <small>{{ resident.level }}</small>
          </article>
        </div>
      </section>

      <section class="location-panel loot-panel" aria-labelledby="loot-title">
        <header class="location-panel__heading location-panel__heading--stacked">
          <h2 id="loot-title">Возможная добыча</h2>
          <div class="loot-stats" aria-label="Статистика добычи">
            <span v-for="stat in overview.lootStats" :key="stat.label">
              {{ stat.label }} <b>{{ stat.value }}</b>
            </span>
          </div>
        </header>

        <div class="loot-grid">
          <article v-for="loot in overview.loot" :key="loot.id" class="loot-item" :title="loot.name">
            <div class="loot-item__icon" :class="{ 'loot-item__icon--unknown': loot.unknown }">
              <IconGenerator
                :config="{
                  id: `stone-spurs-${loot.id}`,
                  glyph: loot.glyph,
                  category: loot.glyph === 'ore' ? 'resource' : 'utility',
                  rarity: loot.rarity,
                }"
                :label="loot.unknown ? undefined : loot.name"
              />
              <span v-if="loot.unknown" aria-hidden="true">?</span>
            </div>
            <small>{{ loot.unknown ? 'Неизвестно' : loot.name }}</small>
          </article>
        </div>
      </section>

      <section class="location-panel contracts-panel" aria-labelledby="overview-contracts-title">
        <header class="location-panel__heading">
          <h2 id="overview-contracts-title">Контракты</h2>
          <span><b>{{ overview.contracts.length }}</b> активных</span>
        </header>

        <div class="contract-list">
          <article v-for="contract in overview.contracts" :key="contract.id" class="overview-contract">
            <div class="overview-contract__topline">
              <div>
                <strong>{{ contract.title }}</strong>
                <p>{{ contract.objective }}</p>
              </div>
              <b>{{ contract.current }} / {{ contract.target }}</b>
            </div>
            <div class="overview-contract__progress" role="progressbar" :aria-label="contract.title" :aria-valuenow="contract.current" :aria-valuemax="contract.target">
              <span :style="{ width: `${Math.min(100, contract.current / contract.target * 100)}%` }" />
            </div>
            <div class="overview-contract__rewards" aria-label="Награды">
              <span v-for="reward in contract.rewards" :key="reward" :title="rewardLabel(reward)">
                <i aria-hidden="true">
                  <IconGenerator :config="{ id: `${contract.id}-${reward}`, glyph: rewardGlyph(reward), category: 'utility', rarity: reward === 'magic' ? 'epic' : 'common' }" />
                </i>
                {{ rewardLabel(reward) }}
              </span>
            </div>
          </article>
        </div>
      </section>

      <section class="location-panel nearby-panel" aria-labelledby="nearby-title">
        <header class="location-panel__heading">
          <h2 id="nearby-title">Игроки поблизости</h2>
          <span><b>{{ overview.playersCount }}</b> игроков</span>
        </header>

        <div class="nearby-list">
          <article v-for="player in overview.nearbyPlayers" :key="player.name" class="nearby-player">
            <div class="nearby-player__avatar">
              <img v-if="playerArt(player)" :src="playerArt(player) ?? ''" :alt="player.name" loading="lazy" />
              <span v-else>{{ player.name.slice(0, 1) }}</span>
            </div>
            <div>
              <strong>{{ player.name }}</strong>
              <small>ур. {{ player.level }}</small>
            </div>
          </article>
          <div class="nearby-more" aria-label="Ещё три игрока">+3</div>
        </div>
      </section>
    </div>
  </section>
</template>

<style>
@media (min-width: 720px) {
  .game-shell:has(.stone-spurs-overview) {
    width: min(calc(100% - 24px), var(--ui-content-width-tablet));
  }

  .game-shell:has(.stone-spurs-overview) .world {
    width: 100%;
    max-width: var(--ui-content-width-tablet);
    padding-inline: clamp(16px, 2.4vw, 28px);
  }

  .game-shell:has(.stone-spurs-overview) .navigation {
    width: min(100%, 720px);
    justify-self: center;
    border-inline: 1px solid rgb(205 177 113 / 12%);
  }
}

@media (min-width: 960px) {
  .game-shell:has(.stone-spurs-overview) .hud {
    grid-template-columns: minmax(0, 1fr) minmax(360px, .9fr);
    grid-template-rows: auto auto;
    align-items: center;
    column-gap: 24px;
    padding-inline: clamp(20px, 3vw, 36px);
  }

  .game-shell:has(.stone-spurs-overview) .hud__main {
    grid-column: 1;
    grid-row: 1 / 3;
  }

  .game-shell:has(.stone-spurs-overview) .hud__bars {
    grid-column: 2;
    grid-row: 1;
  }

  .game-shell:has(.stone-spurs-overview) .hud__context {
    grid-column: 2;
    grid-row: 2;
  }
}
</style>

<style scoped>
.stone-spurs-overview {
  display: grid;
  min-width: 0;
  gap: var(--ui-space-3);
}

.location-breadcrumb {
  display: flex;
  min-width: 0;
  align-items: center;
  gap: 7px;
  padding-inline: 2px;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.location-breadcrumb span {
  color: var(--ui-color-gold);
  font-size: .55rem;
}

.location-breadcrumb strong {
  overflow: hidden;
  color: var(--ui-color-text-secondary);
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.location-hero {
  position: relative;
  min-height: 17rem;
  overflow: hidden;
  border: 1px solid color-mix(in srgb, var(--ui-color-gold) 28%, var(--ui-color-border-strong));
  border-radius: calc(var(--ui-radius-lg) + 2px);
  background-color: var(--ui-color-surface-1);
  background-position: center;
  background-size: cover;
  box-shadow: var(--ui-shadow-inset), 0 18px 42px rgb(0 0 0 / 30%);
}

.location-hero::after {
  position: absolute;
  inset: 0;
  border: 1px solid rgb(255 255 255 / 3%);
  border-radius: inherit;
  content: '';
  pointer-events: none;
}

.location-hero__shade {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(180deg, rgb(3 5 10 / 10%) 0%, rgb(3 5 10 / 20%) 34%, rgb(4 7 13 / 93%) 86%, rgb(4 7 13 / 98%) 100%),
    linear-gradient(90deg, rgb(3 5 10 / 46%), transparent 62%);
}

.location-hero__content {
  position: relative;
  z-index: 1;
  display: grid;
  min-height: 17rem;
  align-content: end;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4);
}

.location-hero__badges,
.location-hero__meta,
.loot-stats,
.overview-contract__rewards,
.nearby-list {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
}

.location-hero__badges {
  gap: 6px;
}

.location-hero__badges span {
  padding: 4px 8px;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-round);
  background: rgb(5 8 14 / 66%);
  color: var(--ui-color-text-secondary);
  font-size: .58rem;
  font-weight: 800;
  letter-spacing: .07em;
  text-transform: uppercase;
  backdrop-filter: blur(8px);
}

.location-hero__badges .location-hero__risk {
  border-color: rgb(216 95 114 / 46%);
  color: #f19aaa;
  box-shadow: 0 0 14px rgb(216 95 114 / 9%);
}

.location-hero__copy {
  display: grid;
  gap: 6px;
  max-width: 42rem;
  text-shadow: 0 2px 10px rgb(0 0 0 / 72%);
}

.location-hero__copy h1,
.location-hero__copy p,
.location-panel__heading h2,
.overview-contract p {
  margin: 0;
}

.location-hero__copy h1 {
  font-family: var(--ui-font-display);
  font-size: clamp(1.72rem, 7.5vw, 2.65rem);
  line-height: 1.02;
  letter-spacing: -.025em;
}

.location-hero__copy p {
  max-width: 34rem;
  color: #d1d5df;
  font-size: var(--ui-font-size-sm);
  line-height: 1.45;
}

.location-hero__meta {
  gap: 7px 14px;
  color: #abb3c4;
  font-size: var(--ui-font-size-xs);
}

.location-hero__meta span {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.location-hero__meta b {
  color: #f0d28e;
  font-variant-numeric: tabular-nums;
}

.location-actions {
  display: grid;
  grid-template-columns: 1fr;
  gap: 8px;
}

.location-actions :deep(.ui-button) {
  min-height: 48px;
  font-weight: 750;
}

.location-actions__explore :deep(.ui-button),
.location-actions :deep(.ui-button:first-child) {
  box-shadow: 0 0 18px rgb(146 136 255 / 10%);
}

.location-overview-grid {
  display: grid;
  min-width: 0;
  gap: var(--ui-space-3);
}

.location-panel {
  min-width: 0;
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background:
    radial-gradient(circle at 12% 0, rgb(146 136 255 / 6%), transparent 11rem),
    linear-gradient(160deg, rgb(16 22 33 / 96%), rgb(7 10 17 / 98%));
  box-shadow: var(--ui-shadow-inset), 0 10px 26px rgb(0 0 0 / 13%);
}

.location-panel__heading {
  display: flex;
  min-width: 0;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 12px 13px 10px;
  border-bottom: 1px solid rgb(255 255 255 / 5%);
}

.location-panel__heading--stacked {
  align-items: flex-start;
}

.location-panel__heading h2 {
  flex: 0 0 auto;
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-md);
  line-height: 1.2;
}

.location-panel__heading > span {
  overflow: hidden;
  color: var(--ui-color-text-muted);
  font-size: .58rem;
  text-align: right;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.location-panel__heading > span b,
.loot-stats b {
  color: #d8d2ff;
  font-variant-numeric: tabular-nums;
}

.resident-strip {
  display: grid;
  grid-auto-columns: 5.2rem;
  grid-auto-flow: column;
  gap: 8px;
  overflow-x: auto;
  padding: 12px 13px 14px;
  scrollbar-width: none;
  scroll-snap-type: x proximity;
  overscroll-behavior-inline: contain;
}

.resident-strip::-webkit-scrollbar {
  display: none;
}

.resident-card {
  display: grid;
  min-width: 0;
  gap: 4px;
  scroll-snap-align: start;
}

.resident-card__portrait {
  position: relative;
  display: grid;
  aspect-ratio: 1;
  overflow: hidden;
  place-items: center;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-md);
  background: rgb(5 8 14 / 92%);
}

.resident-card__portrait img {
  width: 100%;
  height: 100%;
  object-fit: contain;
  object-position: center bottom;
  filter: drop-shadow(0 8px 8px rgb(0 0 0 / 44%));
}

.resident-card--rare .resident-card__portrait {
  border-color: rgb(110 132 224 / 52%);
  box-shadow: inset 0 0 18px rgb(91 104 205 / 9%);
}

.resident-card--boss .resident-card__portrait {
  border-color: rgb(216 95 114 / 55%);
  box-shadow: inset 0 0 22px rgb(216 95 114 / 10%), 0 0 13px rgb(216 95 114 / 7%);
}

.resident-card__silhouette {
  width: 72%;
  height: 72%;
  filter: grayscale(1) brightness(.38);
  opacity: .72;
}

.resident-card--hidden .resident-card__portrait::after {
  position: absolute;
  inset: 0;
  background: radial-gradient(circle, transparent, rgb(1 3 7 / 55%));
  content: '';
}

.resident-card__boss {
  position: absolute;
  right: 4px;
  bottom: 4px;
  padding: 2px 5px;
  border: 1px solid rgb(216 95 114 / 55%);
  border-radius: var(--ui-radius-round);
  background: rgb(26 5 10 / 82%);
  color: #f2a0ae;
  font-size: .46rem;
  font-weight: 900;
  letter-spacing: .08em;
}

.resident-card > strong,
.resident-card > small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.resident-card > strong {
  color: var(--ui-color-text-secondary);
  font-size: .66rem;
  font-weight: 700;
}

.resident-card > small {
  color: var(--ui-color-text-muted);
  font-size: .58rem;
}

.loot-stats {
  justify-content: flex-end;
  gap: 4px 8px;
}

.loot-stats span {
  color: var(--ui-color-text-muted);
  font-size: .56rem;
  white-space: nowrap;
}

.loot-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 9px;
  padding: 12px 13px 14px;
}

.loot-item {
  display: grid;
  min-width: 0;
  justify-items: center;
  gap: 5px;
}

.loot-item__icon {
  position: relative;
  width: min(100%, 3.35rem);
  aspect-ratio: 1;
}

.loot-item__icon--unknown > span {
  position: absolute;
  z-index: 2;
  inset: 0;
  display: grid;
  place-items: center;
  color: #d8d2ff;
  font: 800 1rem var(--ui-font-display);
  text-shadow: 0 1px 5px black;
}

.loot-item__icon--unknown :deep(.icon-generator__glyph) {
  opacity: .2;
}

.loot-item small {
  width: 100%;
  overflow: hidden;
  color: var(--ui-color-text-muted);
  font-size: .56rem;
  text-align: center;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.contract-list {
  display: grid;
  gap: 1px;
  background: rgb(255 255 255 / 5%);
}

.overview-contract {
  display: grid;
  gap: 9px;
  padding: 12px 13px;
  background:
    linear-gradient(90deg, rgb(146 136 255 / 4%), transparent 68%),
    rgb(7 10 17 / 96%);
}

.overview-contract__topline {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: start;
  gap: 10px;
}

.overview-contract__topline > div {
  display: grid;
  min-width: 0;
  gap: 3px;
}

.overview-contract__topline strong {
  font-family: var(--ui-font-display);
  font-size: .78rem;
  line-height: 1.2;
}

.overview-contract__topline p {
  color: var(--ui-color-text-muted);
  font-size: .62rem;
  line-height: 1.35;
}

.overview-contract__topline > b {
  color: #d8d2ff;
  font-size: .68rem;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.overview-contract__progress {
  height: 4px;
  overflow: hidden;
  border-radius: var(--ui-radius-round);
  background: rgb(255 255 255 / 6%);
}

.overview-contract__progress > span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, #6257bd, #a08bea);
  box-shadow: 0 0 8px rgb(146 136 255 / 32%);
}

.overview-contract__rewards {
  gap: 6px 10px;
}

.overview-contract__rewards > span {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  color: var(--ui-color-text-muted);
  font-size: .56rem;
  white-space: nowrap;
}

.overview-contract__rewards i {
  display: block;
  width: 1.2rem;
  height: 1.2rem;
  font-style: normal;
}

.nearby-list {
  gap: 11px 14px;
  padding: 13px;
}

.nearby-player {
  display: flex;
  min-width: 0;
  align-items: center;
  gap: 7px;
}

.nearby-player__avatar,
.nearby-more {
  display: grid;
  width: 2.35rem;
  height: 2.35rem;
  flex: 0 0 auto;
  overflow: hidden;
  place-items: center;
  border: 1px solid color-mix(in srgb, var(--ui-color-primary) 35%, var(--ui-color-border));
  border-radius: 50%;
  background: rgb(8 11 19 / 96%);
}

.nearby-player__avatar img {
  width: 126%;
  height: 126%;
  object-fit: cover;
  object-position: 50% 18%;
  transform: translateY(8%);
}

.nearby-player > div:last-child {
  display: grid;
  min-width: 0;
  gap: 1px;
}

.nearby-player strong {
  max-width: 5rem;
  overflow: hidden;
  color: var(--ui-color-text-secondary);
  font-size: .66rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.nearby-player small {
  color: var(--ui-color-text-muted);
  font-size: .56rem;
}

.nearby-more {
  border-style: dashed;
  color: #d8d2ff;
  font-size: .68rem;
  font-weight: 800;
}

@media (min-width: 390px) {
  .location-actions {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (min-width: 720px) {
  .stone-spurs-overview {
    gap: var(--ui-space-4);
  }

  .location-hero,
  .location-hero__content {
    min-height: 20rem;
  }

  .location-hero__content {
    padding: clamp(20px, 3vw, 30px);
  }

  .location-hero__copy h1 {
    font-size: clamp(2.25rem, 4.8vw, 3.25rem);
  }

  .location-actions {
    grid-template-columns: repeat(2, minmax(0, 15rem));
    justify-content: start;
  }

  .location-overview-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    align-items: start;
  }

  .resident-strip {
    grid-template-columns: repeat(5, minmax(0, 1fr));
    grid-auto-columns: auto;
    grid-auto-flow: row;
    overflow-x: visible;
  }

  .resident-card__portrait {
    min-height: 5rem;
  }

  .loot-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .nearby-list {
    gap: 14px 18px;
  }
}

@media (min-width: 1100px) {
  .location-hero,
  .location-hero__content {
    min-height: 22rem;
  }

  .location-panel__heading {
    padding: 14px 15px 11px;
  }

  .resident-strip,
  .loot-grid,
  .nearby-list {
    padding: 15px;
  }

  .resident-card__portrait {
    min-height: 6.2rem;
  }
}

@media (max-width: 359px) {
  .location-panel__heading {
    align-items: flex-start;
    flex-direction: column;
  }

  .location-panel__heading > span {
    text-align: left;
  }

  .loot-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
