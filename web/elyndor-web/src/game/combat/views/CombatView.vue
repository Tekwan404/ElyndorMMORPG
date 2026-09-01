<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'

import type { CombatEvent } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import { useCombatSessionStore } from '@/stores/combatSession'
import { UIButton, UIHealthBar } from '@/ui/components'

const emit = defineEmits<{ leave: [] }>()
const combat = useCombatSessionStore()
const now = ref(Date.now())
const timer = window.setInterval(() => (now.value = Date.now()), 250)
const snapshot = computed(() => combat.snapshot)
const abilityArt: Record<string, string | undefined> = {
  STRIKE: gameArt.warriorAbilities.strike,
  SHIELD_BASH: gameArt.warriorAbilities.shieldBash,
  PROVOKE: gameArt.warriorAbilities.provoke,
  BASTION: gameArt.warriorAbilities.bastion,
  WILD_STRIKE: gameArt.warriorAbilities.wildStrike,
  WHIRLWIND: gameArt.warriorAbilities.whirlwind,
}

function cooldownRemaining(abilityId: string): number {
  const readyAt = snapshot.value?.player.cooldowns[abilityId]
  return readyAt ? Math.max(0, (Date.parse(readyAt) - now.value) / 1_000) : 0
}

function eventText(event: CombatEvent): string {
  const definition = event.definitionId?.split('_').join(' ') ?? ''
  const enemyName = snapshot.value?.enemy.name ?? 'противником'
  switch (event.type) {
    case 'CombatStarted': return `Бой с ${enemyName} начался`
    case 'AbilityUsed': return `${definition}: способность применена`
    case 'DamageDealt': return `${definition || 'Удар'}: ${event.amount} урона`
    case 'CriticalHit': return `Критический удар: ${event.amount}`
    case 'ResourceChanged': return `${event.amount > 0 ? '+' : ''}${event.amount} Rage (${definition})`
    case 'EffectApplied': return `Эффект: ${definition}`
    case 'ActorDied': return 'Участник боя повержен'
    case 'EnemyKilled': return `${enemyName} повержен`
    case 'CombatEnded': return event.definitionId === 'Victory' ? 'Победа' : 'Бой завершён'
    default: return definition ? `${event.type}: ${definition}` : event.type
  }
}

async function leaveCombat(): Promise<void> {
  const left = await combat.leave()
  if (left) emit('leave')
}

onUnmounted(() => window.clearInterval(timer))
</script>

<template>
  <section class="combat-screen">
    <template v-if="snapshot">
      <header class="enemy-panel">
        <div>
          <p class="eyebrow">{{ snapshot.enemy.definitionId }}</p>
          <h1>{{ snapshot.enemy.name }}</h1>
        </div>
        <span class="combat-state" :data-status="snapshot.status">{{ snapshot.status }}</span>
        <UIHealthBar
          :label="`${snapshot.enemy.name} HP`"
          :value="snapshot.enemy.hp"
          :max="snapshot.enemy.maxHp"
        />
        <div v-if="snapshot.enemy.effects.length" class="effects">
          <span v-for="effect in snapshot.enemy.effects" :key="effect.id">
            {{ effect.id }}<b v-if="effect.stacks > 1">×{{ effect.stacks }}</b>
          </span>
        </div>
      </header>

      <div class="battle-field" aria-hidden="true"><span>ᚨ</span></div>

      <section class="player-panel">
        <div class="player-bars">
          <UIHealthBar label="Health" :value="snapshot.player.hp" :max="snapshot.player.maxHp" />
          <UIHealthBar
            label="Rage"
            tone="rage"
            :value="snapshot.player.resource"
            :max="snapshot.player.maxResource"
          />
        </div>
        <div class="abilities" aria-label="Способности Warrior">
          <button
            v-for="ability in snapshot.player.abilities"
            :key="ability.id"
            class="ability"
            type="button"
            :disabled="
              !combat.isActive
              || combat.pending
              || cooldownRemaining(ability.id) > 0
              || snapshot.player.resource < ability.resourceCost
            "
            @click="combat.useAbility(ability.id)"
          >
            <span class="ability__icon">
              <img v-if="abilityArt[ability.id]" :src="abilityArt[ability.id]" alt="" />
              <b v-else>{{ ability.id.slice(0, 2) }}</b>
              <i v-if="cooldownRemaining(ability.id) > 0">
                {{ Math.ceil(cooldownRemaining(ability.id)) }}
              </i>
              <em v-if="ability.resourceCost > 0">{{ ability.resourceCost }}</em>
            </span>
            <small>{{ ability.id.split('_').join(' ') }}</small>
          </button>
        </div>
        <div class="controls">
          <UIButton
            variant="secondary"
            :disabled="!combat.isActive || combat.pending"
            @click="combat.toggleAutoAttack"
          >
            {{ snapshot.player.autoAttackEnabled ? 'Остановить автоатаку' : 'Включить автоатаку' }}
          </UIButton>
          <UIButton variant="ghost" :disabled="combat.pending" @click="leaveCombat">
            Покинуть бой
          </UIButton>
        </div>
      </section>

      <section class="combat-log" aria-live="polite">
        <b>Combat log</b>
        <ol>
          <li v-for="event in combat.events.slice(-8).reverse()" :key="event.sequence">
            <span>#{{ event.sequence }}</span>{{ eventText(event) }}
          </li>
        </ol>
      </section>
      <p v-if="combat.errorCode" class="error">{{ combat.errorCode }}</p>
    </template>

    <div v-else class="combat-missing">
      <h1>Бой прерван</h1>
      <p>Активная серверная сессия не найдена.</p>
      <UIButton variant="secondary" @click="emit('leave')">Вернуться в мир</UIButton>
    </div>
  </section>
