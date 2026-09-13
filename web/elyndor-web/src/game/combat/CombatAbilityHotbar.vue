<script setup lang="ts">
import type { CombatAbility } from '@/api/contracts'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName } from '@/ui/icons/icon.types'

const props = defineProps<{
  slots: Array<CombatAbility | null>
  queuedAbilityIds: string[]
  fireballStreak: number
  heatActive: boolean
  combustionActive: boolean
  abilityState: (ability: CombatAbility) => 'cooldown' | 'resource' | 'ready'
  abilityIcon: (ability: CombatAbility) => string | undefined
  abilityGlyph: (ability: CombatAbility) => GlyphName
  cooldownRemaining: (abilityId: string) => number
}>()

const emit = defineEmits<{
  use: [ability: CombatAbility]
}>()

function isQueued(abilityId: string): boolean {
  return props.queuedAbilityIds.includes(abilityId)
}

function queuePosition(abilityId: string): number {
  return props.queuedAbilityIds.indexOf(abilityId) + 1
}
</script>

<template>
  <div class="combat-ability-hotbar" aria-label="Боевые способности" data-combat-hotbar>
    <button
      v-for="(ability, index) in slots"
      :key="ability?.id ?? `empty-${index}`"
      type="button"
      class="combat-ability-hotbar__slot"
      :class="{
        'combat-ability-hotbar__slot--empty': !ability,
        'combat-ability-hotbar__slot--comet': ability?.id === 'FIRE_COMET',
        'combat-ability-hotbar__slot--queued': ability && isQueued(ability.id),
      }"
      :data-ability-slot="ability?.id ?? ''"
      :data-state="ability ? abilityState(ability) : 'empty'"
      :disabled="!ability || abilityState(ability) !== 'ready'"
      :aria-label="ability?.displayName ?? 'Пустой слот способности'"
      @click="ability && emit('use', ability)"
    >
      <span class="combat-ability-hotbar__icon">
        <img v-if="ability && abilityIcon(ability)" :src="abilityIcon(ability)" alt="" />
        <IconGenerator
          v-else-if="ability"
          :config="{ id: `ability-${ability.id}`, glyph: abilityGlyph(ability), category: 'skill' }"
        />
        <i v-else />
      </span>
      <span
        v-if="ability?.id === 'MAGE_FIREBALL'"
        class="combat-ability-hotbar__markers"
        :aria-label="`Криты Огненного шара: ${fireballStreak} из 3`"
      >
        <i v-for="marker in 3" :key="marker" :data-filled="marker <= fireballStreak" />
      </span>
      <span v-if="ability?.id === 'FIRE_COMET' && heatActive" class="combat-ability-hotbar__proc">ЖАР</span>
      <span v-if="ability?.id === 'COMBUSTION' && combustionActive" class="combat-ability-hotbar__proc">АКТ.</span>
      <span v-if="ability && isQueued(ability.id)" class="combat-ability-hotbar__queue">{{ queuePosition(ability.id) }}</span>
      <small v-if="ability">{{ ability.displayName }}</small>
      <b v-if="ability && cooldownRemaining(ability.id) > 0" class="combat-ability-hotbar__cooldown">
        {{ Math.ceil(cooldownRemaining(ability.id)) }}
      </b>
      <span v-else-if="ability && ability.resourceCost > 0" class="combat-ability-hotbar__cost">
        {{ Math.round(ability.resourceCost) }}
      </span>
    </button>
  </div>
</template>

<style scoped>
.combat-ability-hotbar { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 4px; }
.combat-ability-hotbar__slot { position: relative; display: grid; min-width: 0; min-height: 52px; place-items: center; align-content: center; gap: 2px; padding: 3px 2px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: linear-gradient(180deg, rgb(255 255 255 / 2.5%), rgb(2 5 9 / 45%)); color: var(--ui-color-text-primary); font: inherit; }
.combat-ability-hotbar__slot[data-state='ready'] { border-color: rgb(146 136 255 / 36%); box-shadow: inset 0 0 0 1px rgb(146 136 255 / 4%); }
.combat-ability-hotbar__slot[data-state='cooldown'], .combat-ability-hotbar__slot[data-state='resource'] { opacity: .46; }
.combat-ability-hotbar__slot--empty { opacity: .2; }
.combat-ability-hotbar__slot--comet { border-color: color-mix(in srgb, var(--ui-modifier-fire) 65%, var(--ui-color-border)); }
.combat-ability-hotbar__icon { display: grid; width: 38px; height: 38px; place-items: center; overflow: hidden; border: 1px solid rgb(255 255 255 / 7%); border-radius: 8px; background: rgb(3 5 10 / 90%); color: #aaa3ff; }
.combat-ability-hotbar__icon img { width: 100%; height: 100%; object-fit: cover; }
.combat-ability-hotbar__icon > i { width: 11px; height: 11px; border: 1px solid var(--ui-color-border); border-radius: 50%; }
.combat-ability-hotbar__slot > small { width: 100%; overflow: hidden; color: var(--ui-color-text-muted); font-size: .38rem; line-height: 1; text-align: center; text-overflow: ellipsis; white-space: nowrap; }
.combat-ability-hotbar__cooldown { position: absolute; inset: 3px; display: grid; place-items: center; border-radius: 8px; background: rgb(1 3 7 / 70%); color: white; font-size: .78rem; }
.combat-ability-hotbar__cost { position: absolute; right: 2px; bottom: 15px; padding: 1px 3px; border-radius: 5px; background: rgb(2 4 8 / 80%); color: #bdb7ff; font-size: .38rem; }
.combat-ability-hotbar__slot--queued { border-color: rgb(155 226 201 / 55%); box-shadow: inset 0 0 0 1px rgb(155 226 201 / 18%); }
.combat-ability-hotbar__queue { position: absolute; top: 3px; left: 3px; z-index: 5; display: grid; width: 1rem; height: 1rem; place-items: center; border-radius: 50%; background: #9be2c9; color: #07110e; font-size: .48rem; font-weight: 900; }
.combat-ability-hotbar__markers { position: absolute; top: 3px; right: 3px; display: flex; gap: 1px; }
.combat-ability-hotbar__markers i { width: 4px; height: 4px; border-radius: 50%; background: rgb(255 255 255 / 20%); }
.combat-ability-hotbar__markers i[data-filled='true'] { background: #f08b63; }
.combat-ability-hotbar__proc { position: absolute; top: 3px; right: 3px; border-radius: 4px; padding: 1px 2px; background: rgb(241 123 70 / 85%); color: #fff4de; font-size: .38rem; font-weight: 900; }
@media (max-width: 360px) { .combat-ability-hotbar__slot { min-height: 48px; } .combat-ability-hotbar__icon { width: 34px; height: 34px; } }
</style>
