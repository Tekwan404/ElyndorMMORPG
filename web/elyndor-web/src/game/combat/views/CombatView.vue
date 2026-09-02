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

type LogSide = 'player' | 'enemy' | 'system'
interface CombatLogEntry { key: number; side: LogSide; actor: string; text: string; sequence: number }

const monsterPresentation: Record<string, { name: string; level: number; art: string }> = {
  WOLF: { name: 'Волк', level: 3, art: gameArt.monsters.wolf },
  FOREST_BOAR: { name: 'Лесной кабан', level: 2, art: gameArt.monsters.forestBoar },
  GIANT_SPIDER: { name: 'Гигантский паук', level: 2, art: gameArt.monsters.giantSpider },
}

const abilityPresentation: Record<string, { name: string; art?: string }> = {
  STRIKE: { name: 'Удар', art: gameArt.warriorAbilities.strike },
  HEAVY_BLOW: { name: 'Тяжёлый удар' },
  SHIELD_BASH: { name: 'Удар щитом', art: gameArt.warriorAbilities.shieldBash },
  PROVOKE: { name: 'Провокация', art: gameArt.warriorAbilities.provoke },
  BATTLE_FOCUS: { name: 'Боевой фокус' },
  BATTLE_SHOUT: { name: 'Боевой клич' },
  BASTION: { name: 'Бастион', art: gameArt.warriorAbilities.bastion },
  WILD_STRIKE: { name: 'Дикий удар', art: gameArt.warriorAbilities.wildStrike },
  WHIRLWIND: { name: 'Вихрь', art: gameArt.warriorAbilities.whirlwind },
  BERSERK: { name: 'Берсерк' },
}

const abilityOrder: Record<string, number> = {
  STRIKE: 10,
  HEAVY_BLOW: 20,
  SHIELD_BASH: 30,
  PROVOKE: 40,
  BATTLE_FOCUS: 50,
  BATTLE_SHOUT: 60,
  WILD_STRIKE: 100,
  WHIRLWIND: 110,
  BASTION: 120,
  BERSERK: 130,
}

const enemyPresentation = computed(() => {
  const enemy = snapshot.value?.enemy
  if (!enemy) return null
  return monsterPresentation[enemy.definitionId] ?? {
    name: enemy.name,
    level: 1,
    art: gameArt.monsters.wolf,
  }
})

const displayAbilities = computed(() => {
  const abilities = snapshot.value?.player.abilities ?? []
  return [...abilities].sort((left, right) =>
    (abilityOrder[left.id] ?? 999) - (abilityOrder[right.id] ?? 999)
    || left.id.localeCompare(right.id),
  )
})

const logEntries = computed<CombatLogEntry[]>(() => {
  const events = combat.events.slice(-32)
  const entries: CombatLogEntry[] = []
  for (let index = 0; index < events.length; index += 1) {
    const event = events[index]!

    if (['AbilityStarted', 'AbilityCompleted', 'CriticalHit'].includes(event.type)) continue
    if (event.type === 'ResourceChanged' && event.amount === 0) continue

    const previous = index > 0 ? events[index - 1] : undefined
    const critical = event.type === 'DamageDealt'
      && previous?.type === 'CriticalHit'
      && previous.sourceActorId === event.sourceActorId
      && previous.targetActorId === event.targetActorId
      && previous.amount === event.amount

    // AbilityUsed is emitted after the kernel outcome. Keep it only when there was no
    // visible outcome (for example a miss/dodge), otherwise it would duplicate the hit.
    if (event.type === 'AbilityUsed') {
      const recentOutcome = events.slice(Math.max(0, index - 6), index).some((candidate) =>
        candidate.definitionId === event.definitionId
        && candidate.sourceActorId === event.sourceActorId
        && ['DamageDealt', 'EffectApplied', 'HealingApplied', 'ResourceChanged', 'TauntApplied'].includes(candidate.type)
        && !(candidate.type === 'ResourceChanged' && candidate.amount === 0),
      )
      if (recentOutcome) continue
    }

    const side = eventSide(event)
    entries.push({
      key: event.sequence,
      side,
      actor: actorLabel(side),
      text: eventText(event, critical),
      sequence: event.sequence,
    })
  }
  return entries.slice(-10).reverse()
})

function cooldownRemaining(abilityId: string): number {
  const readyAt = snapshot.value?.player.cooldowns[abilityId]
  return readyAt ? Math.max(0, (Date.parse(readyAt) - now.value) / 1_000) : 0
}

function abilityName(abilityId: string | null | undefined): string {
  if (!abilityId) return ''
  if (abilityId === 'AUTO_ATTACK') return 'Автоатака'
  if (abilityId === 'BITE') return 'Укус'
  if (abilityId === 'DIRECT_DAMAGE_TAKEN') return 'Получение урона'
  return abilityPresentation[abilityId]?.name ?? abilityId.split('_').join(' ')
}

