<script setup lang="ts">
import { computed, ref, watch } from 'vue'

type JsonRecord = Record<string, unknown>

const modifierTypes = [
  'StatModifier',
  'AbilityModifier',
  'EffectModifier',
  'ResourceModifier',
  'EventTriggered',
  'EquipmentConditional',
] as const

const props = defineProps<{
  entity: JsonRecord
  abilityIds: string[]
}>()

const emit = defineEmits<{
  'update:entity': [entity: JsonRecord]
}>()

const selectedBranchId = ref('')
const selectedNodeId = ref('')

const branches = computed<JsonRecord[]>(() =>
  Array.isArray(props.entity.branches) ? props.entity.branches.filter(isRecord) : [],
)
const nodes = computed<JsonRecord[]>(() =>
  Array.isArray(props.entity.nodes) ? props.entity.nodes.filter(isRecord) : [],
)
const filteredNodes = computed(() =>
  nodes.value.filter(node => stringValue(node.branchId) === selectedBranchId.value),
)
const selectedNodeIndex = computed(() =>
  nodes.value.findIndex(node => stringValue(node.id) === selectedNodeId.value),
)
const selectedNode = computed<JsonRecord | null>(() =>
  selectedNodeIndex.value >= 0 ? nodes.value[selectedNodeIndex.value] ?? null : null,
)
const prerequisites = computed<JsonRecord[]>(() =>
  selectedNode.value && Array.isArray(selectedNode.value.prerequisites)
    ? selectedNode.value.prerequisites.filter(isRecord)
    : [],
)
const modifiers = computed<JsonRecord[]>(() =>
  selectedNode.value && Array.isArray(selectedNode.value.modifiers)
    ? selectedNode.value.modifiers.filter(isRecord)
    : [],
)

watch(
  () => [props.entity.id, nodes.value.length],
  () => initializeSelection(),
  { immediate: true },
)

function initializeSelection(): void {
  const branchStillExists = branches.value.some(branch => stringValue(branch.id) === selectedBranchId.value)
  if (!branchStillExists) selectedBranchId.value = stringValue(branches.value[0]?.id)
  const visible = filteredNodes.value
  if (!visible.some(node => stringValue(node.id) === selectedNodeId.value)) {
    selectedNodeId.value = stringValue(visible[0]?.id)
  }
}

function changeBranch(event: Event): void {
  selectedBranchId.value = (event.target as HTMLSelectElement).value
  selectedNodeId.value = stringValue(filteredNodes.value[0]?.id)
}

function updateTreeField(key: string, value: unknown): void {
  const next = cloneRecord(props.entity)
  next[key] = value
  emit('update:entity', next)
}

function setNodeBranch(event: Event): void {
  if (selectedNodeIndex.value < 0) return
  const branchId = (event.target as HTMLSelectElement).value
  const next = cloneRecord(props.entity)
  const source = Array.isArray(next.nodes) ? next.nodes : []
  const node = source[selectedNodeIndex.value]
  if (!isRecord(node)) return
  node.branchId = branchId
  next.nodes = source

  const branchSource = Array.isArray(next.branches) ? next.branches : []
  for (const branch of branchSource) {
    if (!isRecord(branch)) continue
    const id = stringValue(branch.id)
    branch.nodeCount = source.filter(candidate => isRecord(candidate) && candidate.branchId === id).length
  }
  next.branches = branchSource
  selectedBranchId.value = branchId
  emit('update:entity', next)
}

function updateNodeField(key: string, value: unknown): void {
  if (selectedNodeIndex.value < 0) return
  const next = cloneRecord(props.entity)
  const source = Array.isArray(next.nodes) ? next.nodes : []
  const node = source[selectedNodeIndex.value]
  if (!isRecord(node)) return
  node[key] = value
  next.nodes = source
  emit('update:entity', next)
}

function updatePrerequisite(index: number, key: string, value: unknown): void {
  mutateSelectedNode(node => {
    const source = Array.isArray(node.prerequisites) ? node.prerequisites : []
    const prerequisite = source[index]
    if (!isRecord(prerequisite)) return
    prerequisite[key] = value
    node.prerequisites = source
  })
}

