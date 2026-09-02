<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'

import type { CombatEvent } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIHealthBar } from '@/ui/components'

const emit = defineEmits<{ leave: [] }>()
const combat = useCombatSessionStore()
const session = useGameSessionStore()
const now = ref(Date.now())
const timer = window.setInterval(() => (now.value = Date.now()), 250)
const snapshot = computed(() => combat.snapshot)

type LogSide = 'player' | 'enemy' | 'system'
interface CombatLogEntry { key: number; side: LogSide; actor: string; text: string; detail?: string }

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

const enemyPresentation = computed(() => {
  const enemy = snapshot.value?.enemy
  if (!enemy) return null
  return monsterPresentation[enemy.definitionId] ?? { name: enemy.name, level: 1, art: gameArt.monsters.wolf }
})
const displayAbilities = computed(() => snapshot.value?.player.abilities ?? [])
const healingPotion = computed(() => session.snapshot?.character?.inventory.items.find((item) => item.definitionId === 'SMALL_HEALING_POTION') ?? null)

const logEntries = computed<CombatLogEntry[]>(() => {
  const events = combat.events.slice(-40)
  const entries: CombatLogEntry[] = []
  const consumedResources = new Set<number>()

  for (let index = 0; index < events.length; index += 1) {
    const event = events[index]!
    if (consumedResources.has(event.sequence)) continue
    if (['AbilityStarted', 'AbilityCompleted', 'CriticalHit'].includes(event.type)) continue
    if (event.type === 'ResourceChanged') continue

    if (event.type === 'AbilityUsed') {
      const hasOutcome = events.slice(Math.max(0, index - 6), index).some((candidate) =>
        candidate.definitionId === event.definitionId
        && candidate.sourceActorId === event.sourceActorId
        && ['DamageDealt', 'EffectApplied', 'HealingApplied', 'TauntApplied'].includes(candidate.type),
      )
      if (hasOutcome) continue
    }

    const previous = index > 0 ? events[index - 1] : undefined
    const critical = event.type === 'DamageDealt'
      && previous?.type === 'CriticalHit'
      && previous.sourceActorId === event.sourceActorId
      && previous.targetActorId === event.targetActorId
      && previous.amount === event.amount

    const resourceEvent = event.type === 'DamageDealt'
      ? events.slice(index + 1, Math.min(events.length, index + 6)).find((candidate) =>
          candidate.type === 'ResourceChanged'
          && candidate.amount !== 0
          && candidate.serverTimeUtc === event.serverTimeUtc
          && !consumedResources.has(candidate.sequence),
        )
      : undefined
    if (resourceEvent) consumedResources.add(resourceEvent.sequence)

    const side = eventSide(event)
    entries.push({
      key: event.sequence,
      side,
      actor: actorLabel(side),
      text: eventText(event, critical),
      detail: resourceEvent ? `${resourceEvent.amount > 0 ? '+' : ''}${resourceEvent.amount} ярости · ${abilityName(resourceEvent.definitionId)}` : undefined,
    })
  }
  return entries.slice(-12).reverse()
})

function cooldownRemaining(abilityId: string): number {
  const readyAt = snapshot.value?.player.cooldowns[abilityId]
  return readyAt ? Math.max(0, (Date.parse(readyAt) - now.value) / 1_000) : 0
}

function abilityName(id: string | null | undefined): string {
  if (!id) return ''
  if (id === 'AUTO_ATTACK') return 'Автоатака'
  if (id === 'BITE') return 'Укус'
  if (id === 'DIRECT_DAMAGE_TAKEN') return 'Получение урона'
  if (id === 'SMALL_HEALING_POTION') return 'Малое зелье лечения'
  return abilityPresentation[id]?.name ?? id.split('_').join(' ')
}

function eventSide(event: CombatEvent): LogSide {
  const current = snapshot.value
  if (!current || ['CombatStarted', 'CombatEnded', 'ActorDied', 'EnemyKilled'].includes(event.type)) return 'system'
  const source = event.sourceActorId ?? event.actorId
  if (source === current.player.actorId) return 'player'
  if (source === current.enemy.actorId) return 'enemy'
  return 'system'
}

