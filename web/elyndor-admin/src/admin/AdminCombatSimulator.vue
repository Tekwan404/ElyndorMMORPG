<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { apiClient, ApiRequestError } from '@/api/apiClient'
import type { ContentAdminSimulation } from '@/api/contracts'

interface ClassOption {
  id: string
}

interface MonsterOption {
  id: string
  name: string
  level: number
}

interface TalentSkillOption {
  classId: string
  talentId: string
  name: string
  abilityId: string
}

const props = defineProps<{
  payloadJson: string
  classes: ClassOption[]
  monsters: MonsterOption[]
  talentSkills: TalentSkillOption[]
}>()

const classId = ref('')
const playerLevel = ref(5)
const monsterId = ref('')
const iterations = ref(100)
const seed = ref(1337)
const maxDurationSeconds = ref(90)
const selectedTalentIds = ref<string[]>([])
const running = ref(false)
const result = ref<ContentAdminSimulation | null>(null)
const errorMessage = ref('')

const currentTalentSkills = computed(() =>
  props.talentSkills.filter(option => option.classId === classId.value),
)

const canRun = computed(() =>
  Boolean(classId.value && monsterId.value)
  && playerLevel.value >= 1
  && playerLevel.value <= 60
  && iterations.value >= 1
  && iterations.value <= 1000
  && maxDurationSeconds.value >= 1
  && maxDurationSeconds.value <= 180,
)

watch(
  () => props.classes,
  (classes) => {
    if (!classes.some(option => option.id === classId.value)) {
      classId.value = classes[0]?.id ?? ''
      selectedTalentIds.value = []
    }
  },
  { immediate: true },
)

watch(
  classId,
  () => {
    selectedTalentIds.value = []
    result.value = null
  },
)

watch(
  () => props.monsters,
  (monsters) => {
    if (!monsters.some(option => option.id === monsterId.value)) {
      monsterId.value = monsters[0]?.id ?? ''
    }
  },
  { immediate: true },
)

watch(
  () => props.payloadJson,
  () => {
    result.value = null
    errorMessage.value = ''
  },
)

async function runSimulation(): Promise<void> {
  if (!canRun.value || running.value) return
  running.value = true
  result.value = null
  errorMessage.value = ''

  try {
    result.value = await apiClient.request<ContentAdminSimulation>(
      '/api/v1/admin/content/simulate',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          payloadJson: props.payloadJson,
          classId: classId.value,
          playerLevel: playerLevel.value,
          monsterId: monsterId.value,
          iterations: iterations.value,
          seed: seed.value,
          maxDurationSeconds: maxDurationSeconds.value,
          abilityPriority: null,
          selectedTalentRanks: Object.fromEntries(
            selectedTalentIds.value.map(talentId => [talentId, 1]),
          ),
        }),
      },
    )
  } catch (error) {
    errorMessage.value = error instanceof ApiRequestError
      ? error.code
      : 'simulation_unavailable'
  } finally {
    running.value = false
  }
}

function toggleTalent(talentId: string): void {
  selectedTalentIds.value = selectedTalentIds.value.includes(talentId)
    ? selectedTalentIds.value.filter(id => id !== talentId)
    : [...selectedTalentIds.value, talentId]
  result.value = null
}

function formatNumber(value: number, digits = 1): string {
  return Number.isFinite(value) ? value.toFixed(digits) : '0'
}
</script>

