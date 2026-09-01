<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { apiClient, ApiRequestError } from '@/api/apiClient'
import type { TalentBranchId, TalentLoadoutId, TalentNode, TalentSnapshot } from '@/api/contracts'
import { resolveTalentArt } from '@/game/talents/talentArt'
import { UIButton, UIModal } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName, IconConfig } from '@/ui/icons/icon.types'

type TalentState = 'locked' | 'available' | 'learned' | 'maxed' | 'prerequisite' | 'no-points'

const tiers = Array.from({ length: 9 }, (_, index) => index + 1)
const rowHeight = 128
const treeHeight = tiers.length * rowHeight
const snapshot = ref<TalentSnapshot | null>(null)
const activeBranchId = ref<TalentBranchId>('GUARDIAN')
const activeLoadoutId = ref<TalentLoadoutId>('LOADOUT_1')
const selectedTalent = ref<TalentNode | null>(null)
const loading = ref(true)
const pending = ref(false)
const errorCode = ref<string | null>(null)
const retryMutationIds = new Map<string, string>()

const warriorTalentBranches = computed(() => snapshot.value?.branches ?? [])
const warriorTalents = computed(() => snapshot.value?.nodes ?? [])
const activeLoadout = computed(() => snapshot.value?.loadouts.find((item) => item.id === activeLoadoutId.value))
const activeRanks = computed(() => activeLoadout.value?.selectedRanks ?? {})
const availablePoints = computed(() => Math.max(0, (snapshot.value?.earnedPoints ?? 0) - (activeLoadout.value?.spentPoints ?? 0)))
const canLearnSelected = computed(() => selectedTalent.value !== null
  && ['available', 'learned'].includes(stateFor(selectedTalent.value)))
const activeBranch = computed(() =>
  warriorTalentBranches.value.find((branch) => branch.id === activeBranchId.value),
)
const branchTalents = computed(() =>
  warriorTalents.value.filter((talent) => talent.branchId === activeBranchId.value),
)
const connections = computed(() => {
  const talentIds = new Set(branchTalents.value.map((talent) => talent.id))
  return branchTalents.value.flatMap((talent) =>
    talent.prerequisites
      .filter((prerequisite) => talentIds.has(prerequisite.talentId))
      .map((prerequisite) => ({
        id: `${prerequisite.talentId}-${talent.id}`,
        from: nodePoint(prerequisite.talentId),
        to: nodePoint(talent.id),
      })),
  )
})

function talentsInTier(tier: number): readonly TalentNode[] {
  return branchTalents.value.filter((talent) => talent.tier === tier)
}

function rankFor(talentId: string): number {
  return activeRanks.value[talentId] ?? 0
}

function spentInBranch(branchId: TalentBranchId): number {
  return warriorTalents.value
    .filter((talent) => talent.branchId === branchId)
    .reduce((total, talent) => total + rankFor(talent.id), 0)
}

function stateFor(talent: TalentNode): TalentState {
  const rank = rankFor(talent.id)
  if (rank >= talent.maxRank) return 'maxed'
  if (rank > 0) return 'learned'
  if (spentInBranch(talent.branchId) < talent.requiredSpentPoints) return 'locked'
  if (talent.prerequisites.some((item) => rankFor(item.talentId) < item.requiredRank)) {
    return 'prerequisite'
  }
  return availablePoints.value > 0 ? 'available' : 'no-points'
}

function stateLabel(state: TalentState): string {
  return {
    locked: 'Закрыто',
    available: 'Доступно',
    learned: 'Изучено',
    maxed: 'Максимальный ранг',
    prerequisite: 'Нужен предыдущий талант',
    'no-points': 'Нет очков',
  }[state]
}

function glyphFor(talent: TalentNode): GlyphName {
  if (talent.maxRank === 1) return 'star'
  if (talent.branchId === 'GUARDIAN') return 'shield'
  if (talent.branchId === 'BERSERKER') return 'greatsword'
  return 'helmet'
}

function iconFor(talent: TalentNode): IconConfig {
  return {
    id: `talent-${talent.id}`,
    glyph: glyphFor(talent),
    category: 'skill',
    modifier:
      talent.branchId === 'GUARDIAN'
        ? 'ice'
        : talent.branchId === 'BERSERKER'
          ? 'fire'
          : 'holy',
    rarity: talent.tier === 9 ? 'legendary' : talent.maxRank === 1 ? 'epic' : 'rare',
  }
}

function artFor(talent: TalentNode): string | null {
  return resolveTalentArt(talent.iconId)
}

