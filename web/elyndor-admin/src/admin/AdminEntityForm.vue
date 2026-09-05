<script setup lang="ts">
import { computed } from 'vue'

type JsonRecord = Record<string, unknown>
type JsonPath = Array<string | number>

const props = withDefaults(defineProps<{
  sectionKey: string
  entity: JsonRecord
  lootTableIds?: string[]
  aiProfileIds?: string[]
  abilityIds?: string[]
  classIds?: string[]
}>(), {
  lootTableIds: () => [],
  aiProfileIds: () => [],
  abilityIds: () => [],
  classIds: () => [],
})

const emit = defineEmits<{
  'update:entity': [entity: JsonRecord]
}>()

const damageActionIndex = computed(() => {
  const actions = props.entity.actions
  if (!Array.isArray(actions)) return -1
  return actions.findIndex((action) => isRecord(action) && action.type === 'Damage')
})

const damageCoefficient = computed<{ label: string; path: JsonPath } | null>(() => {
  if (damageActionIndex.value < 0) return null
  const actions = props.entity.actions
  if (!Array.isArray(actions)) return null
  const action = actions[damageActionIndex.value]
  if (!isRecord(action)) return null

  if (typeof action.spellPowerCoefficient === 'number') {
    return {
      label: 'Spell Power coefficient',
      path: ['actions', damageActionIndex.value, 'spellPowerCoefficient'],
    }
  }
  if (typeof action.attackPowerCoefficient === 'number') {
    return {
      label: 'Attack Power coefficient',
      path: ['actions', damageActionIndex.value, 'attackPowerCoefficient'],
    }
  }
  return null
})

function read(path: JsonPath): unknown {
  let current: unknown = props.entity
  for (const segment of path) {
    if (typeof segment === 'number') {
      if (!Array.isArray(current)) return undefined
      current = current[segment]
    } else {
      if (!isRecord(current)) return undefined
      current = current[segment]
    }
  }
  return current
}

function text(path: JsonPath): string {
  const value = read(path)
  return typeof value === 'string' ? value : ''
}

function numberValue(path: JsonPath): number {
  const value = read(path)
  return typeof value === 'number' && Number.isFinite(value) ? value : 0
}

function boolValue(path: JsonPath): boolean {
  return read(path) === true
}

function setString(path: JsonPath, event: Event): void {
  const target = event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement
  update(path, target.value)
}

function setNumber(path: JsonPath, event: Event): void {
  const target = event.target as HTMLInputElement
  if (!Number.isFinite(target.valueAsNumber)) return
  update(path, target.valueAsNumber)
}

function setBoolean(path: JsonPath, event: Event): void {
  update(path, (event.target as HTMLInputElement).checked)
}

function stringArray(path: JsonPath): string[] {
  const value = read(path)
  return Array.isArray(value)
    ? value.filter((entry): entry is string => typeof entry === 'string')
    : []
}

function setOptionalSelection(path: JsonPath, event: Event): void {
  const value = (event.target as HTMLSelectElement).value
  update(path, value || null)
}

function toggleString(path: JsonPath, value: string): void {
  const current = stringArray(path)
  update(path, current.includes(value)
    ? current.filter(entry => entry !== value)
    : [...current, value])
}

function addRelation(path: JsonPath, event: Event): void {
  const select = event.target as HTMLSelectElement
  const value = select.value
  if (!value) return
  const current = stringArray(path)
  if (!current.includes(value)) update(path, [...current, value])
  select.value = ''
}

function removeRelation(path: JsonPath, value: string): void {
  update(path, stringArray(path).filter(entry => entry !== value))
}

function relationOptions(options: string[], current: string): string[] {
  return current && !options.includes(current) ? [current, ...options] : options
}

