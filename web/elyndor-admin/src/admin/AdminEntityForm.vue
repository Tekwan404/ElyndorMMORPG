<script setup lang="ts">
import { computed, ref } from 'vue'

type JsonRecord = Record<string, unknown>
type JsonPath = Array<string | number>

const props = withDefaults(defineProps<{
  sectionKey: string
  entity: JsonRecord
  lootTableIds?: string[]
  aiProfileIds?: string[]
  abilityIds?: string[]
  classIds?: string[]
  setIds?: string[]
}>(), {
  lootTableIds: () => [],
  aiProfileIds: () => [],
  abilityIds: () => [],
  classIds: () => [],
  setIds: () => [],
})

const itemModifierDefinitions = [
  { key: 'strength', label: 'Strength', path: ['stats', 'strength'] as JsonPath, suffix: '' },
  { key: 'agility', label: 'Agility', path: ['stats', 'agility'] as JsonPath, suffix: '' },
  { key: 'intellect', label: 'Intellect', path: ['stats', 'intellect'] as JsonPath, suffix: '' },
  { key: 'stamina', label: 'Stamina', path: ['stats', 'stamina'] as JsonPath, suffix: '' },
  { key: 'maxHpFlat', label: 'Max HP', path: ['maxHpFlat'] as JsonPath, suffix: '' },
  { key: 'attackPowerFlat', label: 'Attack Power', path: ['attackPowerFlat'] as JsonPath, suffix: '' },
  { key: 'spellPowerFlat', label: 'Spell Power', path: ['spellPowerFlat'] as JsonPath, suffix: '' },
  { key: 'criticalChancePercent', label: 'Critical Chance', path: ['criticalChancePercent'] as JsonPath, suffix: '%' },
  { key: 'criticalDamagePercent', label: 'Critical Damage', path: ['criticalDamagePercent'] as JsonPath, suffix: '%' },
  { key: 'accuracyPercent', label: 'Accuracy', path: ['accuracyPercent'] as JsonPath, suffix: '%' },
  { key: 'attackSpeedPercent', label: 'Attack Speed', path: ['attackSpeedPercent'] as JsonPath, suffix: '%' },
  { key: 'armorFlat', label: 'Armor', path: ['armorFlat'] as JsonPath, suffix: '' },
  { key: 'magicResistanceFlat', label: 'Magic Resistance', path: ['magicResistanceFlat'] as JsonPath, suffix: '' },
  { key: 'dodgePercent', label: 'Dodge', path: ['dodgePercent'] as JsonPath, suffix: '%' },
  { key: 'armorPenetrationPercent', label: 'Armor Penetration', path: ['armorPenetrationPercent'] as JsonPath, suffix: '%' },
  { key: 'magicPenetrationPercent', label: 'Magic Penetration', path: ['magicPenetrationPercent'] as JsonPath, suffix: '%' },
  { key: 'maxResourceFlat', label: 'Max Resource', path: ['maxResourceFlat'] as JsonPath, suffix: '' },
] as const

