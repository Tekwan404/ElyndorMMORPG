<script setup lang="ts">
import { computed, ref } from 'vue'
import type { NewItemType } from '@/admin/entityTemplates'

type JsonRecord = Record<string, unknown>

interface ItemOption {
  id: string
  name: string
  stackable: boolean
}

export interface InlineLootItemRequest {
  id: string
  name: string
  itemType: NewItemType
}

const props = defineProps<{
  entity: JsonRecord
  items: ItemOption[]
}>()

const emit = defineEmits<{
  'update:entity': [entity: JsonRecord]
  'create-item': [request: InlineLootItemRequest]
}>()

const newItemId = ref('')
const inlineCreate = ref(false)
const inlineItemId = ref('')
const inlineItemName = ref('')
const inlineItemType = ref<NewItemType>('Material')

const entries = computed<JsonRecord[]>(() => {
  const value = props.entity.entries
  return Array.isArray(value) ? value.filter(isRecord) : []
})

const availableItems = computed(() => {
  const used = new Set(entries.value.map(entry => stringValue(entry.itemId)).filter(Boolean))
  return props.items.filter(item => !used.has(item.id))
})

function updateField(key: string, value: unknown): void {
  const next = cloneRecord(props.entity)
  next[key] = value
  emit('update:entity', next)
}

function updateEntry(index: number, key: string, value: unknown): void {
  const next = cloneRecord(props.entity)
  const source = Array.isArray(next.entries) ? next.entries : []
  const entry = source[index]
  if (!isRecord(entry)) return
  entry[key] = value
  next.entries = source
  emit('update:entity', next)
}

function changeItem(index: number, event: Event): void {
  const id = (event.target as HTMLSelectElement).value
  const item = props.items.find(candidate => candidate.id === id)
  const next = cloneRecord(props.entity)
  const source = Array.isArray(next.entries) ? next.entries : []
  const entry = source[index]
  if (!isRecord(entry)) return
  entry.itemId = id
  if (item && !item.stackable) {
    entry.minQuantity = 1
    entry.maxQuantity = 1
  }
  next.entries = source
  emit('update:entity', next)
}

function setDropChance(index: number, event: Event): void {
  const input = event.target as HTMLInputElement
  if (!Number.isFinite(input.valueAsNumber)) return
  updateEntry(index, 'dropChance', Math.max(0, Math.min(100, input.valueAsNumber)) / 100)
}

function addEntry(): void {
  const id = newItemId.value || availableItems.value[0]?.id
  if (!id) return
  const item = props.items.find(candidate => candidate.id === id)
  const next = cloneRecord(props.entity)
  const source = Array.isArray(next.entries) ? next.entries : []
  if (source.some(entry => isRecord(entry) && entry.itemId === id)) return
  source.push({
    itemId: id,
    dropChance: 1,
    minQuantity: 1,
    maxQuantity: item?.stackable === false ? 1 : 1,
  })
  next.entries = source
  newItemId.value = ''
  emit('update:entity', next)
}

function createInlineItem(): void {
  emit('create-item', {
    id: inlineItemId.value,
    name: inlineItemName.value,
    itemType: inlineItemType.value,
  })
  inlineItemId.value = ''
  inlineItemName.value = ''
  inlineItemType.value = 'Material'
  inlineCreate.value = false
}

function removeEntry(index: number): void {
  const next = cloneRecord(props.entity)
  const source = Array.isArray(next.entries) ? next.entries : []
  source.splice(index, 1)
  next.entries = source
  emit('update:entity', next)
}

