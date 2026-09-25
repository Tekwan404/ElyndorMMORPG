<script setup lang="ts">
import { onUnmounted, shallowRef } from 'vue'

import type { CombatAbility } from '@/api/contracts'
import { resolveAbilityArt } from '@/game/talents/talentArt'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName } from '@/ui/icons/icon.types'

const props = defineProps<{
  abilities: readonly CombatAbility[]
  cooldowns: Readonly<Record<string, string>>
  resource: number
  queuedAbilityIds: readonly string[]
  now: number
  disabled: boolean
}>()
const emit = defineEmits<{ use: [ability: CombatAbility] }>()

const inspectedAbility = shallowRef<CombatAbility | null>(null)
let inspectionTimer: ReturnType<typeof setTimeout> | undefined
let suppressedAbilityId: string | null = null

function remaining(abilityId: string): number {
  const readyAt = props.cooldowns[abilityId]
  return readyAt ? Math.max(0, (Date.parse(readyAt) - props.now) / 1_000) : 0
}

function state(ability: CombatAbility): 'ready' | 'cooldown' | 'resource' | 'disabled' {
  if (props.disabled) return 'disabled'
  if (remaining(ability.id) > 0) return 'cooldown'
  if (props.resource < ability.resourceCost) return 'resource'
  return 'ready'
}

function accessibleLabel(ability: CombatAbility): string {
  const currentState = state(ability)
  if (currentState === 'cooldown') return `${ability.displayName}, восстановление ${Math.ceil(remaining(ability.id))} секунд`
  if (currentState === 'resource') return `${ability.displayName}, недостаточно ресурса`
  if (currentState === 'disabled') return `${ability.displayName}, недоступно`
  return `${ability.displayName}, готово`
}

function activate(ability: CombatAbility): void {
  if (suppressedAbilityId === ability.id) {
    suppressedAbilityId = null
    return
  }
  inspectedAbility.value = null
  if (state(ability) === 'ready') emit('use', ability)
}

function inspect(ability: CombatAbility): void {
  cancelInspection()
  inspectionTimer = setTimeout(() => {
    inspectedAbility.value = ability
    suppressedAbilityId = ability.id
    inspectionTimer = undefined
  }, 500)
}

function cancelInspection(): void {
  if (inspectionTimer !== undefined) clearTimeout(inspectionTimer)
  inspectionTimer = undefined
}

function glyph(abilityId: string): GlyphName {
  if (/FIRE|FLAME|BURN|COMBUST/.test(abilityId)) return 'fire'
  if (/ICE|FROST|BLIZZARD/.test(abilityId)) return 'ice'
  if (/SHIELD|BASTION|BLOCK/.test(abilityId)) return 'shield'
  if (/BOW|ARROW|SHOT/.test(abilityId)) return 'bow'
  if (/HEAL|HOLY|LIGHT/.test(abilityId)) return 'holy'
  return 'sword'
}

onUnmounted(cancelInspection)
</script>

<template>
  <section class="skill-panel" aria-labelledby="battle-skills-title" data-skill-panel data-combat-hotbar>
    <header class="skill-panel__header">
      <h2 id="battle-skills-title">Умения</h2>
      <small>Удерживайте для описания</small>
    </header>
    <div class="skill-panel__grid" data-skill-grid data-columns="4">
      <button
        v-for="ability in abilities"
        :key="ability.id"
        type="button"
        class="skill-panel__ability"
        :data-ability-slot="ability.id"
        :data-state="state(ability)"
        :data-queued="queuedAbilityIds.includes(ability.id)"
        :aria-label="accessibleLabel(ability)"
        :aria-disabled="state(ability) !== 'ready'"
        @pointerdown="inspect(ability)"
        @pointerup="cancelInspection"
        @pointercancel="cancelInspection"
        @pointerleave="cancelInspection"
        @contextmenu.prevent
        @click="activate(ability)"
      >
        <span class="skill-panel__icon">
          <img v-if="resolveAbilityArt(ability.id, ability.iconId)" :src="resolveAbilityArt(ability.id, ability.iconId)!" alt="" loading="eager">
          <IconGenerator v-else :config="{ id: `battle-${ability.id}`, glyph: glyph(ability.id), category: 'skill' }" />
        </span>
        <strong>{{ ability.displayName }}</strong>
        <small v-if="ability.resourceCost > 0" class="skill-panel__cost">{{ Math.round(ability.resourceCost) }}</small>
        <span v-if="queuedAbilityIds.includes(ability.id)" class="skill-panel__queued">ГОТОВО</span>
        <span v-if="remaining(ability.id) > 0" class="skill-panel__cooldown" data-cooldown-overlay>
          <b>{{ Math.ceil(remaining(ability.id)) }}</b>
        </span>
      </button>
    </div>
    <aside v-if="inspectedAbility" class="skill-panel__inspection" data-ability-inspection aria-live="polite">
      <div>
        <strong>{{ inspectedAbility.displayName }}</strong>
        <p>{{ inspectedAbility.description || 'Описание способности пока не добавлено.' }}</p>
      </div>
      <button type="button" aria-label="Закрыть описание" @click="inspectedAbility = null">×</button>
    </aside>
  </section>
