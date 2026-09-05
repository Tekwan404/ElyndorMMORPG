<script setup lang="ts">
type JsonRecord = Record<string, unknown>

const weaponCategories = [
  'ONE_HAND_SWORD', 'TWO_HAND_SWORD', 'AXE', 'MACE', 'SHIELD',
  'BOW', 'DAGGER', 'STAFF', 'WAND',
]
const armorCategories = ['LIGHT', 'MEDIUM', 'HEAVY']

const props = defineProps<{
  entity: JsonRecord
  resourceIds: string[]
}>()

const emit = defineEmits<{
  'update:entity': [entity: JsonRecord]
}>()

function update(key: string, value: unknown): void {
  const next = cloneRecord(props.entity)
  next[key] = value
  emit('update:entity', next)
}

function updateNested(group: string, key: string, value: unknown): void {
  const next = cloneRecord(props.entity)
  const target = isRecord(next[group]) ? next[group] as JsonRecord : {}
  target[key] = value
  next[group] = target
  emit('update:entity', next)
}

function setString(key: string, event: Event): void {
  update(key, (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value)
}

function setNestedString(group: string, key: string, event: Event): void {
  updateNested(group, key, (event.target as HTMLInputElement).value)
}

function setNestedNumber(group: string, key: string, event: Event): void {
  const input = event.target as HTMLInputElement
  if (Number.isFinite(input.valueAsNumber)) updateNested(group, key, input.valueAsNumber)
}

function toggleCategory(key: 'allowedWeaponCategories' | 'allowedArmorCategories', value: string): void {
  const current = stringArray(props.entity[key])
  update(key, current.includes(value)
    ? current.filter(entry => entry !== value)
    : [...current, value])
}

function hasCategory(key: 'allowedWeaponCategories' | 'allowedArmorCategories', value: string): boolean {
  return stringArray(props.entity[key]).includes(value)
}

function stringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === 'string') : []
}

