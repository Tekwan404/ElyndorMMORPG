<script setup lang="ts">
import { computed, ref } from 'vue'

type JsonRecord = Record<string, unknown>

interface MonsterOption {
  id: string
  name: string
}

const props = defineProps<{
  entity: JsonRecord
  monsters: MonsterOption[]
}>()

const emit = defineEmits<{
  'update:entity': [entity: JsonRecord]
}>()

const newMonsterId = ref('')

const encounters = computed<JsonRecord[]>(() => {
  const value = props.entity.encounters
  return Array.isArray(value) ? value.filter(isRecord) : []
})

const availableMonsters = computed(() => {
  const used = new Set(encounters.value.map(entry => stringValue(entry.monsterId)).filter(Boolean))
  return props.monsters.filter(monster => !used.has(monster.id))
})

function update(key: string, value: unknown): void {
  const next = cloneRecord(props.entity)
  next[key] = value
  emit('update:entity', next)
}

function updateEncounter(index: number, key: string, value: unknown): void {
  const next = cloneRecord(props.entity)
  const source = Array.isArray(next.encounters) ? next.encounters : []
  const entry = source[index]
  if (!isRecord(entry)) return
  entry[key] = value
  next.encounters = source
  emit('update:entity', next)
}

function addEncounter(): void {
  const id = newMonsterId.value || availableMonsters.value[0]?.id
  if (!id) return
  const next = cloneRecord(props.entity)
  const source = Array.isArray(next.encounters) ? next.encounters : []
  source.push({ monsterId: id, weight: 1 })
  next.encounters = source
  newMonsterId.value = ''
  emit('update:entity', next)
}

function removeEncounter(index: number): void {
  const next = cloneRecord(props.entity)
  const source = Array.isArray(next.encounters) ? next.encounters : []
  source.splice(index, 1)
  next.encounters = source
  emit('update:entity', next)
}

function setString(key: string, event: Event): void {
  update(key, (event.target as HTMLInputElement | HTMLSelectElement).value)
}

function setNumber(key: string, event: Event): void {
  const input = event.target as HTMLInputElement
  if (Number.isFinite(input.valueAsNumber)) update(key, input.valueAsNumber)
}

function setEncounterNumber(index: number, key: string, event: Event): void {
  const input = event.target as HTMLInputElement
  if (Number.isFinite(input.valueAsNumber)) updateEncounter(index, key, input.valueAsNumber)
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
  <div class="form-grid" data-testid="location-form">
    <fieldset>
      <legend>Локация</legend>
      <label><span>Название</span><input :value="stringValue(entity.displayName)" @input="setString('displayName', $event)" /></label>
      <label>
        <span>Danger</span>
        <select :value="stringValue(entity.dangerLevel)" @change="setString('dangerLevel', $event)">
          <option value="SAFE">SAFE</option>
          <option value="ADVENTURE">ADVENTURE</option>
          <option value="DANGEROUS">DANGEROUS</option>
        </select>
      </label>
      <label><span>Recommended level</span><input type="number" min="1" :value="numberValue(entity.recommendedLevel, 1)" @input="setNumber('recommendedLevel', $event)" /></label>
    </fieldset>

    <fieldset class="encounters">
      <legend>Обычные встречи</legend>
      <article v-for="(encounter, index) in encounters" :key="`${stringValue(encounter.monsterId)}-${index}`" class="encounter-row">
        <label>
          <span>Монстр</span>
          <select :value="stringValue(encounter.monsterId)" @change="updateEncounter(index, 'monsterId', ($event.target as HTMLSelectElement).value)">
            <option v-for="monster in monsters" :key="monster.id" :value="monster.id">{{ monster.id }} · {{ monster.name }}</option>
          </select>
        </label>
        <label>
          <span>Weight</span>
          <input data-testid="encounter-weight" type="number" min="0.01" step="0.01" :value="numberValue(encounter.weight, 1)" @input="setEncounterNumber(index, 'weight', $event)" />
        </label>
        <button class="danger" type="button" @click="removeEncounter(index)">Убрать</button>
      </article>
      <div class="add-row">
        <select v-model="newMonsterId" data-testid="location-new-monster">
          <option value="">Выбери Normal-моба…</option>
          <option v-for="monster in availableMonsters" :key="monster.id" :value="monster.id">{{ monster.id }} · {{ monster.name }}</option>
        </select>
        <button type="button" :disabled="availableMonsters.length === 0 || entity.dangerLevel === 'SAFE'" @click="addEncounter">+ Добавить встречу</button>
      </div>
      <p v-if="entity.dangerLevel === 'SAFE'" class="hint">SAFE-локации не могут содержать hostile encounters.</p>
      <p class="hint">Для моба в encounter обязательны displayName, description и artId — это проверяет сервер.</p>
    </fieldset>
  </div>
</template>

<style scoped>
.form-grid { display: grid; gap: var(--ui-space-3); }
fieldset { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: var(--ui-space-2); margin: 0; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); }
.encounters { grid-template-columns: 1fr; }
legend { padding: 0 var(--ui-space-1); color: var(--ui-color-primary); font-family: var(--ui-font-display); }
label { display: grid; gap: var(--ui-space-1); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
input, select, button { min-height: var(--ui-touch-target); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); color: var(--ui-color-text-primary); font: inherit; }
.encounter-row { display: grid; grid-template-columns: 2fr 1fr auto; align-items: end; gap: var(--ui-space-2); }
.add-row { display: grid; grid-template-columns: 1fr auto; gap: var(--ui-space-2); }
.danger { color: var(--ui-color-danger); border-color: var(--ui-color-danger); }
.hint { margin: 0; color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
@media (max-width: 680px) { fieldset, .encounter-row, .add-row { grid-template-columns: 1fr; } }
</style>