</template>

<style scoped>
.skill-panel { display: grid; gap: .35rem; padding: .48rem; border: 1px solid rgb(177 151 91 / 35%); border-radius: 10px; background: linear-gradient(180deg, rgb(10 13 20 / 94%), rgb(5 7 12 / 96%)); }
.skill-panel__header { display: flex; align-items: baseline; justify-content: space-between; gap: 1rem; }
.skill-panel__header h2 { margin: 0; color: #e7d8b3; font-family: var(--ui-font-display); font-size: .74rem; letter-spacing: .03em; }
.skill-panel__header small { color: #8f8a84; font-size: .48rem; }
.skill-panel__grid { display: grid; grid-template-columns: repeat(4, minmax(64px, 1fr)); gap: .32rem; }
.skill-panel__ability { position: relative; display: grid; min-width: 64px; min-height: 64px; grid-template-columns: 42px minmax(0, 1fr); align-items: center; gap: .3rem; padding: .3rem; overflow: hidden; border: 1px solid rgb(117 103 173 / 38%); border-radius: 8px; background: linear-gradient(145deg, rgb(20 21 33 / 92%), rgb(7 9 15 / 94%)); color: #e9e1d5; font: inherit; text-align: left; cursor: pointer; touch-action: manipulation; }
.skill-panel__ability[data-state='ready'] { border-color: rgb(143 119 212 / 58%); box-shadow: inset 0 0 14px rgb(111 74 178 / 10%); }
.skill-panel__ability[data-state='cooldown'], .skill-panel__ability[data-state='resource'], .skill-panel__ability[data-state='disabled'] { filter: saturate(.32) brightness(.64); }
.skill-panel__ability:focus-visible { outline: 2px solid #e5ca79; outline-offset: 2px; }
.skill-panel__icon { display: grid; width: 42px; height: 42px; place-items: center; overflow: hidden; border: 1px solid rgb(132 104 203 / 35%); border-radius: 50%; background: #060810; }
.skill-panel__icon img, .skill-panel__icon :deep(.icon-generator) { width: 100%; height: 100%; object-fit: cover; }
.skill-panel__ability > strong { display: -webkit-box; overflow: hidden; font-size: clamp(.48rem, 2vw, .6rem); line-height: 1.15; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
.skill-panel__cost { position: absolute; right: .22rem; bottom: .18rem; padding: .06rem .18rem; border-radius: 4px; background: rgb(1 3 7 / 84%); color: #b9a4f0; font-size: .46rem; }
.skill-panel__queued { position: absolute; top: .12rem; left: .2rem; color: #83deb9; font-size: .38rem; font-weight: 900; }
.skill-panel__cooldown { position: absolute; inset: 0; display: grid; place-items: center; background: rgb(3 5 10 / 52%); }
.skill-panel__cooldown b { display: grid; width: 2rem; height: 2rem; place-items: center; border: 1px solid rgb(255 255 255 / 25%); border-radius: 50%; background: rgb(2 4 8 / 82%); color: #fff; font-size: .82rem; }
.skill-panel__inspection { display: flex; align-items: start; justify-content: space-between; gap: .65rem; padding: .55rem; border: 1px solid rgb(135 111 204 / 40%); border-radius: 7px; background: #0b0d17; }
.skill-panel__inspection strong { font-size: .68rem; }
.skill-panel__inspection p { margin: .18rem 0 0; color: #aaa4a0; font-size: .58rem; line-height: 1.4; }
.skill-panel__inspection button { width: 32px; height: 32px; flex: none; border: 1px solid rgb(255 255 255 / 18%); border-radius: 6px; background: transparent; color: #e7dfd2; }

@media (max-width: 390px) {
  .skill-panel__grid { gap: .24rem; }
  .skill-panel__ability { grid-template-columns: 1fr; place-items: center; padding: .2rem; text-align: center; }
  .skill-panel__icon { width: 38px; height: 38px; }
  .skill-panel__ability > strong { width: 100%; font-size: .44rem; white-space: nowrap; text-overflow: ellipsis; }
}
</style>
