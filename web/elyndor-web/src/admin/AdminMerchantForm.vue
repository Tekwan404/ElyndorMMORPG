<script setup lang="ts">
import { computed, ref } from 'vue'

type JsonRecord = Record<string, unknown>

interface ItemOption {
  id: string
  name: string
  buyPriceGold: number
}

interface LocationOption {
  id: string
  name: string
}

const props = defineProps<{
  entity: JsonRecord
  items: ItemOption[]
  locations: LocationOption[]
}>()

const emit = defineEmits<{
  'update:entity': [entity: JsonRecord]
}>()

const newItemId = ref('')

const itemIds = computed<string[]>(() =>
  Array.isArray(props.entity.itemIds)
    ? props.entity.itemIds.filter((value): value is string => typeof value === 'string')
    : [],
)

const availableItems = computed(() => {
  const used = new Set(itemIds.value)
  return props.items.filter(item => !used.has(item.id))
})

function update(key: string, value: unknown): void {
  const next = cloneRecord(props.entity)
  next[key] = value
  emit('update:entity', next)
}

function addItem(): void {
  const id = newItemId.value || availableItems.value[0]?.id
  if (!id || itemIds.value.includes(id)) return
  update('itemIds', [...itemIds.value, id])
  newItemId.value = ''
}

function removeItem(id: string): void {
  update('itemIds', itemIds.value.filter(candidate => candidate !== id))
}

function itemLabel(id: string): string {
  const item = props.items.find(candidate => candidate.id === id)
  return item ? `${item.id} · ${item.name} · ${item.buyPriceGold}g` : id
}

function setString(key: string, event: Event): void {
  update(key, (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value)
}

function stringValue(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function cloneRecord(value: JsonRecord): JsonRecord {
  return JSON.parse(JSON.stringify(value)) as JsonRecord
}
</script>

<template>
  <div class="form-grid" data-testid="merchant-form">
    <fieldset>
      <legend>Торговец</legend>
      <label><span>Имя</span><input :value="stringValue(entity.name)" @input="setString('name', $event)" /></label>
      <label>
        <span>Локация</span>
        <select data-testid="merchant-location" :value="stringValue(entity.locationId)" @change="setString('locationId', $event)">
          <option v-for="location in locations" :key="location.id" :value="location.id">{{ location.id }} · {{ location.name }}</option>
        </select>
      </label>
      <label class="wide"><span>Описание</span><textarea :value="stringValue(entity.description)" @input="setString('description', $event)" /></label>
    </fieldset>

    <fieldset class="inventory">
      <legend>Ассортимент</legend>
      <article v-for="id in itemIds" :key="id" class="item-row">
        <span>{{ itemLabel(id) }}</span>
        <button class="danger" type="button" @click="removeItem(id)">Убрать</button>
      </article>
      <div class="add-row">
        <select v-model="newItemId" data-testid="merchant-new-item">
          <option value="">Выбери предмет…</option>
          <option v-for="item in availableItems" :key="item.id" :value="item.id">
            {{ item.id }} · {{ item.name }} · {{ item.buyPriceGold }}g
          </option>
        </select>
        <button type="button" :disabled="availableItems.length === 0" @click="addItem">+ В ассортимент</button>
      </div>
      <p class="hint">Сервер не даст опубликовать отсутствующий предмет, неизвестную локацию или предмет с Buy Price = 0.</p>
    </fieldset>
  </div>
</template>

<style scoped>
.form-grid { display: grid; gap: var(--ui-space-3); }
fieldset { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--ui-space-2); margin: 0; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); }
.inventory { grid-template-columns: 1fr; }
legend { padding: 0 var(--ui-space-1); color: var(--ui-color-primary); font-family: var(--ui-font-display); }
label { display: grid; gap: var(--ui-space-1); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
label.wide { grid-column: 1 / -1; }
input, select, textarea, button { min-height: var(--ui-touch-target); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); color: var(--ui-color-text-primary); font: inherit; }
textarea { min-height: 6rem; resize: vertical; }
.item-row, .add-row { display: grid; grid-template-columns: 1fr auto; align-items: center; gap: var(--ui-space-2); }
.item-row { padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); }
.danger { color: var(--ui-color-danger); border-color: var(--ui-color-danger); }
.hint { margin: 0; color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
@media (max-width: 620px) { fieldset, .item-row, .add-row { grid-template-columns: 1fr; } label.wide { grid-column: auto; } }
</style>