function setPrerequisiteTalent(index: number, event: Event): void {
  updatePrerequisite(index, 'talentId', (event.target as HTMLSelectElement).value)
}

function setPrerequisiteRank(index: number, event: Event): void {
  const input = event.target as HTMLInputElement
  if (Number.isFinite(input.valueAsNumber)) {
    updatePrerequisite(index, 'requiredRank', input.valueAsNumber)
  }
}

function addPrerequisite(): void {
  const used = new Set(prerequisites.value.map(item => stringValue(item.talentId)))
  const candidate = nodes.value.find(node =>
    stringValue(node.id) !== selectedNodeId.value && !used.has(stringValue(node.id)),
  )
  if (!candidate) return
  mutateSelectedNode(node => {
    const source = Array.isArray(node.prerequisites) ? node.prerequisites : []
    source.push({ talentId: stringValue(candidate.id), requiredRank: 1 })
    node.prerequisites = source
  })
}

function removePrerequisite(index: number): void {
  mutateSelectedNode(node => {
    const source = Array.isArray(node.prerequisites) ? node.prerequisites : []
    source.splice(index, 1)
    node.prerequisites = source
  })
}

function addModifier(): void {
  const rank = Math.max(1, numberValue(selectedNode.value?.maxRank, 1))
  mutateSelectedNode(node => {
    const source = Array.isArray(node.modifiers) ? node.modifiers : []
    source.push({
      type: 'StatModifier',
      key: 'ATTACK_POWER_PERCENT',
      values: Array(rank).fill(0),
      runtimeStatus: 'Supported',
    })
    node.modifiers = source
  })
}

function removeModifier(index: number): void {
  mutateSelectedNode(node => {
    const source = Array.isArray(node.modifiers) ? node.modifiers : []
    source.splice(index, 1)
    node.modifiers = source
  })
}

function updateModifier(index: number, key: string, value: unknown): void {
  mutateSelectedNode(node => {
    const source = Array.isArray(node.modifiers) ? node.modifiers : []
    const modifier = source[index]
    if (!isRecord(modifier)) return
    modifier[key] = value
    if (key === 'runtimeStatus' && value === 'Supported') {
      delete modifier.deferredOwner
    }
    node.modifiers = source
  })
}

function setMaxRank(event: Event): void {
  const input = event.target as HTMLInputElement
  if (!Number.isFinite(input.valueAsNumber)) return
  const rank = Math.max(1, Math.trunc(input.valueAsNumber))
  mutateSelectedNode(node => {
    node.maxRank = rank
    const source = Array.isArray(node.modifiers) ? node.modifiers : []
    for (const modifier of source) {
      if (!isRecord(modifier)) continue
      modifier.values = resizeValues(arrayNumbers(modifier.values), rank)
      if (Array.isArray(modifier.secondaryValues)) {
        modifier.secondaryValues = resizeValues(arrayNumbers(modifier.secondaryValues), rank)
      }
    }
    node.modifiers = source
  })
}

function mutateSelectedNode(action: (node: JsonRecord) => void): void {
  if (selectedNodeIndex.value < 0) return
  const next = cloneRecord(props.entity)
  const source = Array.isArray(next.nodes) ? next.nodes : []
  const node = source[selectedNodeIndex.value]
  if (!isRecord(node)) return
  action(node)
  next.nodes = source
  emit('update:entity', next)
}

function resizeValues(values: number[], size: number): number[] {
  const next = values.slice(0, size)
  const fill = next.length ? next[next.length - 1] ?? 0 : 0
  while (next.length < size) next.push(fill)
  return next
}

function csv(value: unknown): string {
  return arrayNumbers(value).join(', ')
}

function parseCsv(value: string): number[] {
  if (!value.trim()) return []
  return value.split(',')
    .map(part => Number(part.trim()))
    .filter(Number.isFinite)
}

function arrayNumbers(value: unknown): number[] {
  return Array.isArray(value)
    ? value.filter((entry): entry is number => typeof entry === 'number' && Number.isFinite(entry))
    : []
}