function abilityShortName(abilityId: string): string {
  const name = abilityName(abilityId)
  return name.split(' ').map((word) => word[0]).join('').slice(0, 2).toUpperCase()
}

function abilityState(ability: { id: string; resourceCost: number; cooldownSeconds: number }): string {
  const remaining = cooldownRemaining(ability.id)
  if (remaining > 0) return `${Math.ceil(remaining)}с`
  if ((snapshot.value?.player.resource ?? 0) < ability.resourceCost) return `Нужно ${ability.resourceCost}`
  return ability.resourceCost > 0 ? `${ability.resourceCost} яр.` : 'Бесплатно'
}

function abilityCaption(ability: { resourceCost: number; cooldownSeconds: number }): string {
  const cooldown = ability.cooldownSeconds > 0 ? `КД ${ability.cooldownSeconds}с` : 'без КД'
  const resource = ability.resourceCost > 0 ? `${ability.resourceCost} ярости` : '0 ярости'
  return `${resource} · ${cooldown}`
}

function eventSide(event: CombatEvent): LogSide {
  const current = snapshot.value
  if (!current || ['CombatStarted', 'CombatEnded', 'ActorDied', 'EnemyKilled'].includes(event.type)) return 'system'

  const actorId = event.type === 'DamageDealt' || event.type === 'CriticalHit' || event.type === 'HealingApplied'
    ? (event.sourceActorId ?? event.actorId)
    : event.actorId
  if (actorId === current.player.actorId) return 'player'
  if (actorId === current.enemy.actorId) return 'enemy'
  return 'system'
}

function actorLabel(side: LogSide): string {
  if (side === 'player') return 'ВЫ'
  if (side === 'enemy') return enemyPresentation.value?.name.toUpperCase() ?? 'ВРАГ'
  return 'СИСТЕМА'
}

function eventText(event: CombatEvent, critical = false): string {
  const definition = abilityName(event.definitionId)
  const enemyName = enemyPresentation.value?.name ?? snapshot.value?.enemy.name ?? 'Противник'
  const actorIsEnemy = event.actorId === snapshot.value?.enemy.actorId

  switch (event.type) {
    case 'CombatStarted': return `Бой с ${enemyName} начался`
    case 'AutoAttackStarted': return 'Автоатака включена'
    case 'AutoAttackStopped': return 'Автоатака остановлена'
    case 'AbilityUsed': return `«${definition}» не нанесла урона`
    case 'DamageDealt': return `${definition || (eventSide(event) === 'enemy' ? 'Атака' : 'Удар')} · ${event.amount} урона${critical ? ' · КРИТ!' : ''}`
    case 'ResourceChanged': return `${event.amount > 0 ? '+' : ''}${event.amount} ярости${definition ? ` · ${definition}` : ''}`
    case 'EffectApplied': return `Наложен эффект «${definition}»`
    case 'EffectRefreshed': return `Обновлён эффект «${definition}»`
    case 'EffectExpired':
    case 'EffectRemoved': return `Эффект «${definition}» завершён`
    case 'HealingApplied': return `Восстановлено ${event.amount} здоровья`
    case 'TauntApplied': return `Провокация · ${definition}`
    case 'ActorDied': return actorIsEnemy ? `${enemyName} повержен` : 'Вы повержены'
    case 'EnemyKilled': return `${enemyName} повержен`
    case 'CombatEnded': return event.definitionId === 'Victory'
      ? 'Победа'
      : event.definitionId === 'Defeat'
        ? 'Поражение'
        : 'Бой завершён'
    default: return definition ? `${humanizeEventType(event.type)} · ${definition}` : humanizeEventType(event.type)
  }
}

function humanizeEventType(type: string): string {
  return type.replace(/([a-z])([A-Z])/g, '$1 $2')
}

async function leaveCombat(): Promise<void> {
  const left = await combat.leave()
  if (left) emit('leave')
}

onUnmounted(() => window.clearInterval(timer))
</script>

