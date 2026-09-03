<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { apiClient, ApiRequestError } from '@/api/apiClient'
import type { TalentLoadoutId, TalentNode, TalentSnapshot } from '@/api/contracts'
import { resolveTalentArt } from '@/game/talents/talentArt'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIModal } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName, IconConfig, ModifierName } from '@/ui/icons/icon.types'

type TalentState = 'locked' | 'level-locked' | 'available' | 'learned' | 'maxed' | 'prerequisite' | 'no-points'

const tiers = Array.from({ length: 9 }, (_, index) => index + 1)
const rowHeight = 128
const treeHeight = tiers.length * rowHeight
const session = useGameSessionStore()
const snapshot = ref<TalentSnapshot | null>(null)
const activeBranchId = ref('')
const activeLoadoutId = ref<TalentLoadoutId>('LOADOUT_1')
const selectedTalent = ref<TalentNode | null>(null)
const loading = ref(true)
const pending = ref(false)
const loadErrorCode = ref<string | null>(null)
const mutationErrorCode = ref<string | null>(null)
const retryMutationIds = new Map<string, string>()

const branches = computed(() => snapshot.value?.branches ?? [])
const talents = computed(() => snapshot.value?.nodes ?? [])
const activeLoadout = computed(() => snapshot.value?.loadouts.find((item) => item.id === activeLoadoutId.value))
const activeRanks = computed(() => activeLoadout.value?.selectedRanks ?? {})
const availablePoints = computed(() => Math.max(0, (snapshot.value?.earnedPoints ?? 0) - (activeLoadout.value?.spentPoints ?? 0)))
const activeBranch = computed(() => branches.value.find((branch) => branch.id === activeBranchId.value))
const branchTalents = computed(() => talents.value.filter((talent) => talent.branchId === activeBranchId.value))
const canLearnSelected = computed(() => selectedTalent.value !== null && canLearn(selectedTalent.value))
const classLabel = computed(() => snapshot.value?.classId === 'MAGE' ? 'Маг' : 'Воин')
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

function spentInBranch(branchId: string): number {
  return talents.value
    .filter((talent) => talent.branchId === branchId)
    .reduce((total, talent) => total + rankFor(talent.id), 0)
}

function canLearn(talent: TalentNode): boolean {
  const rank = rankFor(talent.id)
  if (rank >= talent.maxRank || availablePoints.value <= 0) return false
  if (spentInBranch(talent.branchId) < talent.requiredSpentPoints) return false
  if (talent.prerequisites.some((item) => rankFor(item.talentId) < item.requiredRank)) return false
  if (talent.requiredLevel !== null && (session.snapshot?.character?.level ?? 0) < talent.requiredLevel) return false
  return true
}

function stateFor(talent: TalentNode): TalentState {
  const rank = rankFor(talent.id)
  if (rank >= talent.maxRank) return 'maxed'
  if (talent.requiredLevel !== null && (session.snapshot?.character?.level ?? 0) < talent.requiredLevel) return 'level-locked'
  if (spentInBranch(talent.branchId) < talent.requiredSpentPoints) return 'locked'
  if (talent.prerequisites.some((item) => rankFor(item.talentId) < item.requiredRank)) return 'prerequisite'
  if (availablePoints.value <= 0) return 'no-points'
  return rank > 0 ? 'learned' : 'available'
}

function stateLabel(talent: TalentNode): string {
  const state = stateFor(talent)
  if (state === 'level-locked') return `Требуется уровень ${talent.requiredLevel}`
  return {
    locked: 'Закрыто: вложите больше очков в ветку',
    available: 'Доступно для изучения',
    learned: 'Изучено, можно улучшить',
    maxed: 'Изучено полностью',
    prerequisite: 'Сначала изучите предыдущий талант',
    'no-points': 'Нет свободных очков талантов',
  }[state]
}

function loadErrorLabel(code: string | null): string {
  if (!code || code === 'talent_unavailable') return 'Дерево талантов сейчас недоступно.'
  if (code === 'network_unavailable') return 'Нет соединения с сервером. Проверьте подключение и попробуйте снова.'
  return `Не удалось загрузить дерево талантов. Код ошибки: ${code}`
}

