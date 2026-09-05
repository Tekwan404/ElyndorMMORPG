<script setup lang="ts">
import { computed, ref } from 'vue'
import type { MonsterTemplate } from '@/admin/entityTemplates'

interface LocationOption {
  id: string
  name: string
  dangerLevel: string
}

export interface MonsterCreateRequest {
  id: string
  name: string
  template: MonsterTemplate
  rank: 'Normal' | 'Elite' | 'Boss'
  createLootTable: boolean
  locationIds: string[]
  encounterWeight: number
}

const props = defineProps<{
  locations: LocationOption[]
}>()

const emit = defineEmits<{
  create: [request: MonsterCreateRequest]
  cancel: []
}>()

const id = ref('')
const name = ref('')
const template = ref<MonsterTemplate>('NormalMelee')
const rank = ref<'Normal' | 'Elite' | 'Boss'>('Normal')
const createLootTable = ref(true)
const locationIds = ref<string[]>([])
const encounterWeight = ref(1)

const hostileLocations = computed(() => props.locations.filter(location => location.dangerLevel !== 'SAFE'))

function applyTemplate(): void {
  if (template.value === 'EliteMelee') rank.value = 'Elite'
  else if (template.value === 'Boss') rank.value = 'Boss'
  else rank.value = 'Normal'
}

function toggleLocation(locationId: string): void {
  locationIds.value = locationIds.value.includes(locationId)
    ? locationIds.value.filter(id => id !== locationId)
    : [...locationIds.value, locationId]
}

function submit(): void {
  emit('create', {
    id: id.value,
    name: name.value,
    template: template.value,
    rank: rank.value,
    createLootTable: createLootTable.value,
    locationIds: locationIds.value,
    encounterWeight: encounterWeight.value,
  })
}
</script>

<template>
  <form class="monster-wizard" data-testid="monster-create-wizard" @submit.prevent="submit">
    <header>
      <div>
        <small>MONSTER WORKFLOW</small>
        <b>Создать готового моба</b>
      </div>
      <button type="button" @click="emit('cancel')">×</button>
    </header>

    <fieldset>
      <legend>1 · Основное</legend>
      <label><span>ID</span><input v-model="id" placeholder="DIRE_WOLF" autocomplete="off" /></label>
      <label><span>Название</span><input v-model="name" placeholder="Лютоволк" autocomplete="off" /></label>
      <label>
        <span>Template</span>
        <select v-model="template" @change="applyTemplate">
          <option value="NormalMelee">Normal Melee</option>
          <option value="NormalCaster">Normal Caster</option>
          <option value="EliteMelee">Elite Melee</option>
          <option value="Boss">Boss</option>
        </select>
      </label>
      <label>
        <span>Rank</span>
        <select v-model="rank">
          <option value="Normal">Normal</option>
          <option value="Elite">Elite</option>
          <option value="Boss">Boss</option>
        </select>
      </label>
    </fieldset>

    <fieldset>
      <legend>2 · Loot</legend>
      <label class="check wide">
        <input v-model="createLootTable" type="checkbox" />
        <span>Создать и сразу привязать <code>{{ id.trim().toUpperCase() || 'MONSTER' }}_LOOT</code></span>
      </label>
      <p class="hint wide">Loot Table создаётся в том же локальном draft. После создания добавь предметы — пустой loot не пройдёт Publish validation.</p>
    </fieldset>

    <fieldset>
      <legend>3 · Locations</legend>
      <label>
        <span>Encounter weight</span>
        <input v-model.number="encounterWeight" type="number" min="0.01" step="0.01" />
      </label>
      <div class="location-list wide">
        <label v-for="location in hostileLocations" :key="location.id" class="check">
          <input
            type="checkbox"
            :checked="locationIds.includes(location.id)"
            @change="toggleLocation(location.id)"
          />
          <span>{{ location.id }} · {{ location.name }}</span>
        </label>
      </div>
      <p v-if="hostileLocations.length === 0" class="hint wide">Нет доступных hostile-локаций. Моба можно привязать позже.</p>
    </fieldset>

    <fieldset>
      <legend>4 · Что будет создано</legend>
      <ul class="summary wide">
        <li>Monster + combat template</li>
        <li>AI profile <code>{{ id.trim().toUpperCase() || 'MONSTER' }}_BASIC_AI</code></li>
        <li v-if="createLootTable">Loot table <code>{{ id.trim().toUpperCase() || 'MONSTER' }}_LOOT</code></li>
        <li v-for="locationId in locationIds" :key="locationId">Encounter в {{ locationId }}</li>
      </ul>
    </fieldset>

    <footer>
      <button type="button" @click="emit('cancel')">Отмена</button>
      <button class="primary" type="submit">Create monster bundle</button>
    </footer>
  </form>
</template>

<style scoped>
.monster-wizard { display:grid; gap:var(--ui-space-3); padding:var(--ui-space-3); border:1px solid var(--ui-color-primary); border-radius:var(--ui-radius-md); background:var(--ui-color-surface-1); }
header,footer { display:flex; align-items:center; justify-content:space-between; gap:var(--ui-space-2); }
header div { display:grid; gap:.15rem; }
header small { color:var(--ui-color-primary); font-size:var(--ui-font-size-xs); }
fieldset { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:var(--ui-space-2); margin:0; padding:var(--ui-space-3); border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-sm); }
legend { padding:0 var(--ui-space-1); color:var(--ui-color-primary); }
label { display:grid; gap:var(--ui-space-1); color:var(--ui-color-text-muted); font-size:var(--ui-font-size-xs); }
label.check { display:flex; align-items:center; min-height:2rem; gap:var(--ui-space-2); }
label.check input { width:1rem; min-height:auto; }
input,select,button { min-height:var(--ui-touch-target); padding:var(--ui-space-2); border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-sm); background:var(--ui-color-surface-2); color:var(--ui-color-text-primary); font:inherit; }
.wide { grid-column:1/-1; }
.location-list { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:var(--ui-space-1); }
.summary { margin:0; padding-left:1.25rem; color:var(--ui-color-text-muted); }
.hint { margin:0; color:var(--ui-color-text-muted); font-size:var(--ui-font-size-xs); }
.primary { border-color:var(--ui-color-primary); }
@media(max-width:700px){fieldset,.location-list{grid-template-columns:1fr}.wide{grid-column:auto}}
</style>