function numberValue(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

function stringValue(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function setNumber(key: string, event: Event): void {
  const input = event.target as HTMLInputElement
  if (!Number.isFinite(input.valueAsNumber)) return
  updateField(key, input.valueAsNumber)
}

function setEntryNumber(index: number, key: string, event: Event): void {
  const input = event.target as HTMLInputElement
  if (!Number.isFinite(input.valueAsNumber)) return
  updateEntry(index, key, input.valueAsNumber)
}

function cloneRecord(value: JsonRecord): JsonRecord {
  return JSON.parse(JSON.stringify(value)) as JsonRecord
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
</script>

<template>
  <div class="form-grid" data-testid="loot-form">
    <fieldset>
      <legend>Loot Table</legend>
      <label>
        <span>ID</span>
        <input :value="stringValue(entity.id)" disabled />
      </label>
      <label>
        <span>Version</span>
        <input type="number" min="1" :value="numberValue(entity.version, 1)" @input="setNumber('version', $event)" />
      </label>
    </fieldset>

    <fieldset class="entries">
      <legend>Дроп</legend>
      <article v-for="(entry, index) in entries" :key="`${stringValue(entry.itemId)}-${index}`" class="entry-card">
        <label>
          <span>Предмет</span>
          <select :value="stringValue(entry.itemId)" @change="changeItem(index, $event)">
            <option v-for="item in items" :key="item.id" :value="item.id">{{ item.id }} · {{ item.name }}</option>
          </select>
        </label>
        <label>
          <span>Шанс, %</span>
          <input
            data-testid="loot-drop-chance"
            type="number"
            min="0.01"
            max="100"
            step="0.01"
            :value="numberValue(entry.dropChance) * 100"
            @input="setDropChance(index, $event)"
          />
        </label>
        <label>
          <span>Min qty</span>
          <input type="number" min="1" :value="numberValue(entry.minQuantity, 1)" @input="setEntryNumber(index, 'minQuantity', $event)" />
        </label>
        <label>
          <span>Max qty</span>
          <input type="number" min="1" :value="numberValue(entry.maxQuantity, 1)" @input="setEntryNumber(index, 'maxQuantity', $event)" />
        </label>
        <button class="danger" type="button" @click="removeEntry(index)">Удалить</button>
      </article>

      <div class="add-row">
        <select v-model="newItemId" data-testid="loot-new-item">
          <option value="">Выбери существующий предмет…</option>
          <option v-for="item in availableItems" :key="item.id" :value="item.id">{{ item.id }} · {{ item.name }}</option>
        </select>
        <button type="button" :disabled="availableItems.length === 0" @click="addEntry">+ Добавить</button>
        <button type="button" @click="inlineCreate = !inlineCreate">+ Create Item</button>
      </div>

      <form v-if="inlineCreate" class="inline-create" @submit.prevent="createInlineItem">
        <label><span>Item ID</span><input v-model="inlineItemId" placeholder="WOLF_FANG" /></label>
        <label><span>Название</span><input v-model="inlineItemName" placeholder="Волчий клык" /></label>
        <label>
          <span>Тип</span>
          <select v-model="inlineItemType">
            <option value="Material">Material</option>
            <option value="Equipment">Equipment</option>
            <option value="Consumable">Consumable</option>
          </select>
        </label>
        <button class="primary" type="submit">Create & add to loot</button>
      </form>

      <p v-if="entries.length === 0" class="hint">Добавь хотя бы один предмет — пустую Loot Table сервер не опубликует.</p>
    </fieldset>
  </div>
</template>

<style scoped>
.form-grid { display: grid; gap: var(--ui-space-3); }
fieldset { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--ui-space-2); margin: 0; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); }
.entries { grid-template-columns: 1fr; }
legend { padding: 0 var(--ui-space-1); color: var(--ui-color-primary); font-family: var(--ui-font-display); }
label { display: grid; gap: var(--ui-space-1); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
input, select, button { min-height: var(--ui-touch-target); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); color: var(--ui-color-text-primary); font: inherit; }
.entry-card { display: grid; grid-template-columns: minmax(12rem, 2fr) repeat(3, minmax(6rem, 1fr)) auto; align-items: end; gap: var(--ui-space-2); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); }
.add-row { display: grid; grid-template-columns: 1fr auto auto; gap: var(--ui-space-2); }
.inline-create { display:grid; grid-template-columns:2fr 2fr 1fr auto; align-items:end; gap:var(--ui-space-2); padding:var(--ui-space-2); border:1px solid var(--ui-color-primary); border-radius:var(--ui-radius-sm); }
.danger { color: var(--ui-color-danger); border-color: var(--ui-color-danger); }
.primary { border-color:var(--ui-color-primary); }
.hint { margin: 0; color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
@media (max-width: 850px) { fieldset, .entry-card, .add-row, .inline-create { grid-template-columns: 1fr; } }
</style>