function createMutationId(): string {
  return globalThis.crypto?.randomUUID?.()
    ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`
}

function nodePoint(talentId: string): { x: number; y: number } {
  const talent = branchTalents.value.find((item) => item.id === talentId)!
  const row = talentsInTier(talent.tier)
  const index = row.findIndex((item) => item.id === talentId)
  return {
    x: ((index + 0.5) / row.length) * 400,
    y: (talent.tier - 0.5) * rowHeight,
  }
}

function connectionPath(connection: (typeof connections.value)[number]): string {
  const middleY = (connection.from.y + connection.to.y) / 2
  return `M ${connection.from.x} ${connection.from.y} C ${connection.from.x} ${middleY}, ${connection.to.x} ${middleY}, ${connection.to.x} ${connection.to.y}`
}

async function loadTalents(): Promise<void> {
  loading.value = true
  errorCode.value = null
  try {
    applySnapshot(await apiClient.request<TalentSnapshot>('/api/v1/talents/'))
  } catch (error) {
    errorCode.value = error instanceof ApiRequestError ? error.code : 'network_unavailable'
  } finally {
    loading.value = false
  }
}

function applySnapshot(next: TalentSnapshot): void {
  snapshot.value = next
  activeLoadoutId.value = next.activeLoadoutId
  if (!next.branches.some((branch) => branch.id === activeBranchId.value)) {
    activeBranchId.value = next.branches[0]?.id ?? 'GUARDIAN'
  }
}

async function mutate(path: string, mutationKey: string, body: object): Promise<void> {
  if (!snapshot.value || pending.value) return
  pending.value = true
  errorCode.value = null
  const mutationId = retryMutationIds.get(mutationKey) ?? createMutationId()
  retryMutationIds.set(mutationKey, mutationId)
  try {
    applySnapshot(await apiClient.request<TalentSnapshot>(path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...body, mutationId }),
    }))
    retryMutationIds.delete(mutationKey)
  } catch (error) {
    errorCode.value = error instanceof ApiRequestError ? error.code : 'network_unavailable'
    if (error instanceof ApiRequestError && error.code === 'talent_state_conflict') {
      retryMutationIds.delete(mutationKey)
      await loadTalents()
    }
  } finally {
    pending.value = false
  }
}

async function learnSelected(): Promise<void> {
  if (!selectedTalent.value || !snapshot.value) return
  await mutate('/api/v1/talents/learn', `learn:${activeLoadoutId.value}:${selectedTalent.value.id}`, {
    talentId: selectedTalent.value.id, loadoutId: activeLoadoutId.value,
    expectedStateVersion: snapshot.value.stateVersion,
  })
}

async function switchLoadout(loadoutId: TalentLoadoutId): Promise<void> {
  if (!snapshot.value || loadoutId === snapshot.value.activeLoadoutId) return
  await mutate('/api/v1/talents/switch', `switch:${loadoutId}`, {
    loadoutId,
    expectedStateVersion: snapshot.value.stateVersion,
  })
}

async function resetLoadout(): Promise<void> {
  if (!snapshot.value) return
  await mutate('/api/v1/talents/reset', `reset:${activeLoadoutId.value}`, {
    loadoutId: activeLoadoutId.value, expectedStateVersion: snapshot.value.stateVersion,
  })
}

onMounted(loadTalents)
</script>

<template>
  <div v-if="loading" class="talent-status" role="status">Загружаем дерево талантов…</div>
  <div v-else-if="errorCode || !activeBranch" class="talent-status talent-status--error" role="alert">
    <p>Не удалось открыть таланты: {{ errorCode ?? 'talent_unavailable' }}</p>
    <UIButton variant="ghost" @click="loadTalents">Повторить</UIButton>
  </div>
  <section v-else class="talents" data-talent-tree :data-tone="activeBranch.id.toLowerCase()">
    <header class="talents__topbar">
      <div>
        <p class="eyebrow">Warrior · Server authoritative</p>
        <h1>Таланты</h1>
      </div>
      <div class="points" aria-label="Доступные очки талантов">
        <span>Очки</span><strong>{{ availablePoints }}</strong>
      </div>
    </header>

    <div class="loadouts" aria-label="Сборки талантов">
      <button
        v-for="loadout in (['LOADOUT_1', 'LOADOUT_2'] as const)"
        :key="loadout"
        type="button"
        :class="{ active: activeLoadoutId === loadout }"
        :aria-pressed="activeLoadoutId === loadout"
        :disabled="pending"
        @click="switchLoadout(loadout)"
      >
        Build {{ loadout === 'LOADOUT_1' ? 1 : 2 }}
      </button>
      <small>Активен {{ snapshot?.activeLoadoutId === 'LOADOUT_1' ? 'Build 1' : 'Build 2' }}</small>
    </div>

    <nav class="branches" aria-label="Ветки талантов">
      <button
        v-for="branch in warriorTalentBranches"
        :key="branch.id"
        :data-branch="branch.id"
        type="button"
        :class="{ active: activeBranchId === branch.id }"
        :aria-current="activeBranchId === branch.id ? 'page' : undefined"
        @click="activeBranchId = branch.id"
      >
        <span>{{ branch.name }}</span>
        <small>{{ spentInBranch(branch.id) }}</small>
      </button>
    </nav>

    <div class="branch-intro">
      <div>
        <span>Ветка</span>
        <h2>{{ activeBranch.name }}</h2>
        <p>{{ activeBranch.fantasy }}</p>
      </div>
      <strong>{{ spentInBranch(activeBranch.id) }}<small>/70</small></strong>
    </div>

    <div class="state-legend" aria-label="Состояния талантов">
      <span><i class="legend--maxed" />Изучено</span>
      <span><i class="legend--available" />Доступно</span>
      <span><i class="legend--locked" />Закрыто</span>
    </div>

    <div class="tree" :style="{ height: `${treeHeight}px` }">
      <svg
        class="tree__connections"
        :viewBox="`0 0 400 ${treeHeight}`"
        preserveAspectRatio="none"
        aria-hidden="true"
      >
        <path
          v-for="connection in connections"
          :key="connection.id"
          :d="connectionPath(connection)"
        />
      </svg>

      <section
        v-for="tier in tiers"
        :key="tier"
        class="tier"
        :class="{ 'tier--locked': spentInBranch(activeBranch.id) < (tier - 1) * 5 }"
        :style="{ top: `${(tier - 1) * rowHeight}px`, height: `${rowHeight}px` }"
      >
        <p class="tier__label">
          <b>Tier {{ tier }}</b>
          <span>{{ (tier - 1) * 5 }} очков</span>
        </p>
        <div
          class="tier__nodes"
          :style="{ gridTemplateColumns: `repeat(${talentsInTier(tier).length}, 1fr)` }"
        >
          <button
            v-for="talent in talentsInTier(tier)"
            :key="talent.id"
            data-talent-node
            type="button"
            class="talent-node"
            :class="`talent-node--${stateFor(talent)}`"
            :aria-label="`${talent.name}, ${rankFor(talent.id)} из ${talent.maxRank}`"
            @click="selectedTalent = talent"
          >
            <img
              v-if="artFor(talent)"
              class="talent-node__art"
              :src="artFor(talent)!"
              :alt="talent.name"
              loading="lazy"
              decoding="async"
            />
            <IconGenerator v-else :config="iconFor(talent)" :label="talent.name" />
            <span>{{ rankFor(talent.id) }}/{{ talent.maxRank }}</span>
          </button>
        </div>
      </section>
    </div>

    <footer class="talents__footer">
      <div><b>Сбросить билд</b><small>Только для выбранного loadout</small></div>
      <UIButton variant="ghost" :disabled="pending || (activeLoadout?.spentPoints ?? 0) === 0" @click="resetLoadout">Сброс</UIButton>
    </footer>

    <UIModal
      :open="selectedTalent !== null"
      :title="selectedTalent?.name ?? ''"
      @close="selectedTalent = null"
    >
      <article v-if="selectedTalent" class="talent-detail" data-talent-detail>
        <div class="talent-detail__identity">
          <img
            v-if="artFor(selectedTalent)"
            class="talent-detail__art"
            :src="artFor(selectedTalent)!"
            :alt="selectedTalent.name"
            decoding="async"
          />
          <IconGenerator v-else :config="iconFor(selectedTalent)" :label="selectedTalent.name" />
          <div>
            <p>{{ selectedTalent.englishName }}</p>
            <strong>Ранг {{ rankFor(selectedTalent.id) }}/{{ selectedTalent.maxRank }}</strong>
          </div>
        </div>
        <p class="talent-detail__state">{{ stateLabel(stateFor(selectedTalent)) }}</p>
        <p v-if="selectedTalent.unlockedAbilityId" class="talent-detail__ability">
          Открывает способность · {{ selectedTalent.unlockedAbilityId }}
        </p>
        <p v-if="selectedTalent.runtimeStatus === 'DEFERRED'" class="talent-detail__deferred">
          Эффект подготовлен и станет активен вместе с боевой системой своей фазы.
        </p>
        <p class="talent-detail__description">{{ selectedTalent.description }}</p>
        <dl>
          <div><dt>Tier</dt><dd>{{ selectedTalent.tier }}</dd></div>
          <div><dt>Нужно очков в ветке</dt><dd>{{ selectedTalent.requiredSpentPoints }}</dd></div>
          <div v-if="selectedTalent.prerequisites.length">
            <dt>Предыдущие таланты</dt><dd>{{ selectedTalent.prerequisites.map((item) => item.talentId).join(', ') }}</dd>
          </div>
        </dl>
      </article>
      <template #actions>
        <UIButton
          data-learn-talent
          :loading="pending"
          :disabled="!canLearnSelected"
          @click="learnSelected"
        >Изучить</UIButton>
      </template>
    </UIModal>
  </section>
</template>

<style scoped>
.talents {
  --branch-accent: var(--ui-color-secondary);
  min-height: 100%;
  color: var(--ui-color-text-primary);
  background:
    radial-gradient(circle at 50% 12rem, rgb(75 84 160 / 16%), transparent 21rem),
    linear-gradient(180deg, var(--ui-color-surface-1), var(--ui-color-background));
}
.talent-status { padding: var(--ui-space-6) var(--ui-space-4); text-align: center; }
.talent-status--error { color: var(--ui-color-danger); }
.talents[data-tone='berserker'] { --branch-accent: var(--ui-modifier-fire); }
.talents[data-tone='warlord'] { --branch-accent: var(--ui-color-warning); }
.talents__topbar,
.branch-intro,
.talents__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
}
.talents__topbar { padding: var(--ui-space-4); }
.eyebrow,
.talents h1,
.branch-intro h2,
.branch-intro p { margin: 0; }
.eyebrow {
  color: var(--branch-accent);
  font-size: var(--ui-font-size-xs);
  letter-spacing: 0.1em;
  text-transform: uppercase;
}
.talents h1,
.branch-intro h2 { font-family: var(--ui-font-display); }
.points {
  display: grid;
  min-width: 4.25rem;
  padding: var(--ui-space-2) var(--ui-space-3);
  border: 1px solid color-mix(in srgb, var(--branch-accent) 46%, transparent);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-surface-2);
  text-align: center;
}
.points span { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.points strong { color: var(--branch-accent); font-size: var(--ui-font-size-xl); }
.loadouts,
.branches { display: grid; border-block: 1px solid var(--ui-color-border); }
.loadouts {
  grid-template-columns: 1fr 1fr auto;
  align-items: center;
  padding: var(--ui-space-2) var(--ui-space-4);
  background: var(--ui-color-surface-2);
}
.loadouts button,
.branches button {
  min-height: var(--ui-touch-target);
  border: 0;
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
}
.loadouts button.active,
.branches button.active { color: var(--ui-color-text-primary); }
.loadouts button.active {
  border: 1px solid var(--ui-color-primary);
  border-radius: var(--ui-radius-sm);
  background: rgb(127 123 234 / 12%);
}
.loadouts > small { padding-left: var(--ui-space-2); color: var(--ui-color-text-muted); }
.branches {
  grid-template-columns: repeat(3, minmax(0, 1fr));
  border-top: 0;
  background: var(--ui-color-surface-1);
}
.branches button {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--ui-space-2);
}
.branches button.active::after {
  position: absolute;
  right: var(--ui-space-3);
  bottom: -1px;
  left: var(--ui-space-3);
  height: 2px;
  background: var(--branch-accent);
  box-shadow: 0 0 8px var(--branch-accent);
  content: '';
}
.branches small { color: var(--branch-accent); }
.branch-intro { padding: var(--ui-space-4); }
.branch-intro span,
.branch-intro p,
.branch-intro strong small { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.branch-intro strong { color: var(--branch-accent); font-size: var(--ui-font-size-xl); }
.state-legend {
  display: flex;
  gap: var(--ui-space-3);
  padding: 0 var(--ui-space-4) var(--ui-space-3);
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}
.state-legend span { display: flex; align-items: center; gap: var(--ui-space-1); }
.state-legend i { width: 0.5rem; height: 0.5rem; border-radius: 50%; }
.legend--maxed { background: var(--branch-accent); }
.legend--available { border: 1px solid var(--ui-color-primary); }
.legend--locked { background: var(--ui-color-disabled); }
.tree {
  position: relative;
  margin-inline: var(--ui-space-2);
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  background:
    linear-gradient(90deg, transparent 49.8%, rgb(127 123 234 / 8%) 50%, transparent 50.2%),
    rgb(5 8 16 / 66%);
}
.tree__connections { position: absolute; z-index: 0; inset: 0; width: 100%; height: 100%; }
.tree__connections path {
  fill: none;
  stroke: color-mix(in srgb, var(--branch-accent) 44%, var(--ui-color-border));
  stroke-width: 2;
  vector-effect: non-scaling-stroke;
}
.tier { position: absolute; right: 0; left: 0; border-bottom: 1px solid var(--ui-color-border); }
.tier--locked { background: rgb(3 5 10 / 32%); }
.tier__label {
  position: absolute;
  z-index: 2;
  top: var(--ui-space-1);
  left: var(--ui-space-2);
  display: grid;
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: 0.58rem;
  text-transform: uppercase;
}
.tier__label span { font-size: 0.52rem; }
.tier__nodes {
  position: relative;
  z-index: 1;
  display: grid;
  height: 100%;
  align-items: center;
  justify-items: center;
  padding: var(--ui-space-5) var(--ui-space-1) 0;
}
.talent-node {
  position: relative;
  width: 3.6rem;
  height: 3.6rem;
  padding: 0.3rem;
  border: 2px solid var(--ui-color-border-strong);
  border-radius: 50%;
  background: var(--ui-color-surface-2);
  color: var(--ui-color-text-muted);
  box-shadow: 0 0 0 4px rgb(8 12 22 / 82%);
}
.talent-node :deep(.icon-generator) { width: 100%; height: 100%; }
.talent-node__art {
  width: 100%;
  height: 100%;
  border-radius: 50%;
  object-fit: cover;
}
.talent-node > span {
  position: absolute;
  right: -0.35rem;
  bottom: -0.35rem;
  min-width: 1.75rem;
  padding: 0.1rem 0.25rem;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-xs);
  background: var(--ui-color-background);
  color: var(--ui-color-text-primary);
  font-size: 0.64rem;
}
.talent-node--available { border-color: var(--ui-color-primary); animation: talent-pulse 2.4s ease-in-out infinite; }
.talent-node--learned,
.talent-node--maxed { border-color: var(--branch-accent); box-shadow: 0 0 0 4px rgb(8 12 22 / 82%), 0 0 13px color-mix(in srgb, var(--branch-accent) 36%, transparent); }
.talent-node--locked,
.talent-node--prerequisite,
.talent-node--no-points { filter: grayscale(0.9); opacity: 0.42; }
.talents__footer {
  margin: var(--ui-space-3);
  padding: var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  background: var(--ui-color-surface-1);
}
.talents__footer div { display: grid; }
.talents__footer small { color: var(--ui-color-text-muted); }
.talent-detail__identity { display: flex; align-items: center; gap: var(--ui-space-3); }
.talent-detail__identity :deep(.icon-generator) { width: var(--ui-icon-slot-lg); height: var(--ui-icon-slot-lg); }
.talent-detail__art {
  width: var(--ui-icon-slot-lg);
  height: var(--ui-icon-slot-lg);
  flex: 0 0 auto;
  border: 1px solid var(--branch-accent);
  border-radius: var(--ui-radius-md);
  object-fit: cover;
  box-shadow: 0 0 12px color-mix(in srgb, var(--branch-accent) 32%, transparent);
}
.talent-detail__identity p { margin: 0; color: var(--ui-color-text-muted); }
.talent-detail__state { color: var(--ui-color-primary); font-weight: var(--ui-font-weight-semibold); }
.talent-detail__ability { color: var(--branch-accent); font-weight: var(--ui-font-weight-semibold); }
.talent-detail__deferred { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-sm); }
.talent-detail__description { white-space: pre-line; }
.talent-detail dl { display: grid; gap: var(--ui-space-2); }
.talent-detail dl div { display: flex; justify-content: space-between; gap: var(--ui-space-4); }
.talent-detail dt { color: var(--ui-color-text-muted); }
.talent-detail dd { margin: 0; text-align: right; }
@keyframes talent-pulse { 50% { box-shadow: 0 0 0 4px rgb(8 12 22 / 82%), var(--ui-glow-selected); } }
@media (prefers-reduced-motion: reduce) { .talent-node--available { animation: none; } }
@media (max-width: 350px) {
  .talent-node { width: 3.2rem; height: 3.2rem; }
  .loadouts { grid-template-columns: 1fr 1fr; }
  .loadouts > small { display: none; }
  .branches button { gap: var(--ui-space-1); font-size: var(--ui-font-size-sm); }
}
</style>