</template>

<style scoped>
.combat-screen { display: grid; min-height: 100%; align-content: start; background: var(--ui-color-background); }
.combat-missing { display: grid; min-height: 100%; place-content: center; justify-items: start; gap: var(--ui-space-3); padding: var(--ui-space-6); background: linear-gradient(rgb(7 9 19 / 55%), rgb(7 9 19 / 94%)), url('@/assets/world/forest.jpg') center/cover; }
.combat-missing h1, .combat-missing p { margin: 0; }
.combat-missing p { color: var(--ui-color-text-secondary); }
.eyebrow { margin: 0; color: var(--ui-color-primary); font-size: var(--ui-font-size-xs); letter-spacing: .12em; }
.enemy-panel, .player-panel, .combat-log { padding: var(--ui-space-3) var(--ui-space-4); border-bottom: 1px solid var(--ui-color-border); background: rgb(15 18 34 / 94%); }
.enemy-panel { display: grid; grid-template-columns: 1fr auto; gap: var(--ui-space-2) var(--ui-space-3); }
.enemy-panel h1 { margin: 0; font: var(--ui-font-weight-bold) var(--ui-font-size-lg) var(--ui-font-display); }
.enemy-panel > :deep(.ui-bar) { grid-column: 1 / -1; }
.combat-state { align-self: center; color: var(--ui-color-warning); font-size: var(--ui-font-size-xs); text-transform: uppercase; }
.combat-state[data-status='Victory'] { color: var(--ui-color-success); }
.combat-state[data-status='Defeat'] { color: var(--ui-color-danger); }
.battle-field { display: grid; min-height: 7rem; place-items: center; background: radial-gradient(circle, rgb(92 110 255 / 20%), transparent 52%), linear-gradient(rgb(7 9 19 / 20%), rgb(7 9 19 / 75%)), url('@/assets/world/forest.jpg') center/cover; }
.battle-field span { color: var(--ui-color-primary); font-size: 3rem; opacity: .75; text-shadow: var(--ui-glow-primary); }
.player-panel { display: grid; gap: var(--ui-space-3); }
.player-bars { display: grid; gap: var(--ui-space-2); }
.abilities { display: grid; grid-template-columns: repeat(auto-fit, minmax(3.75rem, 1fr)); gap: var(--ui-space-2); }
.ability { display: grid; min-width: 0; justify-items: center; gap: var(--ui-space-1); padding: 0; border: 0; background: transparent; color: var(--ui-color-text-secondary); font: inherit; }
.ability:disabled { filter: grayscale(.8); opacity: .48; }
.ability__icon { position: relative; display: grid; width: 3.25rem; height: 3.25rem; place-items: center; overflow: hidden; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); }
.ability__icon img { width: 100%; height: 100%; object-fit: cover; }
.ability__icon i { position: absolute; inset: 0; display: grid; place-items: center; background: rgb(2 4 12 / 72%); color: white; font-style: normal; font-weight: 700; }
.ability__icon em { position: absolute; right: 2px; bottom: 1px; color: var(--ui-color-primary); font-size: .7rem; font-style: normal; font-weight: 800; text-shadow: 0 1px 2px black; }
.ability small { width: 100%; overflow: hidden; font-size: .62rem; text-overflow: ellipsis; white-space: nowrap; }
.controls { display: flex; flex-wrap: wrap; gap: var(--ui-space-2); }
.effects { grid-column: 1 / -1; display: flex; flex-wrap: wrap; gap: var(--ui-space-1); }
.effects span { padding: 2px 6px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-round); color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-xs); }
.combat-log { border-bottom: 0; }
.combat-log ol { display: grid; max-height: 9rem; gap: var(--ui-space-1); margin: var(--ui-space-2) 0 0; padding: 0; overflow-y: auto; list-style: none; }
.combat-log li { display: flex; gap: var(--ui-space-2); color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-xs); }
.combat-log li span { color: var(--ui-color-text-muted); font-variant-numeric: tabular-nums; }
.error { margin: var(--ui-space-2) var(--ui-space-4); color: var(--ui-color-danger); font-size: var(--ui-font-size-xs); }
@media (max-width: 340px) { .enemy-panel, .player-panel, .combat-log { padding-inline: var(--ui-space-3); } .ability__icon { width: 2.9rem; height: 2.9rem; } }
</style>