function stringValue(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function numberValue(group: string, key: string): number {
  const source = props.entity[group]
  if (!isRecord(source)) return 0
  const value = source[key]
  return typeof value === 'number' && Number.isFinite(value) ? value : 0
}

function cloneRecord(value: JsonRecord): JsonRecord {
  return JSON.parse(JSON.stringify(value)) as JsonRecord
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
</script>

<template>
  <div class="class-editor" data-testid="class-form">
    <fieldset>
      <legend>Идентичность</legend>
      <label><span>Class ID</span><input :value="stringValue(entity.id)" disabled /></label>
      <label>
        <span>Primary Attribute</span>
        <select data-testid="class-primary-attribute" :value="stringValue(entity.primaryAttribute)" @change="setString('primaryAttribute', $event)">
          <option value="STRENGTH">STRENGTH</option>
          <option value="AGILITY">AGILITY</option>
          <option value="INTELLECT">INTELLECT</option>
        </select>
      </label>
      <label>
        <span>Resource</span>
        <select data-testid="class-resource" :value="stringValue(entity.resourceProfileId)" @change="setString('resourceProfileId', $event)">
          <option v-for="id in resourceIds" :key="id" :value="id">{{ id }}</option>
        </select>
      </label>
      <label class="wide"><span>Prototype identity</span><textarea :value="stringValue(entity.prototypeIdentity)" @input="setString('prototypeIdentity', $event)" /></label>
    </fieldset>

    <fieldset>
      <legend>Base Stats · Level 1</legend>
      <label><span>Strength</span><input type="number" min="0" step="0.1" :value="numberValue('baseStats', 'strength')" @input="setNestedNumber('baseStats', 'strength', $event)" /></label>
      <label><span>Agility</span><input type="number" min="0" step="0.1" :value="numberValue('baseStats', 'agility')" @input="setNestedNumber('baseStats', 'agility', $event)" /></label>
      <label><span>Intellect</span><input type="number" min="0" step="0.1" :value="numberValue('baseStats', 'intellect')" @input="setNestedNumber('baseStats', 'intellect', $event)" /></label>
      <label><span>Stamina</span><input type="number" min="0" step="0.1" :value="numberValue('baseStats', 'stamina')" @input="setNestedNumber('baseStats', 'stamina', $event)" /></label>
    </fieldset>

    <fieldset>
      <legend>Growth / Level</legend>
      <label><span>Strength / lvl</span><input data-testid="class-growth-strength" type="number" min="0" step="0.1" :value="numberValue('levelGrowth', 'strength')" @input="setNestedNumber('levelGrowth', 'strength', $event)" /></label>
      <label><span>Agility / lvl</span><input type="number" min="0" step="0.1" :value="numberValue('levelGrowth', 'agility')" @input="setNestedNumber('levelGrowth', 'agility', $event)" /></label>
      <label><span>Intellect / lvl</span><input type="number" min="0" step="0.1" :value="numberValue('levelGrowth', 'intellect')" @input="setNestedNumber('levelGrowth', 'intellect', $event)" /></label>
      <label><span>Stamina / lvl</span><input type="number" min="0" step="0.1" :value="numberValue('levelGrowth', 'stamina')" @input="setNestedNumber('levelGrowth', 'stamina', $event)" /></label>
    </fieldset>

    <fieldset class="stack">
      <legend>Equipment permissions</legend>
      <div>
        <b>Weapons</b>
        <div class="checks">
          <label v-for="category in weaponCategories" :key="category" class="check">
            <input type="checkbox" :checked="hasCategory('allowedWeaponCategories', category)" @change="toggleCategory('allowedWeaponCategories', category)" />
            <span>{{ category }}</span>
          </label>
        </div>
      </div>
      <div>
        <b>Armor</b>
        <div class="checks">
          <label v-for="category in armorCategories" :key="category" class="check">
            <input type="checkbox" :checked="hasCategory('allowedArmorCategories', category)" @change="toggleCategory('allowedArmorCategories', category)" />
            <span>{{ category }}</span>
          </label>
        </div>
      </div>
    </fieldset>

    <fieldset class="stack ability-rule">
      <legend>Active abilities</legend>
      <strong>Только через Talents</strong>
      <p>Class Profile не выдаёт стартовые скиллы и не открывает их по уровню. Новый активный skill появляется только через <code>UNLOCK_ABILITY</code> в дереве талантов.</p>
    </fieldset>

    <fieldset v-if="entity.combatAutoAttack" class="auto-attack">
      <legend>Combat Auto Attack</legend>
      <label><span>Interval</span><input :value="stringValue(isRecord(entity.combatAutoAttack) ? entity.combatAutoAttack.interval : '')" @input="setNestedString('combatAutoAttack', 'interval', $event)" /></label>
      <label><span>Base Damage</span><input type="number" min="0" step="0.1" :value="numberValue('combatAutoAttack', 'baseDamage')" @input="setNestedNumber('combatAutoAttack', 'baseDamage', $event)" /></label>
      <label><span>AP coefficient</span><input type="number" min="0" step="0.05" :value="numberValue('combatAutoAttack', 'attackPowerCoefficient')" @input="setNestedNumber('combatAutoAttack', 'attackPowerCoefficient', $event)" /></label>
      <label><span>Resource on hit</span><input type="number" min="0" step="0.1" :value="numberValue('combatAutoAttack', 'resourceOnHit')" @input="setNestedNumber('combatAutoAttack', 'resourceOnHit', $event)" /></label>
    </fieldset>

    <p class="hint">Новый Class ID здесь намеренно не создаётся: новый игровой класс требует согласованного resource/ability/talent/character-creation контента.</p>
  </div>
</template>

<style scoped>
.class-editor { display: grid; gap: var(--ui-space-3); }
fieldset { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: var(--ui-space-2); margin: 0; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); }
fieldset.stack { grid-template-columns: 1fr; }
legend { padding: 0 var(--ui-space-1); color: var(--ui-color-primary); font-family: var(--ui-font-display); }
label { display: grid; gap: var(--ui-space-1); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
label.wide { grid-column: 1 / -1; }
input, select, textarea, button { min-height: var(--ui-touch-target); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); color: var(--ui-color-text-primary); font: inherit; }
textarea { min-height: 6rem; resize: vertical; }
.checks { display: flex; flex-wrap: wrap; gap: var(--ui-space-2); margin-top: var(--ui-space-2); }
label.check { display: flex; align-items: center; gap: var(--ui-space-1); min-height: 2rem; padding: 0 var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-round); }
.check input { width: auto; min-height: auto; }
.ability-rule strong { color: var(--ui-color-success); }
.ability-rule p { margin: 0; color: var(--ui-color-text-muted); }
.ability-rule code { color: var(--ui-color-primary); }
.hint { margin: 0; color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
@media (max-width: 850px) { fieldset { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 620px) { fieldset { grid-template-columns: 1fr; } label.wide { grid-column: auto; } }
</style>