<template>
  <section class="combat-screen">
    <template v-if="snapshot && enemyPresentation">
      <section class="enemy-stage">
        <div class="enemy-stage__heading">
          <div>
            <p class="eyebrow">ПРОТИВНИК · УР. {{ enemyPresentation.level }}</p>
            <h1>{{ enemyPresentation.name }}</h1>
          </div>
          <span class="combat-state" :data-status="snapshot.status">{{ snapshot.status }}</span>
        </div>

        <div class="enemy-portrait">
          <div class="enemy-portrait__glow" />
          <img :src="enemyPresentation.art" :alt="enemyPresentation.name" />
        </div>

        <div class="enemy-health">
          <div class="enemy-health__numbers">
            <strong>{{ enemyPresentation.name }}</strong>
            <span>{{ Math.ceil(snapshot.enemy.hp) }} / {{ Math.ceil(snapshot.enemy.maxHp) }} HP</span>
          </div>
          <UIHealthBar
            :label="`${enemyPresentation.name} HP`"
            :value="snapshot.enemy.hp"
            :max="snapshot.enemy.maxHp"
          />
        </div>

        <div v-if="snapshot.enemy.effects.length" class="effects">
          <span v-for="effect in snapshot.enemy.effects" :key="effect.id">
            {{ abilityName(effect.id) }}<b v-if="effect.stacks > 1">×{{ effect.stacks }}</b>
          </span>
        </div>
      </section>

      <section class="player-panel">
        <div class="player-heading">
          <div>
            <small>ВАШ ПЕРСОНАЖ</small>
            <strong>{{ snapshot.player.name }}</strong>
          </div>
          <span v-if="snapshot.player.hp / snapshot.player.maxHp <= 0.25" class="danger-badge">Низкое здоровье</span>
        </div>

        <div class="player-bars">
          <UIHealthBar label="Здоровье" :value="snapshot.player.hp" :max="snapshot.player.maxHp" />
          <UIHealthBar
            label="Ярость"
            tone="rage"
            :value="snapshot.player.resource"
            :max="snapshot.player.maxResource"
          />
        </div>

        <div class="abilities" aria-label="Способности воина">
          <button
            v-for="ability in displayAbilities"
            :key="ability.id"
            class="ability"
            type="button"
            :title="`${abilityName(ability.id)} — ${abilityCaption(ability)}`"
            :disabled="
              !combat.isActive
              || combat.pending
              || cooldownRemaining(ability.id) > 0
              || snapshot.player.resource < ability.resourceCost
            "
            @click="combat.useAbility(ability.id)"
          >
            <span class="ability__icon">
              <img v-if="abilityPresentation[ability.id]?.art" :src="abilityPresentation[ability.id]?.art" alt="" />
              <b v-else>{{ abilityShortName(ability.id) }}</b>
              <i v-if="cooldownRemaining(ability.id) > 0">
                {{ Math.ceil(cooldownRemaining(ability.id)) }}
              </i>
            </span>
            <span class="ability__copy">
              <strong>{{ abilityName(ability.id) }}</strong>
              <small>{{ abilityState(ability) }}</small>
            </span>
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
        <div class="combat-log__heading">
          <b>Журнал боя</b>
          <small>Кто сделал → что произошло</small>
        </div>
        <ol>
          <li
            v-for="entry in logEntries"
            :key="entry.key"
            :data-side="entry.side"
          >
            <span class="log-actor">{{ entry.actor }}</span>
            <span class="log-text">{{ entry.text }}</span>
            <small class="log-sequence">#{{ entry.sequence }}</small>
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
.enemy-stage { display: grid; gap: var(--ui-space-2); padding: var(--ui-space-3) var(--ui-space-4) var(--ui-space-4); border-bottom: 1px solid var(--ui-color-border); background: linear-gradient(180deg, rgb(7 9 18 / 24%), rgb(7 9 18 / 92%)), url('@/assets/world/forest.jpg') center/cover; }
.enemy-stage__heading { z-index: 1; display: flex; align-items: center; justify-content: space-between; gap: var(--ui-space-3); }
.eyebrow { margin: 0; color: var(--ui-color-warning); font-size: var(--ui-font-size-xs); font-weight: var(--ui-font-weight-bold); letter-spacing: .1em; }
.enemy-stage h1 { margin: 0; color: var(--ui-color-text-primary); font: var(--ui-font-weight-bold) var(--ui-font-size-xl) var(--ui-font-display); }
.combat-state { flex: 0 0 auto; padding: 3px 8px; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-round); background: rgb(8 11 23 / 68%); color: var(--ui-color-warning); font-size: var(--ui-font-size-xs); text-transform: uppercase; }
.combat-state[data-status='Victory'] { color: var(--ui-color-success); }
.combat-state[data-status='Defeat'] { color: var(--ui-color-danger); }
.enemy-portrait { position: relative; display: grid; min-height: 12rem; place-items: center; overflow: hidden; }
.enemy-portrait__glow { position: absolute; width: 13rem; height: 8rem; border-radius: 50%; background: rgb(255 134 52 / 14%); filter: blur(34px); }
.enemy-portrait img { position: relative; width: min(100%, 22rem); max-height: 14rem; object-fit: contain; filter: drop-shadow(0 18px 20px rgb(0 0 0 / 62%)); }
.enemy-health { z-index: 1; display: grid; gap: var(--ui-space-1); padding: var(--ui-space-2) var(--ui-space-3); border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-md); background: rgb(8 11 23 / 82%); backdrop-filter: blur(6px); }
.enemy-health__numbers { display: flex; justify-content: space-between; gap: var(--ui-space-3); color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-xs); }
.enemy-health__numbers strong { color: var(--ui-color-text-primary); }
.player-panel, .combat-log { padding: var(--ui-space-3) var(--ui-space-4); border-bottom: 1px solid var(--ui-color-border); background: rgb(15 18 34 / 94%); }
.player-panel { display: grid; gap: var(--ui-space-3); }
.player-heading { display: flex; align-items: center; justify-content: space-between; gap: var(--ui-space-2); }
.player-heading > div { display: grid; }
.player-heading small { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.player-heading strong { color: var(--ui-color-text-primary); }
.danger-badge { padding: 3px 7px; border: 1px solid rgb(255 78 99 / 50%); border-radius: var(--ui-radius-round); background: rgb(255 78 99 / 10%); color: var(--ui-color-danger); font-size: var(--ui-font-size-xs); font-weight: var(--ui-font-weight-bold); }
.player-bars { display: grid; gap: var(--ui-space-2); }
.abilities { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--ui-space-2); }
.ability { display: grid; grid-template-columns: auto minmax(0, 1fr); min-width: 0; align-items: center; gap: var(--ui-space-2); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-2); color: var(--ui-color-text-secondary); font: inherit; text-align: left; }
.ability:not(:disabled):active { transform: translateY(1px); }
.ability:disabled { filter: grayscale(.65); opacity: .48; }
.ability__icon { position: relative; display: grid; width: 3rem; height: 3rem; place-items: center; overflow: hidden; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-1); color: var(--ui-color-primary); }
.ability__icon img { width: 100%; height: 100%; object-fit: cover; }
.ability__icon > b { font-size: var(--ui-font-size-sm); }
.ability__icon i { position: absolute; inset: 0; display: grid; place-items: center; background: rgb(2 4 12 / 76%); color: white; font-style: normal; font-weight: 700; }
.ability__copy { display: grid; min-width: 0; gap: 2px; }
.ability__copy strong { overflow: hidden; color: var(--ui-color-text-primary); font-size: var(--ui-font-size-sm); text-overflow: ellipsis; white-space: nowrap; }
.ability__copy small { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.controls { display: flex; flex-wrap: wrap; gap: var(--ui-space-2); }
.effects { display: flex; flex-wrap: wrap; gap: var(--ui-space-1); }
.effects span { padding: 2px 6px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-round); background: rgb(8 11 23 / 78%); color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-xs); }
.combat-log { border-bottom: 0; }
.combat-log__heading { display: flex; align-items: baseline; justify-content: space-between; gap: var(--ui-space-2); }
.combat-log__heading small { color: var(--ui-color-text-muted); }
.combat-log ol { display: grid; max-height: 13rem; gap: var(--ui-space-2); margin: var(--ui-space-2) 0 0; padding: 0; overflow-y: auto; list-style: none; }
.combat-log li { display: grid; grid-template-columns: 4.75rem minmax(0, 1fr) auto; align-items: start; gap: var(--ui-space-2); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-left-width: 3px; border-radius: var(--ui-radius-sm); background: rgb(9 12 24 / 62%); color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-xs); }
.combat-log li[data-side='player'] { border-left-color: var(--ui-color-primary); }
.combat-log li[data-side='enemy'] { border-left-color: var(--ui-color-danger); }
.combat-log li[data-side='system'] { border-left-color: var(--ui-color-warning); }
.log-actor { overflow: hidden; font-weight: var(--ui-font-weight-bold); text-overflow: ellipsis; white-space: nowrap; }
li[data-side='player'] .log-actor { color: var(--ui-color-primary); }
li[data-side='enemy'] .log-actor { color: var(--ui-color-danger); }
li[data-side='system'] .log-actor { color: var(--ui-color-warning); }
.log-text { line-height: var(--ui-line-height-normal); }
.log-sequence { color: var(--ui-color-text-muted); font-variant-numeric: tabular-nums; }
.error { margin: var(--ui-space-2) var(--ui-space-4); color: var(--ui-color-danger); font-size: var(--ui-font-size-xs); }
@media (max-width: 420px) {
  .enemy-stage, .player-panel, .combat-log { padding-inline: var(--ui-space-3); }
  .enemy-portrait { min-height: 10rem; }
  .enemy-portrait img { max-height: 11.5rem; }
  .abilities { grid-template-columns: 1fr; }
  .combat-log li { grid-template-columns: 4.25rem minmax(0, 1fr); }
  .log-sequence { display: none; }
}
</style>