function actorLabel(side: LogSide): string {
  if (side === 'player') return 'ВЫ'
  if (side === 'enemy') return enemyPresentation.value?.name.toUpperCase() ?? 'ВРАГ'
  return 'СИСТЕМА'
}

function eventText(event: CombatEvent, critical = false): string {
  const definition = abilityName(event.definitionId)
  const enemyName = enemyPresentation.value?.name ?? 'Противник'
  switch (event.type) {
    case 'CombatStarted': return `Бой с ${enemyName} начался`
    case 'AutoAttackStarted': return 'Автоатака включена'
    case 'AutoAttackStopped': return 'Автоатака остановлена'
    case 'DamageDealt': return `${definition || 'Атака'} · ${Math.round(event.amount)} урона${critical ? ' · КРИТ!' : ''}`
    case 'AbilityUsed': return `${definition} · действие выполнено`
    case 'EffectApplied': return `Наложен эффект «${definition}»`
    case 'HealingApplied': return `Восстановлено ${Math.round(event.amount)} здоровья`
    case 'ConsumableUsed': return `${definition} · +${Math.round(event.amount)} здоровья`
    case 'TauntApplied': return `Провокация · ${definition}`
    case 'ActorDied': return event.actorId === snapshot.value?.enemy.actorId ? `${enemyName} повержен` : 'Вы повержены'
    case 'EnemyKilled': return `${enemyName} повержен`
    case 'CombatEnded': return event.definitionId === 'Victory' ? 'Победа' : event.definitionId === 'Defeat' ? 'Поражение' : 'Бой завершён'
    default: return definition ? `Событие · ${definition}` : 'Событие боя'
  }
}

