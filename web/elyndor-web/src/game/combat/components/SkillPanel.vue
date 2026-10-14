<script setup lang="ts">
import { computed, onUnmounted, shallowRef, watch } from 'vue'

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
  globalCooldownEndsAtUtc?: string | null
  activeCastResolvesAtUtc?: string | null
  disabled: boolean
}>()
const emit = defineEmits<{ use: [ability: CombatAbility] }>()

const inspectedAbility = shallowRef<CombatAbility | null>(null)
const page = shallowRef(0)
const pageCount = computed(() => Math.max(1, Math.ceil(props.abilities.length / 10)))
const visibleAbilities = computed(() =>
  props.abilities.slice(page.value * 10, page.value * 10 + 10),
)
watch(pageCount, (count) => {
  page.value = Math.min(page.value, count - 1)
})
function changePage(next: number): void {
  cancelInspection()
  inspectedAbility.value = null
  suppressedAbilityId = null
  page.value = Math.max(0, Math.min(pageCount.value - 1, next))
}
let inspectionTimer: ReturnType<typeof setTimeout> | undefined
let suppressedAbilityId: string | null = null

function remaining(abilityId: string): number {
  const readyAt = props.cooldowns[abilityId]
  return readyAt ? Math.max(0, (Date.parse(readyAt) - props.now) / 1_000) : 0
}

function globalRemaining(ability: CombatAbility): number {
  if (ability.usesGlobalCooldown === false || !props.globalCooldownEndsAtUtc) return 0
  return Math.max(0, (Date.parse(props.globalCooldownEndsAtUtc) - props.now) / 1000)
}

function state(ability: CombatAbility): 'ready' | 'cooldown' | 'resource' | 'disabled' {
  if (props.disabled) return 'disabled'
  if (remaining(ability.id) > 0 || globalRemaining(ability) > 0.25) return 'cooldown'
  if (
    ability.canUseWhileCasting !== true &&
    props.activeCastResolvesAtUtc &&
    Date.parse(props.activeCastResolvesAtUtc) - props.now > 250
  )
    return 'cooldown'
  if (props.resource < ability.resourceCost) return 'resource'
  return 'ready'
}

function accessibleLabel(ability: CombatAbility): string {
  const currentState = state(ability)
  if (currentState === 'cooldown')
    return `${ability.displayName}, восстановление ${Math.ceil(Math.max(remaining(ability.id), globalRemaining(ability)))} секунд`
  if (currentState === 'resource') return `${ability.displayName}, недостаточно ресурса`
  if (currentState === 'disabled') return `${ability.displayName}, недоступно`
  return `${ability.displayName}, готово${(ability.castTimeSeconds ?? 0) > 0 ? `, каст ${ability.castTimeSeconds} секунд` : ', мгновенно'}`
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
  <section
    class="skill-panel"
    aria-labelledby="battle-skills-title"
    data-skill-panel
    data-combat-hotbar
  >
    <h2 id="battle-skills-title" class="skill-panel__heading">Умения</h2>
    <div id="battle-skill-grid" class="skill-panel__grid" data-skill-grid data-columns="5">
      <button
        v-for="ability in visibleAbilities"
        :key="ability.id"
        type="button"
        class="skill-panel__ability"
        :data-ability-slot="ability.id"
        :data-state="state(ability)"
        :data-queued="queuedAbilityIds.includes(ability.id)"
        :aria-describedby="
          inspectedAbility?.id === ability.id ? 'battle-skill-inspection' : undefined
        "
        :aria-label="
          accessibleLabel(ability) + (queuedAbilityIds.includes(ability.id) ? ', в очереди' : '')
        "
        :aria-disabled="state(ability) !== 'ready'"
        @pointerdown="inspect(ability)"
        @pointerup="cancelInspection"
        @pointercancel="cancelInspection"
        @pointerleave="cancelInspection"
        @contextmenu.prevent
        @keydown.f1.prevent="inspectedAbility = ability"
        @keydown.esc="inspectedAbility = null"
        @click="activate(ability)"
      >
        <span class="skill-panel__icon">
          <img
            v-if="resolveAbilityArt(ability.id, ability.iconId)"
            :src="resolveAbilityArt(ability.id, ability.iconId)!"
            alt=""
            loading="eager"
          />
          <IconGenerator
            v-else
            :config="{
              id: `battle-${ability.id}`,
              glyph: glyph(ability.id),
              category: 'skill',
            }"
          />
        </span>
        <span
          v-if="remaining(ability.id) > 0 || globalRemaining(ability) > 0.25"
          class="skill-panel__cooldown"
          data-cooldown-overlay
        >
          <b>{{ Math.ceil(Math.max(remaining(ability.id), globalRemaining(ability))) }}</b>
        </span>
      </button>
    </div>
    <nav v-if="pageCount > 1" class="skill-panel__pager" aria-label="Страницы умений">
      <button
        type="button"
        data-skill-prev
        :disabled="page === 0"
        aria-label="Предыдущие умения"
        @click="changePage(page - 1)"
      >
        ‹
      </button>
      <span role="status">{{ page + 1 }} / {{ pageCount }}</span>
      <button
        type="button"
        data-skill-next
        :disabled="page >= pageCount - 1"
        aria-label="Следующие умения"
        @click="changePage(page + 1)"
      >
        ›
      </button>
    </nav>
    <aside
      v-if="inspectedAbility"
      id="battle-skill-inspection"
      class="skill-panel__inspection"
      data-ability-inspection
      aria-live="polite"
    >
      <div>
        <strong>{{ inspectedAbility.displayName }}</strong>
        <p>
          {{ inspectedAbility.description || 'Описание способности пока не добавлено.' }}
        </p>
        <p>Стоимость: {{ inspectedAbility.resourceCost }}</p>
        <p data-ability-timing>
          {{
            (inspectedAbility.castTimeSeconds ?? 0) > 0
              ? `Каст: ${inspectedAbility.castTimeSeconds} с`
              : 'Мгновенно'
          }}
          ·
          {{
            inspectedAbility.cooldownSeconds > 0
              ? `Перезарядка: ${inspectedAbility.cooldownSeconds} с`
              : 'Без перезарядки'
          }}
        </p>
      </div>
      <button type="button" aria-label="Закрыть описание" @click="inspectedAbility = null">
        ×
      </button>
    </aside>
  </section>
