<script setup lang="ts">
import type { CombatActorSnapshot } from '@/api/contracts'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName } from '@/ui/icons/icon.types'

const props = defineProps<{
  allies: CombatActorSnapshot[]
  playerActorId: string
  aggroedActorIds: string[]
  selectedFriendlyTargetActorId: string | null
  participantStatus: (actorId: string) => string
  participantGlyph: (actorId: string) => GlyphName
  roleLabel: (actor: CombatActorSnapshot) => string
  healthRatio: (actor: CombatActorSnapshot) => number
}>()

const emit = defineEmits<{
  select: [actorId: string]
}>()

function isAggroed(actorId: string): boolean {
  return props.aggroedActorIds.includes(actorId)
}

function accessibleLabel(ally: CombatActorSnapshot): string {
  const selected = ally.actorId === props.selectedFriendlyTargetActorId ? ', выбранная дружеская цель' : ''
  const aggro = isAggroed(ally.actorId) ? ', враг атакует этого союзника' : ''
  return `${ally.name}, ${props.roleLabel(ally)}, здоровье ${Math.round(props.healthRatio(ally))}%${selected}${aggro}`
}
</script>

<template>
  <section
    v-if="allies.length > 0"
    class="combat-ally-roster"
    aria-label="Состав группы в бою"
    data-combat-party-roster
  >
    <header class="combat-ally-roster__header">
      <div>
        <small>СОЮЗНИКИ В БОЮ</small>
        <strong>Союзники · {{ allies.length }}</strong>
      </div>
      <span>Выбери цель для поддержки</span>
    </header>
    <div class="combat-ally-roster__grid">
      <button
        v-for="ally in allies"
        :key="ally.actorId"
        type="button"
        class="combat-ally-roster__member"
        :class="{
          'combat-ally-roster__member--self': ally.actorId === playerActorId,
          'combat-ally-roster__member--selected': ally.actorId === selectedFriendlyTargetActorId,
          'combat-ally-roster__member--aggro': isAggroed(ally.actorId),
        }"
        :data-status="participantStatus(ally.actorId)"
        :aria-label="accessibleLabel(ally)"
        :aria-pressed="ally.actorId === selectedFriendlyTargetActorId"
        @click="emit('select', ally.actorId)"
      >
        <span class="combat-ally-roster__crest" aria-hidden="true">{{ ally.name.slice(0, 1).toUpperCase() }}</span>
        <span class="combat-ally-roster__body">
          <span class="combat-ally-roster__identity"><strong>{{ ally.actorId === playerActorId ? `Вы · ${ally.name}` : ally.name }}</strong><small>{{ roleLabel(ally) }}</small><b v-if="ally.actorId === selectedFriendlyTargetActorId">ЦЕЛЬ</b><b v-if="isAggroed(ally.actorId)">АГРО</b></span>
          <span class="combat-ally-roster__bar" aria-hidden="true"><i :style="{ width: `${healthRatio(ally)}%` }" /></span>
          <span class="combat-ally-roster__vitals">{{ Math.ceil(ally.hp) }} / {{ Math.ceil(ally.maxHp) }} · {{ participantStatus(ally.actorId) }}</span>
        </span>
        <span class="combat-ally-roster__state" aria-hidden="true"><IconGenerator :config="{ id: `combat-player-state-${ally.actorId}`, glyph: participantGlyph(ally.actorId), category: 'utility' }" /></span>
      </button>
    </div>
  </section>
</template>