function mutationErrorLabel(code: string): string {
  return {
    talent_insufficient_points: 'Недостаточно очков талантов.',
    talent_prerequisite_not_met: 'Сначала изучите требуемый предыдущий талант.',
    talent_tier_locked: 'Нужно вложить больше очков в эту ветку.',
    talent_level_required: 'Уровень персонажа пока недостаточен.',
    talent_state_conflict: 'Состояние талантов изменилось. Данные обновлены.',
  }[code] ?? `Не удалось изменить таланты. Код ошибки: ${code}`
}

function abilityLabel(abilityId: string): string {
  return {
    STRIKE: 'Удар', WILD_STRIKE: 'Дикий удар', WHIRLWIND: 'Вихрь', BASTION: 'Бастион',
    BERSERK: 'Берсерк', SHIELD_BASH: 'Удар щитом', PROVOKE: 'Провокация',
    HEAVY_BLOW: 'Тяжёлый удар', BATTLE_FOCUS: 'Боевой фокус', BATTLE_SHOUT: 'Боевой клич',
    MAGE_FIREBALL: 'Огненный шар', MAGE_ARCANE_SPARK: 'Тайная искра', MAGE_ICE_SHARD: 'Ледяной осколок',
    FLAME_FLASH: 'Вспышка', FIRE_WAVE: 'Огненная волна', COMBUSTION: 'Возгорание', FIRE_COMET: 'Огненная комета',
  }[abilityId] ?? abilityId.replace(/_/g, ' ')
}

function talentName(talentId: string): string {
  return talents.value.find((talent) => talent.id === talentId)?.name ?? talentId
}

function glyphFor(talent: TalentNode): GlyphName {
  if (talent.branchId === 'FIRE') return 'fire'
  if (talent.branchId === 'GUARDIAN') return 'shield'
  if (talent.branchId === 'BERSERKER') return 'greatsword'
  if (talent.branchId === 'FROST') return 'ice'
  return talent.maxRank === 1 ? 'star' : 'staff'
}

function modifierFor(talent: TalentNode): ModifierName {
  if (talent.branchId === 'FIRE' || talent.branchId === 'BERSERKER') return 'fire'
  if (talent.branchId === 'FROST' || talent.branchId === 'GUARDIAN') return 'ice'
  if (talent.branchId === 'ARCANE') return 'lightning'
  return 'holy'
}

function iconFor(talent: TalentNode): IconConfig {
  return {
    id: `talent-${talent.id}`,
    glyph: glyphFor(talent),
    category: 'skill',
    modifier: modifierFor(talent),
    rarity: talent.tier === 9 ? 'legendary' : talent.maxRank === 1 ? 'epic' : 'rare',
  }
}

function artFor(talent: TalentNode): string | null {
  return resolveTalentArt(talent.iconId)
}