<template>
  <section class="simulator" data-testid="combat-simulator">
    <header class="simulator__header">
      <div>
        <small>HEADLESS BALANCE LAB</small>
        <h2>Combat Simulator</h2>
        <p>Запускает реальный CombatSession kernel против текущего локального draft. Никаких XP, gold, loot или сохранения персонажа.</p>
      </div>
      <span class="safe-badge">STATELESS</span>
    </header>

    <div class="controls">
      <label>
        <span>Class</span>
        <select v-model="classId" data-testid="simulation-class">
          <option v-for="option in classes" :key="option.id" :value="option.id">{{ option.id }}</option>
        </select>
      </label>
      <label>
        <span>Player level</span>
        <input v-model.number="playerLevel" data-testid="simulation-level" type="number" min="1" max="60" />
      </label>
      <label>
        <span>Monster</span>
        <select v-model="monsterId" data-testid="simulation-monster">
          <option v-for="option in monsters" :key="option.id" :value="option.id">
            {{ option.id }} · Lv {{ option.level }} · {{ option.name }}
          </option>
        </select>
      </label>
      <label>
        <span>Iterations</span>
        <input v-model.number="iterations" data-testid="simulation-iterations" type="number" min="1" max="1000" step="10" />
      </label>
      <label>
        <span>Seed</span>
        <input v-model.number="seed" data-testid="simulation-seed" type="number" />
      </label>
      <label>
        <span>Max fight, sec</span>
        <input v-model.number="maxDurationSeconds" type="number" min="1" max="180" />
      </label>
      <button class="primary" data-testid="simulation-run" type="button" :disabled="!canRun || running" @click="runSimulation">
        {{ running ? 'Simulating…' : `Run ${iterations} fights` }}
      </button>
    </div>

    <div class="talent-skills">
      <div>
        <b>Skill talents</b>
        <span>Скилл участвует в симуляции только если выбран talent с UNLOCK_ABILITY.</span>
      </div>
      <label
        v-for="skill in currentTalentSkills"
        :key="skill.talentId"
        class="talent-skill"
      >
        <input
          data-testid="simulation-talent"
          type="checkbox"
          :checked="selectedTalentIds.includes(skill.talentId)"
          @change="toggleTalent(skill.talentId)"
        />
        <span><b>{{ skill.name }}</b><small>{{ skill.talentId }} → {{ skill.abilityId }}</small></span>
      </label>
      <p v-if="currentTalentSkills.length === 0" class="muted">Для этого класса в текущем draft нет talent nodes с UNLOCK_ABILITY.</p>
    </div>

    <p class="scope-note">
      MVP применяет выбранные skill talents через реальный TalentModifierResolver. Полный passive talent build и equipment presets добавим следующим слоем.
    </p>

    <p v-if="errorMessage" class="message message--danger">{{ errorMessage }}</p>

    <template v-if="result">
      <div class="result-meta">
        <span>{{ result.contentVersion }}</span>
        <span>{{ result.balanceVersion }}</span>
        <span>{{ result.classId }} Lv {{ result.playerLevel }}</span>
        <span>vs {{ result.monsterId }}</span>
        <span>seed {{ seed }}</span>
      </div>

      <div class="metrics">
        <article>
          <small>WIN RATE</small>
          <strong data-testid="simulation-win-rate">{{ formatNumber(result.winRatePercent) }}%</strong>
          <span>{{ result.victories }}W · {{ result.defeats }}L · {{ result.timeouts }}T</span>
        </article>
        <article>
          <small>PLAYER DPS</small>
          <strong>{{ formatNumber(result.averagePlayerDps) }}</strong>
          <span>enemy {{ formatNumber(result.averageEnemyDps) }}</span>
        </article>
        <article>
          <small>AVG DURATION</small>
          <strong>{{ formatNumber(result.averageDurationSeconds) }}s</strong>
          <span>P50 {{ formatNumber(result.p50DurationSeconds) }} · P95 {{ formatNumber(result.p95DurationSeconds) }}</span>
        </article>
        <article>
          <small>AVG HP LEFT</small>
          <strong>{{ formatNumber(result.averagePlayerRemainingHp) }}</strong>
          <span>{{ result.iterations }} simulations</span>
        </article>
      </div>

      <div class="damage-table">
        <h3>Player damage breakdown</h3>
        <div class="damage-table__header">
          <span>Source</span><span>Avg damage</span><span>Share</span>
        </div>
        <div v-for="source in result.damageSources" :key="source.definitionId" class="damage-table__row">
          <code>{{ source.definitionId }}</code>
          <span>{{ formatNumber(source.averageDamage) }}</span>
          <span>{{ formatNumber(source.damageSharePercent) }}%</span>
        </div>
        <p v-if="result.damageSources.length === 0" class="muted">Нет нанесённого урона.</p>
      </div>
    </template>
  </section>
</template>

<style scoped>
.simulator { max-width: 86rem; margin: var(--ui-space-4) auto 0; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-1); color: var(--ui-color-text-primary); }
.simulator__header { display: flex; align-items: start; justify-content: space-between; gap: var(--ui-space-3); }
.simulator__header h2, .damage-table h3 { margin: 0; font-family: var(--ui-font-display); }
.simulator__header small { color: var(--ui-color-primary); letter-spacing: .1em; }
.simulator__header p, .scope-note { margin-bottom: 0; color: var(--ui-color-text-muted); }
.safe-badge { padding: var(--ui-space-1) var(--ui-space-2); border: 1px solid var(--ui-color-success); border-radius: var(--ui-radius-round); color: var(--ui-color-success); font-size: var(--ui-font-size-xs); }
.controls { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)) auto; align-items: end; gap: var(--ui-space-2); margin-top: var(--ui-space-3); }
.controls label { display: grid; gap: var(--ui-space-1); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.controls input, .controls select, .controls button { min-height: var(--ui-touch-target); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); color: inherit; font: inherit; }
.controls button.primary { border-color: var(--ui-color-primary); color: var(--ui-color-primary); cursor: pointer; }
.controls button:disabled { opacity: .45; cursor: not-allowed; }
.talent-skills { display: grid; gap: var(--ui-space-2); margin-top: var(--ui-space-3); padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); }
.talent-skills > div { display: grid; gap: var(--ui-space-1); }
.talent-skills > div span { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.talent-skill { display: flex; align-items: center; gap: var(--ui-space-2); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); }
.talent-skill input { width: 1rem; min-height: auto; }
.talent-skill span { display: grid; gap: var(--ui-space-1); }
.talent-skill small { color: var(--ui-color-text-muted); }
.result-meta { display: flex; flex-wrap: wrap; gap: var(--ui-space-1); margin-top: var(--ui-space-3); }
.result-meta span { padding: var(--ui-space-1) var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-round); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.metrics { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: var(--ui-space-2); margin-top: var(--ui-space-2); }
.metrics article { display: grid; gap: var(--ui-space-1); padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); }
.metrics small, .metrics span { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.metrics strong { font-size: 1.5rem; font-family: var(--ui-font-display); }
.damage-table { margin-top: var(--ui-space-3); }
.damage-table__header, .damage-table__row { display: grid; grid-template-columns: minmax(0, 2fr) 1fr 1fr; gap: var(--ui-space-2); padding: var(--ui-space-2); border-top: 1px solid var(--ui-color-border); }
.damage-table__header { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.message { padding: var(--ui-space-2); border: 1px solid var(--ui-color-danger); border-radius: var(--ui-radius-sm); color: var(--ui-color-danger); }
.muted { color: var(--ui-color-text-muted); }
@media (max-width: 980px) { .controls { grid-template-columns: repeat(3, minmax(0, 1fr)); } .metrics { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 560px) { .simulator__header { flex-direction: column; } .controls, .metrics { grid-template-columns: 1fr; } }
</style>