<style scoped>
.combat-ally-roster { display: grid; gap: 7px; padding: 8px; border: 1px solid rgb(170 163 255 / 24%); border-radius: var(--ui-radius-md); background: rgb(8 10 20 / 55%); }
.combat-ally-roster__header { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.combat-ally-roster__header div { display: grid; gap: 2px; }
.combat-ally-roster__header small { color: var(--ui-color-gold-muted); font-size: .48rem; font-weight: 900; letter-spacing: .1em; }
.combat-ally-roster__header strong { font-family: var(--ui-font-display); font-size: var(--ui-font-size-sm); }
.combat-ally-roster__header > span { color: var(--ui-color-text-muted); font-size: .52rem; text-align: right; }
.combat-ally-roster__grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 5px; }
.combat-ally-roster__member { display: grid; grid-template-columns: 2rem minmax(0, 1fr) auto; align-items: center; gap: 6px; min-width: 0; min-height: var(--ui-touch-target); padding: 6px; border: 1px solid rgb(255 255 255 / 8%); border-radius: var(--ui-radius-sm); background: rgb(5 8 13 / 82%); color: var(--ui-color-text-primary); font: inherit; text-align: left; }
.combat-ally-roster__member--selected { border-color: rgb(205 177 113 / 72%); box-shadow: inset 0 0 0 1px rgb(205 177 113 / 18%); }
.combat-ally-roster__member--self { border-color: rgb(170 163 255 / 56%); background: linear-gradient(105deg, rgb(146 136 255 / 11%), rgb(5 8 13 / 88%)); }
.combat-ally-roster__member--aggro { border-color: rgb(205 177 113 / 72%); box-shadow: 0 0 11px rgb(205 177 113 / 15%); }
.combat-ally-roster__member[data-status='Fled'], .combat-ally-roster__member[data-status='Dead'] { opacity: .58; }
.combat-ally-roster__crest { display: grid; width: 2rem; height: 2rem; place-items: center; border: 1px solid rgb(205 177 113 / 42%); border-radius: 50%; background: radial-gradient(circle, rgb(205 177 113 / 20%), rgb(9 12 18 / 96%) 68%); color: #ecd797; font-family: var(--ui-font-display); font-size: .8rem; }
.combat-ally-roster__body { display: grid; min-width: 0; gap: 2px; }
.combat-ally-roster__identity { display: grid; min-width: 0; }
.combat-ally-roster__identity strong { overflow: hidden; font-size: var(--ui-font-size-xs); text-overflow: ellipsis; white-space: nowrap; }
.combat-ally-roster__identity small, .combat-ally-roster__vitals { overflow: hidden; color: var(--ui-color-text-muted); font-size: .5rem; text-overflow: ellipsis; white-space: nowrap; }
.combat-ally-roster__identity b { width: max-content; border-radius: var(--ui-radius-round); padding: 1px 3px; background: rgb(205 177 113 / 20%); color: var(--ui-color-gold); font-size: .4rem; letter-spacing: .05em; }
.combat-ally-roster__bar { display: block; height: 4px; overflow: hidden; border-radius: var(--ui-radius-round); background: rgb(255 255 255 / 9%); }
.combat-ally-roster__bar i { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, #5f9fe0, #9be2c9); }
.combat-ally-roster__state { display: grid; color: var(--ui-color-gold-muted); }

.combat-ally-roster--battlefield {
  position: absolute;
  z-index: 4;
  top: 8px;
  left: 8px;
  width: min(48%, 13rem);
  gap: 4px;
  padding: 4px;
  border-color: rgb(170 163 255 / 20%);
  background: rgb(5 8 13 / 72%);
  backdrop-filter: blur(4px);
}

.combat-ally-roster--battlefield .combat-ally-roster__header { display: none; }
.combat-ally-roster--battlefield .combat-ally-roster__grid { grid-template-columns: 1fr; gap: 3px; }
.combat-ally-roster--battlefield .combat-ally-roster__member { min-height: 34px; grid-template-columns: 1.45rem minmax(0, 1fr) auto; gap: 4px; padding: 3px 4px; }
.combat-ally-roster--battlefield .combat-ally-roster__crest { width: 1.45rem; height: 1.45rem; font-size: .58rem; }
.combat-ally-roster--battlefield .combat-ally-roster__identity { display: flex; align-items: center; gap: 3px; }
.combat-ally-roster--battlefield .combat-ally-roster__identity strong { font-size: .52rem; }
.combat-ally-roster--battlefield .combat-ally-roster__identity small,
.combat-ally-roster--battlefield .combat-ally-roster__vitals,
.combat-ally-roster--battlefield .combat-ally-roster__state { display: none; }
.combat-ally-roster--battlefield .combat-ally-roster__identity b { font-size: .34rem; }
.combat-ally-roster--battlefield .combat-ally-roster__bar { height: 3px; }

@media (max-width: 380px) { .combat-ally-roster__grid { grid-template-columns: 1fr; } }
</style>