const newItemModifierKey = ref('')

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
  if (['Weapon', 'MainHand', 'OffHand'].includes(slot)) {
    next.weaponCategory = typeof next.weaponCategory === 'string' && next.weaponCategory
      ? next.weaponCategory
      : 'ONE_HAND_SWORD'
    next.armorCategory = null
    if (typeof next.weaponBaseAttackIntervalSeconds !== 'number' || next.weaponBaseAttackIntervalSeconds <= 0) {
      next.weaponBaseAttackIntervalSeconds = 2.5
    }
  } else if (['Head', 'Chest', 'Hands', 'Legs', 'Boots', 'Feet'].includes(slot)) {
    next.weaponCategory = null
    next.armorCategory = typeof next.armorCategory === 'string' && next.armorCategory
      ? next.armorCategory
      : 'LIGHT'
    next.weaponBaseAttackIntervalSeconds = null
  } else {
    next.weaponCategory = null
    next.armorCategory = null
    next.weaponBaseAttackIntervalSeconds = null
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
    if (typeof next.slot !== 'string') next.slot = 'Amulet'
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
    next.maxHpFlat = 0
    next.attackPowerFlat = 0
    next.spellPowerFlat = 0
    next.criticalChancePercent = 0
    next.criticalDamagePercent = 0
    next.accuracyPercent = 0
    next.armorFlat = 0
    next.magicResistanceFlat = 0
    next.armorPenetrationPercent = 0
    next.magicPenetrationPercent = 0
    next.maxResourceFlat = 0
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

function activeItemModifiers() {
  return itemModifierDefinitions.filter(definition => numberValue(definition.path) !== 0)
}

function availableItemModifiers() {
  return itemModifierDefinitions.filter(definition => numberValue(definition.path) === 0)
}

function addItemModifier(): void {
  const definition = itemModifierDefinitions.find(candidate => candidate.key === newItemModifierKey.value)
  if (!definition) return
  update(definition.path, 1)
  newItemModifierKey.value = ''
}

function removeItemModifier(path: JsonPath): void {
  update(path, 0)
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
      <label>
        <span>Rarity</span>
        <select :value="text(['rarity'])" @change="setString(['rarity'], $event)">
          <option v-for="rarity in ['Common','Uncommon','Rare','Epic','Legendary','Unique']" :key="rarity" :value="rarity">{{ rarity }}</option>
        </select>
      </label>
      <label><span>Required level</span><input type="number" min="1" :value="numberValue(['requiredLevel'])" @input="setNumber(['requiredLevel'], $event)" /></label>
      <label><span>Max stack</span><input type="number" min="1" :value="numberValue(['maxStack'])" @input="setNumber(['maxStack'], $event)" /></label>
      <label v-if="text(['type']) === 'Equipment'">
        <span>Slot</span>
        <select data-testid="item-slot" :value="text(['slot'])" @change="setItemSlot">
          <optgroup label="Canonical">
            <option value="MainHand">Main Hand</option>
            <option value="OffHand">Off Hand</option>
            <option value="Head">Head</option>
            <option value="Chest">Chest</option>
            <option value="Hands">Hands</option>
            <option value="Legs">Legs</option>
            <option value="Feet">Feet</option>
            <option value="Cloak">Cloak</option>
            <option value="Amulet">Amulet</option>
            <option value="Ring1">Ring 1</option>
            <option value="Ring2">Ring 2</option>
          </optgroup>
          <optgroup label="Legacy content">
            <option value="Weapon">Weapon (legacy)</option>
            <option value="Boots">Boots (legacy)</option>
            <option value="Accessory">Accessory (legacy)</option>
          </optgroup>
        </select>
      </label>
      <label v-if="text(['type']) === 'Equipment' && ['Weapon','MainHand','OffHand'].includes(text(['slot']))">
        <span>Weapon category</span>
        <select data-testid="item-weapon-category" :value="text(['weaponCategory'])" @change="setOptionalSelection(['weaponCategory'], $event)">
          <option v-for="category in ['ONE_HAND_SWORD','TWO_HAND_SWORD','AXE','MACE','SHIELD','BOW','DAGGER','STAFF','WAND']" :key="category" :value="category">{{ category }}</option>
        </select>
      </label>
      <label v-if="text(['type']) === 'Equipment' && ['Head','Chest','Hands','Legs','Boots','Feet'].includes(text(['slot']))">
        <span>Armor category</span>
        <select data-testid="item-armor-category" :value="text(['armorCategory'])" @change="setOptionalSelection(['armorCategory'], $event)">
          <option value="LIGHT">LIGHT</option>
          <option value="MEDIUM">MEDIUM</option>
          <option value="HEAVY">HEAVY</option>
        </select>
      </label>
      <label v-if="text(['type']) === 'Equipment' && ['Weapon','MainHand','OffHand'].includes(text(['slot']))">
        <span>Base attack interval, sec</span>
        <input
          type="number"
          min="0.1"
          step="0.1"
          :value="numberValue(['weaponBaseAttackIntervalSeconds'])"
          @input="setNumber(['weaponBaseAttackIntervalSeconds'], $event)"
        />
      </label>
      <label v-if="text(['type']) === 'Equipment'">
        <span>Equipment Set</span>
        <select :value="text(['setId'])" @change="setOptionalSelection(['setId'], $event)">
          <option value="">— no set —</option>
          <option v-for="id in relationOptions(setIds, text(['setId']))" :key="id" :value="id">{{ id }}</option>
        </select>
      </label>
      <label><span>Icon ID</span><input :value="text(['iconId'])" @input="setString(['iconId'], $event)" /></label>
      <label v-if="text(['type']) === 'Equipment'"><span>Appearance Profile</span><input :value="text(['appearanceProfileId'])" @input="setString(['appearanceProfileId'], $event)" /></label>
      <label class="wide"><span>Описание</span><textarea :value="text(['description'])" @input="setString(['description'], $event)" /></label>
    </fieldset>

    <fieldset v-if="text(['type']) === 'Equipment'" class="modifier-fieldset">
      <legend>Stat modifiers</legend>
      <div class="modifier-list wide">
        <div v-for="definition in activeItemModifiers()" :key="definition.key" class="modifier-row">
          <label>
            <span>{{ definition.label }}{{ definition.suffix ? ` (${definition.suffix})` : '' }}</span>
            <input
              :data-testid="definition.key === 'strength' ? 'item-strength' : undefined"
              type="number"
              step="0.1"
              :value="numberValue(definition.path)"
              @input="setNumber(definition.path, $event)"
            />
          </label>
          <button class="danger compact" type="button" @click="removeItemModifier(definition.path)">×</button>
        </div>
      </div>
      <div class="add-modifier wide">
        <select v-model="newItemModifierKey">
          <option value="">Добавить характеристику…</option>
          <option v-for="definition in availableItemModifiers()" :key="definition.key" :value="definition.key">
            {{ definition.label }}{{ definition.suffix ? ` (${definition.suffix})` : '' }}
          </option>
        </select>
        <button type="button" :disabled="!newItemModifierKey" @click="addItemModifier">+ Add modifier</button>
      </div>
      <p class="wide relation-hint">Нулевые модификаторы скрываются. Все значения проходят через authoritative equipment stat pipeline.</p>
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
.modifier-fieldset { grid-template-columns: 1fr; }
.modifier-list { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--ui-space-2); }
.modifier-row { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: end; gap: var(--ui-space-1); }
.modifier-row .compact { width: var(--ui-touch-target); min-height: var(--ui-touch-target); }
.add-modifier { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: var(--ui-space-2); }
@media (max-width: 900px) { fieldset { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 560px) { fieldset, .modifier-list, .add-modifier { grid-template-columns: 1fr; } label.wide { grid-column: auto; } }
</style>