function setItemSlot(event: Event): void {
  const slot = (event.target as HTMLSelectElement).value
  const next = cloneJsonValue(props.entity) as JsonRecord
  next.slot = slot || null
  if (slot === 'Weapon') {
    next.weaponCategory = typeof next.weaponCategory === 'string' && next.weaponCategory
      ? next.weaponCategory
      : 'ONE_HAND_SWORD'
    next.armorCategory = null
  } else if (['Head', 'Chest', 'Legs', 'Boots'].includes(slot)) {
    next.weaponCategory = null
    next.armorCategory = typeof next.armorCategory === 'string' && next.armorCategory
      ? next.armorCategory
      : 'LIGHT'
  } else {
    next.weaponCategory = null
    next.armorCategory = null
  }
  emit('update:entity', next)
}

function setItemType(event: Event): void {
  const type = (event.target as HTMLSelectElement).value
  const next = cloneJsonValue(props.entity) as JsonRecord
  next.type = type

  if (type === 'Equipment') {
    next.stackable = false
    next.maxStack = 1
    if (typeof next.slot !== 'string') next.slot = 'Accessory'
    next.healAmount = 0
    next.consumableCooldownSeconds = 0
  } else {
    next.stackable = true
    next.slot = null
    next.weaponCategory = null
    next.armorCategory = null
    next.allowedClassIds = []
    next.setId = null
    next.weaponBaseAttackIntervalSeconds = null
    next.attackSpeedPercent = 0
    next.dodgePercent = 0
    next.stats = { strength: 0, agility: 0, intellect: 0, stamina: 0 }

    if (type === 'Consumable') {
      next.maxStack = typeof next.maxStack === 'number' && next.maxStack >= 2 ? next.maxStack : 20
      next.healAmount = typeof next.healAmount === 'number' && next.healAmount > 0 ? next.healAmount : 50
      next.consumableCooldownSeconds =
        typeof next.consumableCooldownSeconds === 'number' && next.consumableCooldownSeconds > 0
          ? next.consumableCooldownSeconds
          : 30
    } else {
      next.maxStack = typeof next.maxStack === 'number' && next.maxStack >= 2 ? next.maxStack : 99
      next.healAmount = 0
      next.consumableCooldownSeconds = 0
    }
  }

  emit('update:entity', next)
}

function cloneJsonValue(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(cloneJsonValue)
  }
  if (isRecord(value)) {
    const clone: JsonRecord = {}
    for (const [key, entry] of Object.entries(value)) {
      clone[key] = cloneJsonValue(entry)
    }
    return clone
  }
  return value
}