function createMutationId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`
}

function nodePoint(talentId: string): { x: number; y: number } {
  const talent = branchTalents.value.find((item) => item.id === talentId)!
  const row = talentsInTier(talent.tier)
  const index = row.findIndex((item) => item.id === talentId)
  return { x: ((index + 0.5) / row.length) * 400, y: (talent.tier - 0.5) * rowHeight }
}

function connectionPath(connection: (typeof connections.value)[number]): string {
  const middleY = (connection.from.y + connection.to.y) / 2
  return `M ${connection.from.x} ${connection.from.y} C ${connection.from.x} ${middleY}, ${connection.to.x} ${middleY}, ${connection.to.x} ${connection.to.y}`
}

async function loadTalents(): Promise<void> {
  loading.value = true
  loadErrorCode.value = null
  mutationErrorCode.value = null
  try {
    applySnapshot(await apiClient.request<TalentSnapshot>('/api/v1/talents/'))
  } catch (error) {
    loadErrorCode.value = error instanceof ApiRequestError ? error.code : 'network_unavailable'
  } finally {
    loading.value = false
  }
}

function applySnapshot(next: TalentSnapshot): void {
  snapshot.value = next
  activeLoadoutId.value = next.activeLoadoutId
  if (!next.branches.some((branch) => branch.id === activeBranchId.value)) {
    activeBranchId.value = next.branches[0]?.id ?? ''
  }
}

async function mutate(path: string, mutationKey: string, body: object): Promise<void> {
  if (!snapshot.value || pending.value) return
  pending.value = true
  mutationErrorCode.value = null
  const mutationId = retryMutationIds.get(mutationKey) ?? createMutationId()
  retryMutationIds.set(mutationKey, mutationId)
  try {
    applySnapshot(await apiClient.request<TalentSnapshot>(path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...body, mutationId }),
    }))
    retryMutationIds.delete(mutationKey)
    await session.refreshSnapshot()
  } catch (error) {
    mutationErrorCode.value = error instanceof ApiRequestError ? error.code : 'network_unavailable'
    if (error instanceof ApiRequestError && error.code === 'talent_state_conflict') {
      retryMutationIds.delete(mutationKey)
      await loadTalents()
      await session.refreshSnapshot()
    }
  } finally {
    pending.value = false
  }
}

async function learnSelected(): Promise<void> {
  if (!selectedTalent.value || !snapshot.value || !canLearn(selectedTalent.value)) return
  await mutate('/api/v1/talents/learn', `learn:${activeLoadoutId.value}:${selectedTalent.value.id}`, {
    talentId: selectedTalent.value.id,
    loadoutId: activeLoadoutId.value,
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
    loadoutId: activeLoadoutId.value,
    expectedStateVersion: snapshot.value.stateVersion,
  })
}

onMounted(loadTalents)
</script>

<template>
  <div v-if="loading" class="talent-status" role="status">Загружаем дерево талантов…</div>
  <div v-else-if="loadErrorCode || !activeBranch" class="talent-status talent-status--error" role="alert">
    <p>{{ loadErrorLabel(loadErrorCode) }}</p><UIButton variant="ghost" @click="loadTalents">Повторить</UIButton>
  </div>
  <section v-else class="talents" data-talent-tree :data-tone="activeBranch.id.toLowerCase()">
    <header class="talents__topbar">
      <div><p class="eyebrow">{{ classLabel }} · дерево развития</p><h1>Таланты</h1></div>
      <div class="points"><span>Свободно</span><strong>{{ availablePoints }}</strong></div>
    </header>
    <p v-if="mutationErrorCode" class="mutation-error" role="alert">{{ mutationErrorLabel(mutationErrorCode) }}</p>

    <div class="loadouts">
      <button v-for="loadout in (['LOADOUT_1', 'LOADOUT_2'] as const)" :key="loadout" type="button"
        :class="{ active: activeLoadoutId === loadout }" :disabled="pending" @click="switchLoadout(loadout)">
        Сборка {{ loadout === 'LOADOUT_1' ? 1 : 2 }}
      </button>
      <small>Активна сборка {{ snapshot?.activeLoadoutId === 'LOADOUT_1' ? 1 : 2 }}</small>
    </div>

    <nav class="branches" :style="{ gridTemplateColumns: `repeat(${Math.max(1, branches.length)}, minmax(0, 1fr))` }">
      <button v-for="branch in branches" :key="branch.id" type="button" :class="{ active: activeBranchId === branch.id }" @click="activeBranchId = branch.id">
        <span>{{ branch.name }}</span><small>{{ spentInBranch(branch.id) }}</small>
      </button>
    </nav>

    <div class="branch-intro"><div><span>Ветка</span><h2>{{ activeBranch.name }}</h2><p>{{ activeBranch.fantasy }}</p></div><strong>{{ spentInBranch(activeBranch.id) }}<small> очков</small></strong></div>

    <div class="tree" :style="{ height: `${treeHeight}px` }">
      <svg class="tree__connections" :viewBox="`0 0 400 ${treeHeight}`" preserveAspectRatio="none" aria-hidden="true"><path v-for="connection in connections" :key="connection.id" :d="connectionPath(connection)" /></svg>
      <section v-for="tier in tiers" :key="tier" class="tier" :class="{ 'tier--locked': spentInBranch(activeBranch.id) < (tier - 1) * 5 }" :style="{ top: `${(tier - 1) * rowHeight}px`, height: `${rowHeight}px` }">
        <p class="tier__label"><b>Ряд {{ tier }}</b><span>нужно {{ (tier - 1) * 5 }} очков</span></p>
        <div class="tier__nodes" :style="{ gridTemplateColumns: `repeat(${Math.max(1, talentsInTier(tier).length)}, 1fr)` }">
          <button v-for="talent in talentsInTier(tier)" :key="talent.id" data-talent-node type="button" class="talent-node" :class="`talent-node--${stateFor(talent)}`" @click="selectedTalent = talent">
            <img v-if="artFor(talent)" class="talent-node__art" :src="artFor(talent)!" :alt="talent.name" />
            <IconGenerator v-else :config="iconFor(talent)" :label="talent.name" />
            <span>{{ rankFor(talent.id) }}/{{ talent.maxRank }}</span>
          </button>
        </div>
      </section>
    </div>

    <footer class="talents__footer"><div><b>Сбросить сборку</b><small>Сбрасывается только выбранная сборка</small></div><UIButton variant="ghost" :disabled="pending || (activeLoadout?.spentPoints ?? 0) === 0" @click="resetLoadout">Сбросить</UIButton></footer>

    <UIModal :open="selectedTalent !== null" :title="selectedTalent?.name ?? ''" @close="selectedTalent = null">
      <article v-if="selectedTalent" class="talent-detail">
        <div class="talent-detail__identity">
          <img v-if="artFor(selectedTalent)" class="talent-detail__art" :src="artFor(selectedTalent)!" :alt="selectedTalent.name" />
          <IconGenerator v-else :config="iconFor(selectedTalent)" :label="selectedTalent.name" />
          <div><p>{{ activeBranch.name }} · ряд {{ selectedTalent.tier }}</p><strong>Ранг {{ rankFor(selectedTalent.id) }}/{{ selectedTalent.maxRank }}</strong></div>
        </div>
        <p class="talent-detail__state">{{ stateLabel(selectedTalent) }}</p>
        <p v-if="selectedTalent.unlockedAbilityId" class="talent-detail__ability">Открывает способность «{{ abilityLabel(selectedTalent.unlockedAbilityId) }}»</p>
        <p v-if="selectedTalent.runtimeStatus === 'DEFERRED'" class="talent-detail__deferred">Этот эффект пока не участвует в бою.</p>
        <p class="talent-detail__description">{{ selectedTalent.description }}</p>
        <dl>
          <div><dt>Ряд дерева</dt><dd>{{ selectedTalent.tier }}</dd></div>
          <div><dt>Нужно очков в ветке</dt><dd>{{ selectedTalent.requiredSpentPoints }}</dd></div>
          <div v-if="selectedTalent.prerequisites.length"><dt>Нужные таланты</dt><dd>{{ selectedTalent.prerequisites.map((item) => talentName(item.talentId)).join(', ') }}</dd></div>
        </dl>
      </article>
      <template #actions><UIButton :loading="pending" :disabled="!canLearnSelected" @click="learnSelected">Изучить</UIButton></template>
    </UIModal>
  </section>
</template>

<style scoped>
.talents{--branch-accent:var(--ui-color-secondary);min-height:100%;color:var(--ui-color-text-primary);background:radial-gradient(circle at 50% 12rem,rgb(75 84 160 / 16%),transparent 21rem),linear-gradient(180deg,var(--ui-color-surface-1),var(--ui-color-background))}.talents[data-tone='fire'],.talents[data-tone='berserker']{--branch-accent:var(--ui-modifier-fire)}.talents[data-tone='guardian'],.talents[data-tone='frost']{--branch-accent:var(--ui-modifier-ice)}.talents[data-tone='warlord']{--branch-accent:var(--ui-color-warning)}
.talent-status{padding:var(--ui-space-6) var(--ui-space-4);text-align:center}.talent-status--error,.mutation-error{color:var(--ui-color-danger)}.mutation-error{margin:0 var(--ui-space-4) var(--ui-space-3)}
.talents__topbar,.branch-intro,.talents__footer{display:flex;align-items:center;justify-content:space-between;gap:var(--ui-space-3)}.talents__topbar,.branch-intro{padding:var(--ui-space-4)}.eyebrow,.talents h1,.branch-intro h2,.branch-intro p{margin:0}.eyebrow{color:var(--branch-accent);font-size:var(--ui-font-size-xs);letter-spacing:.1em;text-transform:uppercase}.talents h1,.branch-intro h2{font-family:var(--ui-font-display)}
.points{display:grid;min-width:4.25rem;padding:var(--ui-space-2) var(--ui-space-3);border:1px solid color-mix(in srgb,var(--branch-accent) 46%,transparent);border-radius:var(--ui-radius-md);background:var(--ui-color-surface-2);text-align:center}.points span,.branch-intro span,.branch-intro p,.branch-intro strong small{color:var(--ui-color-text-muted);font-size:var(--ui-font-size-xs)}.points strong,.branch-intro strong{color:var(--branch-accent);font-size:var(--ui-font-size-xl)}
.loadouts,.branches{display:grid;border-block:1px solid var(--ui-color-border)}.loadouts{grid-template-columns:1fr 1fr auto;align-items:center;padding:var(--ui-space-2) var(--ui-space-4);background:var(--ui-color-surface-2)}.loadouts button,.branches button{min-height:var(--ui-touch-target);border:0;background:transparent;color:var(--ui-color-text-muted);font:inherit}.loadouts button.active,.branches button.active{color:var(--ui-color-text-primary)}.loadouts button.active{border:1px solid var(--ui-color-primary);border-radius:var(--ui-radius-sm)}.loadouts>small{padding-left:var(--ui-space-2);color:var(--ui-color-text-muted)}.branches{border-top:0;background:var(--ui-color-surface-1)}.branches button{position:relative;display:flex;align-items:center;justify-content:center;gap:var(--ui-space-2)}.branches button.active::after{position:absolute;right:var(--ui-space-3);bottom:-1px;left:var(--ui-space-3);height:2px;background:var(--branch-accent);content:''}.branches small{color:var(--branch-accent)}
.tree{position:relative;margin-inline:var(--ui-space-2);overflow:hidden;border:1px solid var(--ui-color-border);background:rgb(5 8 16 / 66%)}.tree__connections{position:absolute;z-index:0;inset:0;width:100%;height:100%}.tree__connections path{fill:none;stroke:color-mix(in srgb,var(--branch-accent) 44%,var(--ui-color-border));stroke-width:2;vector-effect:non-scaling-stroke}.tier{position:absolute;right:0;left:0;border-bottom:1px solid var(--ui-color-border)}.tier--locked{background:rgb(3 5 10 / 32%)}.tier__label{position:absolute;z-index:2;top:var(--ui-space-1);left:var(--ui-space-2);display:grid;margin:0;color:var(--ui-color-text-muted);font-size:.58rem;text-transform:uppercase}.tier__label span{font-size:.52rem}.tier__nodes{position:relative;z-index:1;display:grid;height:100%;align-items:center;justify-items:center;padding:var(--ui-space-5) var(--ui-space-1) 0}
.talent-node{position:relative;width:3.6rem;height:3.6rem;padding:.3rem;border:2px solid var(--ui-color-border-strong);border-radius:50%;background:var(--ui-color-surface-2);color:var(--ui-color-text-muted);box-shadow:0 0 0 4px rgb(8 12 22 / 82%)}.talent-node :deep(.icon-generator){width:100%;height:100%}.talent-node__art{width:100%;height:100%;border-radius:50%;object-fit:cover}.talent-node>span{position:absolute;right:-.35rem;bottom:-.35rem;min-width:1.75rem;padding:.1rem .25rem;border:1px solid var(--ui-color-border-strong);border-radius:var(--ui-radius-xs);background:var(--ui-color-background);color:var(--ui-color-text-primary);font-size:.64rem}.talent-node--available{border-color:var(--ui-color-primary)}.talent-node--learned,.talent-node--maxed{border-color:var(--branch-accent);box-shadow:0 0 0 4px rgb(8 12 22 / 82%),0 0 13px color-mix(in srgb,var(--branch-accent) 36%,transparent)}.talent-node--locked,.talent-node--level-locked,.talent-node--prerequisite,.talent-node--no-points{filter:grayscale(.9);opacity:.42}
.talents__footer{margin:var(--ui-space-3);padding:var(--ui-space-3);border:1px solid var(--ui-color-border);background:var(--ui-color-surface-1)}.talents__footer div{display:grid}.talents__footer small,.talent-detail__identity p,.talent-detail__deferred,dt{color:var(--ui-color-text-muted)}.talent-detail__identity{display:flex;align-items:center;gap:var(--ui-space-3)}.talent-detail__identity :deep(.icon-generator){width:var(--ui-icon-slot-lg);height:var(--ui-icon-slot-lg)}.talent-detail__art{width:var(--ui-icon-slot-lg);height:var(--ui-icon-slot-lg);border:1px solid var(--branch-accent);border-radius:var(--ui-radius-md);object-fit:cover}.talent-detail__identity p{margin:0}.talent-detail__state{color:var(--ui-color-primary);font-weight:var(--ui-font-weight-semibold)}.talent-detail__ability{color:var(--branch-accent);font-weight:var(--ui-font-weight-semibold)}.talent-detail__description{white-space:pre-line}.talent-detail dl{display:grid;gap:var(--ui-space-2)}.talent-detail dl div{display:flex;justify-content:space-between;gap:var(--ui-space-4)}dd{margin:0;text-align:right}
@media(max-width:350px){.talent-node{width:3.2rem;height:3.2rem}.loadouts{grid-template-columns:1fr 1fr}.loadouts>small{display:none}}
</style>