</template>

<style scoped>
.skill-panel {
  position: relative;
  display: grid;
  gap: 4px;
  padding: 6px;
  border: 1px solid rgb(177 151 91 / 28%);
  border-radius: 10px;
  background: linear-gradient(180deg, #0a0d14ef, #05070cf5);
}
.skill-panel__heading {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}
.skill-panel__grid {
  display: grid;
  grid-template-columns: repeat(5, minmax(44px, 1fr));
  grid-template-rows: repeat(2, 52px);
  gap: 6px;
  justify-items: center;
  align-items: center;
  width: min(100%, 340px);
  margin-inline: auto;
}
.skill-panel__ability {
  position: relative;
  display: grid;
  place-items: center;
  width: min(100%, 52px);
  aspect-ratio: 1;
  min-width: 44px;
  min-height: 44px;
  padding: 2px;
  overflow: hidden;
  border: 1px solid #796a9d;
  border-radius: 50%;
  background: #090c14;
  color: #f4e8d1;
  cursor: pointer;
  touch-action: manipulation;
  user-select: none;
  -webkit-touch-callout: none;
}
.skill-panel__ability[data-state='ready'] {
  border-color: #b49b60;
  box-shadow:
    inset 0 0 0 1px #070910,
    0 2px 5px #0009;
}
.skill-panel__ability[data-state='cooldown'],
.skill-panel__ability[data-state='resource'],
.skill-panel__ability[data-state='disabled'] {
  filter: saturate(0.35) brightness(0.66);
}
.skill-panel__ability[data-queued='true'] {
  border-color: #83deb9;
  box-shadow: 0 0 0 2px #83deb944;
}
.skill-panel__ability:focus-visible {
  outline: 2px solid #e5ca79;
  outline-offset: 2px;
}
.skill-panel__icon {
  display: grid;
  width: 100%;
  height: 100%;
  place-items: center;
  overflow: hidden;
  border-radius: 50%;
  background: #060810;
}
.skill-panel__icon img,
.skill-panel__icon :deep(.icon-generator) {
  width: 100%;
  height: 100%;
  border-radius: inherit;
  object-fit: cover;
}
.skill-panel__cooldown {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  background: #03050a88;
}
.skill-panel__cooldown b {
  color: #fff;
  font: 800 16px/1 var(--ui-font-body);
  text-shadow: 0 1px 3px #000;
}
.skill-panel__pager {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  color: #bfb098;
  font-size: 11px;
}
.skill-panel__pager button {
  width: 44px;
  min-height: 32px;
  border: 1px solid #b1975b55;
  border-radius: 6px;
  background: #0b0e17;
  color: #e4d1a8;
  font: 700 22px/1 var(--ui-font-body);
}
.skill-panel__pager button:disabled {
  opacity: 0.35;
}
.skill-panel__inspection {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 10px;
  padding: 8px;
  border: 1px solid #876fcc66;
  border-radius: 7px;
  background: #0b0d17;
}
.skill-panel__inspection strong {
  font-size: 13px;
}
.skill-panel__inspection p {
  margin: 4px 0 0;
  color: #bcb4a9;
  font-size: 11px;
  line-height: 1.4;
}
.skill-panel__inspection button {
  width: 36px;
  height: 36px;
  flex: none;
  border: 1px solid #ffffff33;
  border-radius: 6px;
  background: transparent;
  color: #e7dfd2;
}
@media (max-width: 360px) {
  .skill-panel__grid {
    grid-template-rows: repeat(2, 46px);
    gap: 4px;
  }
  .skill-panel__ability {
    max-width: 46px;
  }
}
</style>