function update(path: JsonPath, value: unknown): void {
  const next = cloneJsonValue(props.entity) as JsonRecord
  let current: unknown = next

  for (let index = 0; index < path.length - 1; index++) {
    const segment = path[index]
    if (segment === undefined) return
    if (typeof segment === 'number') {
      if (!Array.isArray(current)) return
      current = current[segment]
    } else {
      if (!isRecord(current)) return
      current = current[segment]
    }
  }

  const finalSegment = path[path.length - 1]
  if (finalSegment === undefined) return
  if (typeof finalSegment === 'number') {
    if (!Array.isArray(current)) return
    current[finalSegment] = value
  } else {
    if (!isRecord(current)) return
    current[finalSegment] = value
  }

  emit('update:entity', next)
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
</script>

<template>
  <div v-if="sectionKey === 'monsters'" class="form-grid" data-testid="monster-form">
    <fieldset>
      <legend>Основное</legend>
      <label><span>Название</span><input :value="text(['displayName'])" @input="setString(['displayName'], $event)" /></label>
      <label><span>Internal name</span><input :value="text(['name'])" @input="setString(['name'], $event)" /></label>
      <label><span>Rank</span><input :value="text(['rank'])" @input="setString(['rank'], $event)" /></label>
      <label><span>Уровень</span><input type="number" min="1" :value="numberValue(['level'])" @input="setNumber(['level'], $event)" /></label>
      <label><span>HP</span><input data-testid="monster-max-hp" type="number" min="1" :value="numberValue(['maxHp'])" @input="setNumber(['maxHp'], $event)" /></label>
      <label class="wide"><span>Описание</span><textarea :value="text(['description'])" @input="setString(['description'], $event)" /></label>
    </fieldset>

    <fieldset>
      <legend>Бой</legend>
      <label><span>Attack Power</span><input type="number" :value="numberValue(['stats', 'attackPower'])" @input="setNumber(['stats', 'attackPower'], $event)" /></label>
      <label><span>Spell Power</span><input type="number" :value="numberValue(['stats', 'spellPower'])" @input="setNumber(['stats', 'spellPower'], $event)" /></label>
      <label><span>Accuracy</span><input type="number" :value="numberValue(['stats', 'accuracy'])" @input="setNumber(['stats', 'accuracy'], $event)" /></label>
      <label><span>Crit %</span><input type="number" step="0.1" :value="numberValue(['stats', 'criticalChance'])" @input="setNumber(['stats', 'criticalChance'], $event)" /></label>
      <label><span>Auto damage</span><input type="number" :value="numberValue(['autoAttackBaseDamage'])" @input="setNumber(['autoAttackBaseDamage'], $event)" /></label>
      <label><span>AP coefficient</span><input type="number" step="0.05" :value="numberValue(['autoAttackAttackPowerCoefficient'])" @input="setNumber(['autoAttackAttackPowerCoefficient'], $event)" /></label>
      <label><span>Attack interval</span><input :value="text(['autoAttackInterval'])" @input="setString(['autoAttackInterval'], $event)" /></label>
      <label><span>Armor</span><input type="number" :value="numberValue(['stats', 'armor'])" @input="setNumber(['stats', 'armor'], $event)" /></label>
      <label><span>Magic Resist</span><input type="number" :value="numberValue(['stats', 'magicResistance'])" @input="setNumber(['stats', 'magicResistance'], $event)" /></label>
      <label><span>Dodge %</span><input type="number" step="0.1" :value="numberValue(['stats', 'dodge'])" @input="setNumber(['stats', 'dodge'], $event)" /></label>
    </fieldset>

    <fieldset>
      <legend>Награды и представление</legend>
      <label><span>XP</span><input type="number" min="0" :value="numberValue(['xpReward'])" @input="setNumber(['xpReward'], $event)" /></label>
      <label><span>Gold min</span><input type="number" min="0" :value="numberValue(['goldRewardMin'])" @input="setNumber(['goldRewardMin'], $event)" /></label>
      <label><span>Gold max</span><input type="number" min="0" :value="numberValue(['goldRewardMax'])" @input="setNumber(['goldRewardMax'], $event)" /></label>
      <label>
        <span>Loot table</span>
        <select data-testid="monster-loot-table" :value="text(['lootTableId'])" @change="setOptionalSelection(['lootTableId'], $event)">
          <option value="">— none —</option>
          <option v-for="id in relationOptions(lootTableIds, text(['lootTableId']))" :key="id" :value="id">{{ id }}</option>
        </select>
      </label>
      <label>
        <span>AI profile</span>
        <select data-testid="monster-ai-profile" :value="text(['aiProfileId'])" @change="setString(['aiProfileId'], $event)">
          <option v-for="id in relationOptions(aiProfileIds, text(['aiProfileId']))" :key="id" :value="id">{{ id }}</option>
        </select>
      </label>
      <label><span>Art ID</span><input :value="text(['artId'])" @input="setString(['artId'], $event)" /></label>
    </fieldset>

    <fieldset>
      <legend>Abilities</legend>
      <div class="relation-list wide">
        <span v-for="id in stringArray(['abilityIds'])" :key="id" class="relation-chip">
          <code>{{ id }}</code>
          <button type="button" @click="removeRelation(['abilityIds'], id)">×</button>
        </span>
      </div>
      <label class="wide">
        <span>Добавить ability</span>
        <select data-testid="monster-add-ability" value="" @change="addRelation(['abilityIds'], $event)">
          <option value="">Выбери ability…</option>
          <option v-for="id in abilityIds.filter(id => !stringArray(['abilityIds']).includes(id))" :key="id" :value="id">{{ id }}</option>
        </select>
      </label>
    </fieldset>
  </div>

  <div v-else-if="sectionKey === 'abilities'" class="form-grid" data-testid="ability-form">
    <fieldset>
      <legend>Основное</legend>
      <label><span>Название</span><input :value="text(['displayName'])" @input="setString(['displayName'], $event)" /></label>
      <label>
        <span>Тип</span>
        <select :value="text(['type'])" @change="setString(['type'], $event)">
          <option value="Instant">Instant</option>
          <option value="Casted">Casted</option>
        </select>
      </label>
      <label><span>Target</span><input :value="text(['targetType'])" @input="setString(['targetType'], $event)" /></label>
      <label><span>School</span><input :value="text(['school'])" @input="setString(['school'], $event)" /></label>
      <label class="wide"><span>Описание</span><textarea :value="text(['description'])" @input="setString(['description'], $event)" /></label>
    </fieldset>

    <fieldset>
      <legend>Стоимость и тайминги</legend>
      <label><span>Resource cost</span><input data-testid="ability-resource-cost" type="number" min="0" :value="numberValue(['resourceCost'])" @input="setNumber(['resourceCost'], $event)" /></label>
      <label><span>Cooldown</span><input :value="text(['cooldown'])" @input="setString(['cooldown'], $event)" /></label>
      <label><span>Cast time</span><input :value="text(['castTime'])" @input="setString(['castTime'], $event)" /></label>
      <label class="check"><input type="checkbox" :checked="boolValue(['usesGlobalCooldown'])" @change="setBoolean(['usesGlobalCooldown'], $event)" /><span>Uses global cooldown</span></label>
      <label v-if="damageCoefficient">
        <span>{{ damageCoefficient.label }}</span>
        <input
          data-testid="ability-damage-coefficient"
          type="number"
          step="0.05"
          :value="numberValue(damageCoefficient.path)"
          @input="setNumber(damageCoefficient.path, $event)"
        />
      </label>
    </fieldset>
  </div>

  <div v-else-if="sectionKey === 'items'" class="form-grid" data-testid="item-form">
    <fieldset>
      <legend>Основное</legend>
      <label><span>Название</span><input :value="text(['name'])" @input="setString(['name'], $event)" /></label>
      <label>
        <span>Тип</span>
        <select data-testid="item-type" :value="text(['type'])" @change="setItemType">
          <option value="Equipment">Equipment</option>
          <option value="Material">Material</option>
          <option value="Consumable">Consumable</option>
        </select>
      </label>
      <label><span>Rarity</span><input :value="text(['rarity'])" @input="setString(['rarity'], $event)" /></label>
      <label><span>Required level</span><input type="number" min="1" :value="numberValue(['requiredLevel'])" @input="setNumber(['requiredLevel'], $event)" /></label>
      <label><span>Max stack</span><input type="number" min="1" :value="numberValue(['maxStack'])" @input="setNumber(['maxStack'], $event)" /></label>
      <label v-if="text(['type']) === 'Equipment'">
        <span>Slot</span>
        <select data-testid="item-slot" :value="text(['slot'])" @change="setItemSlot">
          <option value="Weapon">Weapon</option>
          <option value="Head">Head</option>
          <option value="Chest">Chest</option>
          <option value="Legs">Legs</option>
          <option value="Boots">Boots</option>
          <option value="Accessory">Accessory</option>
        </select>
      </label>
      <label v-if="text(['type']) === 'Equipment' && text(['slot']) === 'Weapon'">
        <span>Weapon category</span>
        <select data-testid="item-weapon-category" :value="text(['weaponCategory'])" @change="setOptionalSelection(['weaponCategory'], $event)">
          <option v-for="category in ['ONE_HAND_SWORD','TWO_HAND_SWORD','AXE','MACE','SHIELD','BOW','DAGGER','STAFF','WAND']" :key="category" :value="category">{{ category }}</option>
        </select>
      </label>
      <label v-if="text(['type']) === 'Equipment' && ['Head','Chest','Legs','Boots'].includes(text(['slot']))">
        <span>Armor category</span>
        <select data-testid="item-armor-category" :value="text(['armorCategory'])" @change="setOptionalSelection(['armorCategory'], $event)">
          <option value="LIGHT">LIGHT</option>
          <option value="MEDIUM">MEDIUM</option>
          <option value="HEAVY">HEAVY</option>
        </select>
      </label>
      <label class="wide"><span>Описание</span><textarea :value="text(['description'])" @input="setString(['description'], $event)" /></label>
    </fieldset>

    <fieldset>
      <legend>Характеристики</legend>
      <label><span>Strength</span><input data-testid="item-strength" type="number" :value="numberValue(['stats', 'strength'])" @input="setNumber(['stats', 'strength'], $event)" /></label>
      <label><span>Agility</span><input type="number" :value="numberValue(['stats', 'agility'])" @input="setNumber(['stats', 'agility'], $event)" /></label>
      <label><span>Intellect</span><input type="number" :value="numberValue(['stats', 'intellect'])" @input="setNumber(['stats', 'intellect'], $event)" /></label>
      <label><span>Stamina</span><input type="number" :value="numberValue(['stats', 'stamina'])" @input="setNumber(['stats', 'stamina'], $event)" /></label>
    </fieldset>

    <fieldset v-if="text(['type']) === 'Equipment'">
      <legend>Class restrictions</legend>
      <label v-for="id in classIds" :key="id" class="check">
        <input
          data-testid="item-class-restriction"
          type="checkbox"
          :checked="stringArray(['allowedClassIds']).includes(id)"
          @change="toggleString(['allowedClassIds'], id)"
        />
        <span>{{ id }}</span>
      </label>
      <p class="wide relation-hint">Пустой список = предмет не ограничен конкретным классом.</p>
    </fieldset>

    <fieldset>
      <legend>Экономика / расходники</legend>
      <label><span>Heal amount</span><input type="number" min="0" :value="numberValue(['healAmount'])" @input="setNumber(['healAmount'], $event)" /></label>
      <label><span>Consumable CD</span><input type="number" min="0" :value="numberValue(['consumableCooldownSeconds'])" @input="setNumber(['consumableCooldownSeconds'], $event)" /></label>
      <label><span>Buy price</span><input type="number" min="0" :value="numberValue(['buyPriceGold'])" @input="setNumber(['buyPriceGold'], $event)" /></label>
      <label><span>Sell price</span><input type="number" min="0" :value="numberValue(['sellPriceGold'])" @input="setNumber(['sellPriceGold'], $event)" /></label>
    </fieldset>
  </div>
</template>

<style scoped>
.form-grid { display: grid; gap: var(--ui-space-3); }
fieldset { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: var(--ui-space-2); margin: 0; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); }
legend { padding: 0 var(--ui-space-1); color: var(--ui-color-primary); font-family: var(--ui-font-display); }
label { display: grid; align-content: start; gap: var(--ui-space-1); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
label.wide { grid-column: 1 / -1; }
label.check { display: flex; align-items: center; min-height: var(--ui-touch-target); gap: var(--ui-space-2); }
input, select, textarea { width: 100%; min-height: var(--ui-touch-target); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); color: var(--ui-color-text-primary); font: inherit; }
textarea { min-height: 6rem; resize: vertical; }
input:focus, select:focus, textarea:focus { outline: 1px solid var(--ui-color-primary); border-color: var(--ui-color-primary); }
.check input { width: 1rem; min-height: auto; }
.relation-list { display: flex; flex-wrap: wrap; gap: var(--ui-space-1); }
.relation-chip { display: inline-flex; align-items: center; gap: var(--ui-space-1); padding: var(--ui-space-1) var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-round); }
.relation-chip button { min-height: 1.5rem; padding: 0 .35rem; border: 0; background: transparent; color: var(--ui-color-danger); cursor: pointer; }
.relation-hint { margin: 0; color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
@media (max-width: 900px) { fieldset { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 560px) { fieldset { grid-template-columns: 1fr; } label.wide { grid-column: auto; } }
</style>