async function usePotion(): Promise<void> {
  if (!healingPotion.value || !snapshot.value) return
  await combat.useConsumable(healingPotion.value.definitionId)
  await session.refreshSnapshot()
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
        <div class="enemy-heading"><div><small>ПРОТИВНИК · УР. {{ enemyPresentation.level }}</small><h1>{{ enemyPresentation.name }}</h1></div></div>
        <div class="enemy-portrait"><img :src="enemyPresentation.art" :alt="enemyPresentation.name" /></div>
        <UIHealthBar :label="`${enemyPresentation.name} · ${Math.ceil(snapshot.enemy.hp)} / ${Math.ceil(snapshot.enemy.maxHp)}`" :value="snapshot.enemy.hp" :max="snapshot.enemy.maxHp" />
      </section>

      <section class="player-panel">
        <div class="player-heading"><div><small>ВАШ ПЕРСОНАЖ</small><strong>{{ snapshot.player.name }}</strong></div><span :class="{ active: snapshot.player.autoAttackEnabled }">{{ snapshot.player.autoAttackEnabled ? 'Автоатака включена' : 'Автоатака выключена' }}</span></div>
        <UIHealthBar label="Здоровье" :value="snapshot.player.hp" :max="snapshot.player.maxHp" />
        <UIHealthBar label="Ярость" tone="rage" :value="snapshot.player.resource" :max="snapshot.player.maxResource" />

        <div class="abilities">
          <button v-for="ability in displayAbilities" :key="ability.id" type="button" :disabled="combat.pending || cooldownRemaining(ability.id) > 0 || snapshot.player.resource < ability.resourceCost" @click="combat.useAbility(ability.id)">
            <span class="ability-icon"><img v-if="abilityPresentation[ability.id]?.art" :src="abilityPresentation[ability.id]?.art" alt="" /><b v-else>{{ abilityName(ability.id).slice(0,2) }}</b></span>
            <span><strong>{{ abilityName(ability.id) }}</strong><small>{{ cooldownRemaining(ability.id) > 0 ? `${Math.ceil(cooldownRemaining(ability.id))} сек.` : ability.resourceCost > 0 ? `${ability.resourceCost} ярости` : 'Без затрат' }}</small></span>
          </button>
        </div>

        <div class="consumables">
          <button type="button" :disabled="combat.pending || !healingPotion || snapshot.player.hp >= snapshot.player.maxHp" @click="usePotion">
            <span>✚</span><div><strong>Малое зелье лечения</strong><small v-if="healingPotion">+{{ healingPotion.healAmount }} здоровья · в рюкзаке {{ healingPotion.quantity }}</small><small v-else>В рюкзаке нет зелий</small></div>
          </button>
        </div>

        <div class="controls"><UIButton variant="secondary" :disabled="combat.pending" @click="combat.toggleAutoAttack">{{ snapshot.player.autoAttackEnabled ? 'Остановить автоатаку' : 'Включить автоатаку' }}</UIButton><UIButton variant="ghost" :disabled="combat.pending" @click="leaveCombat">Покинуть бой</UIButton></div>
      </section>

      <section class="combat-log">
        <header><b>Журнал боя</b><small>Последние действия</small></header>
        <ol>
          <li v-for="entry in logEntries" :key="entry.key" :data-side="entry.side">
            <span class="actor">{{ entry.actor }}</span>
            <div><strong>{{ entry.text }}</strong><small v-if="entry.detail">↳ {{ entry.detail }}</small></div>
          </li>
        </ol>
      </section>
      <p v-if="combat.errorCode" class="error">Не удалось выполнить действие: {{ combat.errorCode }}</p>
    </template>
    <div v-else class="missing"><h1>Бой прерван</h1><UIButton @click="emit('leave')">Вернуться в мир</UIButton></div>
  </section>
</template>

<style scoped>
.combat-screen{display:grid;width:min(100%,var(--ui-content-width));margin-inline:auto;gap:var(--ui-space-4);padding:var(--ui-space-4)}
.enemy-stage,.player-panel,.combat-log{display:grid;gap:var(--ui-space-3);padding:var(--ui-space-4);border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-lg);background:var(--ui-color-surface-1)}
.enemy-heading h1{margin:0;font-family:var(--ui-font-display)}.enemy-heading small,.player-heading small,.combat-log header small{color:var(--ui-color-text-muted)}.enemy-portrait{display:grid;height:15rem;place-items:center;overflow:hidden;background:radial-gradient(circle,rgb(96 82 255 / 15%),transparent 60%)}.enemy-portrait img{width:100%;height:100%;object-fit:contain}
.player-heading{display:flex;justify-content:space-between;gap:var(--ui-space-2)}.player-heading>div{display:grid}.player-heading>span{color:var(--ui-color-text-muted);font-size:var(--ui-font-size-xs)}.player-heading>span.active{color:var(--ui-color-success)}
.abilities{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:var(--ui-space-2)}.abilities button,.consumables button{display:grid;grid-template-columns:auto 1fr;align-items:center;gap:var(--ui-space-2);padding:var(--ui-space-2);border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-md);background:var(--ui-color-surface-2);color:inherit;font:inherit;text-align:left}.abilities button>span:last-child,.consumables div{display:grid}.abilities small,.consumables small{color:var(--ui-color-text-muted)}.ability-icon,.consumables button>span{display:grid;width:2.8rem;height:2.8rem;place-items:center;overflow:hidden;border-radius:var(--ui-radius-md);background:var(--ui-color-background);color:var(--ui-color-primary)}.ability-icon img{width:100%;height:100%;object-fit:cover}
.controls{display:flex;flex-wrap:wrap;gap:var(--ui-space-2)}.combat-log header{display:flex;justify-content:space-between}.combat-log ol{display:grid;gap:var(--ui-space-2);margin:0;padding:0;list-style:none}.combat-log li{display:grid;grid-template-columns:5rem 1fr;gap:var(--ui-space-2);padding:var(--ui-space-2);border-radius:var(--ui-radius-md);background:var(--ui-color-surface-2)}.combat-log li[data-side='player']{border-left:3px solid var(--ui-color-primary)}.combat-log li[data-side='enemy']{border-left:3px solid var(--ui-color-danger)}.combat-log li[data-side='system']{border-left:3px solid var(--ui-color-border-strong)}.actor{font-size:var(--ui-font-size-xs);font-weight:700}.combat-log li div{display:grid;gap:2px}.combat-log li small{color:var(--ui-color-text-muted);font-weight:400}.error{color:var(--ui-color-danger)}.missing{display:grid;gap:var(--ui-space-3);place-items:start}
@media(max-width:420px){.abilities{grid-template-columns:1fr}.combat-log li{grid-template-columns:4rem 1fr}.enemy-portrait{height:12rem}}
</style>