function setTreeNumber(key: string, event: Event): void {
  const input = event.target as HTMLInputElement
  if (Number.isFinite(input.valueAsNumber)) updateTreeField(key, input.valueAsNumber)
}

function setNodeString(key: string, event: Event): void {
  updateNodeField(key, (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value)
}

function setNodeOptionalString(key: string, event: Event): void {
  const value = (event.target as HTMLInputElement).value.trim()
  updateNodeField(key, value || null)
}

function setNodeOptionalNumber(key: string, event: Event): void {
  const input = event.target as HTMLInputElement
  updateNodeField(key, input.value.trim() === '' ? null : input.valueAsNumber)
}

function setNodeNumber(key: string, event: Event): void {
  const input = event.target as HTMLInputElement
  if (Number.isFinite(input.valueAsNumber)) updateNodeField(key, input.valueAsNumber)
}

function setModifierString(index: number, key: string, event: Event): void {
  updateModifier(index, key, (event.target as HTMLInputElement | HTMLSelectElement).value)
}

function setModifierOptionalString(index: number, key: string, event: Event): void {
  const value = (event.target as HTMLInputElement).value.trim()
  updateModifier(index, key, value || null)
}

function setModifierNumber(index: number, key: string, event: Event): void {
  const input = event.target as HTMLInputElement
  if (Number.isFinite(input.valueAsNumber)) updateModifier(index, key, input.valueAsNumber)
}

function setModifierCsv(index: number, key: string, event: Event): void {
  const value = (event.target as HTMLInputElement).value
  const parsed = parseCsv(value)
  updateModifier(index, key, key === 'secondaryValues' && !value.trim() ? null : parsed)
}

function stringValue(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function numberValue(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

function cloneRecord(value: JsonRecord): JsonRecord {
  return JSON.parse(JSON.stringify(value)) as JsonRecord
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
</script>

<template>
  <div class="talent-editor" data-testid="talent-form">
    <fieldset class="tree-meta">
      <legend>Дерево</legend>
      <label><span>Tree ID</span><input :value="stringValue(entity.id)" disabled /></label>
      <label><span>Class</span><input :value="stringValue(entity.classId)" disabled /></label>
      <label><span>Max points</span><input type="number" min="1" :value="numberValue(entity.maxSpendablePoints, 1)" @input="setTreeNumber('maxSpendablePoints', $event)" /></label>
      <label><span>Version</span><input type="number" min="1" :value="numberValue(entity.version, 1)" @input="setTreeNumber('version', $event)" /></label>
    </fieldset>

    <div class="node-workspace">
      <aside class="node-catalog">
        <label>
          <span>Ветка</span>
          <select :value="selectedBranchId" @change="changeBranch">
            <option v-for="branch in branches" :key="stringValue(branch.id)" :value="stringValue(branch.id)">
              {{ stringValue(branch.id) }} · {{ stringValue(branch.name) }}
            </option>
          </select>
        </label>
        <button
          v-for="node in filteredNodes"
          :key="stringValue(node.id)"
          type="button"
          :class="{ active: selectedNodeId === stringValue(node.id) }"
          @click="selectedNodeId = stringValue(node.id)"
        >
          <b>{{ stringValue(node.id) }}</b>
          <small>T{{ numberValue(node.tier) }} · {{ stringValue(node.name) }}</small>
        </button>
      </aside>

      <section v-if="selectedNode" class="node-editor">
        <fieldset>
          <legend>Талант {{ stringValue(selectedNode.id) }}</legend>
          <label><span>Название</span><input data-testid="talent-name" :value="stringValue(selectedNode.name)" @input="setNodeString('name', $event)" /></label>
          <label><span>English</span><input :value="stringValue(selectedNode.englishName)" @input="setNodeString('englishName', $event)" /></label>
          <label>
            <span>Branch</span>
            <select data-testid="talent-branch" :value="stringValue(selectedNode.branchId)" @change="setNodeBranch">
              <option v-for="branch in branches" :key="stringValue(branch.id)" :value="stringValue(branch.id)">{{ stringValue(branch.id) }}</option>
            </select>
          </label>
          <label><span>Tier</span><input type="number" min="1" max="9" :value="numberValue(selectedNode.tier, 1)" @input="setNodeNumber('tier', $event)" /></label>
          <label><span>Required spent</span><input type="number" min="0" :value="numberValue(selectedNode.requiredSpentPoints)" @input="setNodeNumber('requiredSpentPoints', $event)" /></label>
          <label><span>Max rank</span><input data-testid="talent-max-rank" type="number" min="1" :value="numberValue(selectedNode.maxRank, 1)" @input="setMaxRank" /></label>
          <label><span>Required level</span><input type="number" min="1" :value="selectedNode.requiredLevel ?? ''" @input="setNodeOptionalNumber('requiredLevel', $event)" /></label>
          <label><span>Icon ID</span><input :value="stringValue(selectedNode.iconId)" @input="setNodeOptionalString('iconId', $event)" /></label>
          <label class="wide"><span>Описание</span><textarea :value="stringValue(selectedNode.description)" @input="setNodeString('description', $event)" /></label>
        </fieldset>

        <fieldset class="stack">
          <legend>Prerequisites</legend>
          <article v-for="(prerequisite, index) in prerequisites" :key="`${stringValue(prerequisite.talentId)}-${index}`" class="row">
            <select :value="stringValue(prerequisite.talentId)" @change="setPrerequisiteTalent(index, $event)">
              <option v-for="node in nodes.filter(candidate => stringValue(candidate.id) !== selectedNodeId)" :key="stringValue(node.id)" :value="stringValue(node.id)">
                {{ stringValue(node.id) }} · {{ stringValue(node.name) }}
              </option>
            </select>
            <input type="number" min="1" :value="numberValue(prerequisite.requiredRank, 1)" @input="setPrerequisiteRank(index, $event)" />
            <button class="danger" type="button" @click="removePrerequisite(index)">Удалить</button>
          </article>
          <button type="button" @click="addPrerequisite">+ Prerequisite</button>
        </fieldset>

        <fieldset class="stack">
          <legend>Modifiers / Rank values</legend>
          <article v-for="(modifier, index) in modifiers" :key="index" class="modifier-card">
            <div class="modifier-grid">
              <label>
                <span>Type</span>
                <select :value="stringValue(modifier.type)" @change="setModifierString(index, 'type', $event)">
                  <option v-for="type in modifierTypes" :key="type" :value="type">{{ type }}</option>
                </select>
              </label>
              <label><span>Key</span><input :value="stringValue(modifier.key)" @input="setModifierString(index, 'key', $event)" /></label>
              <label>
                <span>Runtime</span>
                <select :value="stringValue(modifier.runtimeStatus) || 'Supported'" @change="setModifierString(index, 'runtimeStatus', $event)">
                  <option value="Supported">Supported</option>
                  <option value="Deferred">Deferred</option>
                </select>
              </label>
              <label v-if="stringValue(modifier.runtimeStatus) === 'Deferred'">
                <span>Deferred owner</span>
                <select :value="stringValue(modifier.deferredOwner)" @change="setModifierString(index, 'deferredOwner', $event)">
                  <option value="COMBAT_SESSION">COMBAT_SESSION</option>
                  <option value="PARTY">PARTY</option>
                  <option value="MONSTER">MONSTER</option>
                  <option value="BOSS_ELITE">BOSS_ELITE</option>
                  <option value="EQUIPMENT">EQUIPMENT</option>
                </select>
              </label>
              <label><span>Target ID</span><input list="talent-ability-ids" :value="stringValue(modifier.targetId)" @input="setModifierOptionalString(index, 'targetId', $event)" /></label>
              <label class="wide"><span>Values by rank</span><input data-testid="talent-modifier-values" :value="csv(modifier.values)" @change="setModifierCsv(index, 'values', $event)" /></label>
              <label class="wide"><span>Secondary values</span><input :value="csv(modifier.secondaryValues)" @change="setModifierCsv(index, 'secondaryValues', $event)" /></label>
              <label><span>Chance %</span><input type="number" min="0" max="100" :value="numberValue(modifier.chancePercent, 100)" @input="setModifierNumber(index, 'chancePercent', $event)" /></label>
              <label><span>Threshold %</span><input type="number" min="0" max="100" :value="numberValue(modifier.threshold)" @input="setModifierNumber(index, 'threshold', $event)" /></label>
              <label><span>ICD sec</span><input type="number" min="0" step="0.1" :value="numberValue(modifier.internalCooldownSeconds)" @input="setModifierNumber(index, 'internalCooldownSeconds', $event)" /></label>
              <label><span>Duration sec</span><input type="number" min="0" step="0.1" :value="numberValue(modifier.durationSeconds)" @input="setModifierNumber(index, 'durationSeconds', $event)" /></label>
              <label><span>Tick sec</span><input type="number" min="0" step="0.1" :value="numberValue(modifier.tickIntervalSeconds)" @input="setModifierNumber(index, 'tickIntervalSeconds', $event)" /></label>
              <label><span>Trigger count</span><input type="number" min="0" :value="numberValue(modifier.triggerCount)" @input="setModifierNumber(index, 'triggerCount', $event)" /></label>
              <label><span>Cast time sec</span><input type="number" min="0" step="0.1" :value="numberValue(modifier.castTimeSeconds)" @input="setModifierNumber(index, 'castTimeSeconds', $event)" /></label>
              <label><span>Resource cost reduction %</span><input type="number" min="0" max="100" :value="numberValue(modifier.resourceCostReductionPercent)" @input="setModifierNumber(index, 'resourceCostReductionPercent', $event)" /></label>
            </div>
            <button class="danger" type="button" @click="removeModifier(index)">Удалить modifier</button>
          </article>
          <button type="button" @click="addModifier">+ Modifier</button>
          <p class="hint">Количество Values и Secondary Values должно совпадать с Max Rank. При смене Max Rank значения автоматически растягиваются.</p>
        </fieldset>
      </section>
    </div>

    <datalist id="talent-ability-ids">
      <option v-for="abilityId in abilityIds" :key="abilityId" :value="abilityId" />
    </datalist>
  </div>
</template>

<style scoped>
.talent-editor { display: grid; gap: var(--ui-space-3); }
fieldset { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: var(--ui-space-2); margin: 0; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); }
legend { padding: 0 var(--ui-space-1); color: var(--ui-color-primary); font-family: var(--ui-font-display); }
label { display: grid; gap: var(--ui-space-1); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
label.wide { grid-column: 1 / -1; }
input, select, textarea, button { min-height: var(--ui-touch-target); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); color: var(--ui-color-text-primary); font: inherit; }
textarea { min-height: 7rem; resize: vertical; }
.node-workspace { display: grid; grid-template-columns: minmax(13rem, 16rem) minmax(0, 1fr); gap: var(--ui-space-3); }
.node-catalog { display: grid; align-content: start; gap: var(--ui-space-1); max-height: 48rem; overflow: auto; }
.node-catalog > label { margin-bottom: var(--ui-space-2); }
.node-catalog button { display: grid; gap: .15rem; text-align: left; }
.node-catalog button.active { border-color: var(--ui-color-primary); color: var(--ui-color-primary); }
.node-catalog small { color: var(--ui-color-text-muted); }
.node-editor, .stack { display: grid; grid-template-columns: 1fr; gap: var(--ui-space-2); }
.row { display: grid; grid-template-columns: 2fr 1fr auto; gap: var(--ui-space-2); }
.modifier-card { display: grid; gap: var(--ui-space-2); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); }
.modifier-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: var(--ui-space-2); }
.danger { color: var(--ui-color-danger); border-color: var(--ui-color-danger); }
.hint { margin: 0; color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
@media (max-width: 950px) { fieldset, .modifier-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 720px) { .node-workspace { grid-template-columns: 1fr; } .node-catalog { max-height: 16rem; } fieldset, .modifier-grid, .row { grid-template-columns: 1fr; } label.wide { grid-column: auto; } }
</style>
