<script setup lang="ts">
import { computed, ref } from 'vue'

import type { CombatEffectSnapshot } from '@/api/contracts'
import { itemArtUrl } from '@/assets/itemArt'
import { resolveAbilityArt } from '@/game/talents/talentArt'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName } from '@/ui/icons/icon.types'

const props = defineProps<{
  effects: CombatEffectSnapshot[]
  now: number
  side: 'player' | 'enemy'
}>()

const expanded = ref(false)
const selectedEffectKey = ref<string | null>(null)
const initiallyVisibleCount = 6
const visibleEffects = computed(() =>
  expanded.value ? props.effects : props.effects.slice(0, initiallyVisibleCount),
)
const overflowCount = computed(() => Math.max(0, props.effects.length - initiallyVisibleCount))
const selectedEffect = computed(() =>
  props.effects.find(effect => effectKey(effect) === selectedEffectKey.value) ?? null,
)

function effectKey(effect: CombatEffectSnapshot): string {
  return `${effect.id}:${effect.expiresAtUtc}`
}

function effectName(effect: CombatEffectSnapshot): string {
  if (effect.displayName?.trim()) return effect.displayName
  const readableId = effect.id.replace(/[_-]+/g, ' ').toLocaleLowerCase('ru-RU')
  return readableId.charAt(0).toLocaleUpperCase('ru-RU') + readableId.slice(1)
}

function effectDescription(effect: CombatEffectSnapshot): string {
  return effect.description?.trim() || 'Подробное описание этого эффекта пока не добавлено.'
}

function remainingSeconds(effect: CombatEffectSnapshot): number {
  return Math.max(0, Math.ceil((Date.parse(effect.expiresAtUtc) - props.now) / 1_000))
}

function effectArt(effect: CombatEffectSnapshot): string | undefined {
  return resolveAbilityArt(effect.id, effect.iconId) ?? itemArtUrl(effect.iconId)
}

function effectGlyph(effect: CombatEffectSnapshot): GlyphName {
  const id = `${effect.id} ${effect.iconId ?? ''}`.toLowerCase()
  if (id.includes('fire') || id.includes('burn') || id.includes('flame')) return 'fire'
  if (id.includes('frost') || id.includes('ice') || id.includes('frozen')) return 'ice'
  if (id.includes('poison')) return 'poison'
  if (id.includes('shield') || id.includes('block') || id.includes('guard')) return 'shield'
  if (id.includes('bow') || id.includes('arrow') || id.includes('mark')) return 'bow'
  if (id.includes('holy') || id.includes('light')) return 'holy'
  return 'star'
}

function toggleDetails(effect: CombatEffectSnapshot): void {
  const key = effectKey(effect)
  selectedEffectKey.value = selectedEffectKey.value === key ? null : key
}
</script>

<template>
  <div
    v-if="effects.length"
    class="combat-effect-strip"
    :class="`combat-effect-strip--${side}`"
    data-combat-effect-strip
  >
    <button
      v-for="effect in visibleEffects"
      :key="effectKey(effect)"
      type="button"
      class="combat-effect-strip__effect"
      :class="{ 'combat-effect-strip__effect--selected': selectedEffectKey === effectKey(effect) }"
      :aria-label="`${effectName(effect)}. ${effect.stacks > 1 ? `Стаки: ${effect.stacks}. ` : ''}${remainingSeconds(effect)} сек. Нажмите, чтобы посмотреть описание.`"
      :aria-pressed="selectedEffectKey === effectKey(effect)"
      @click="toggleDetails(effect)"
    >
      <img v-if="effectArt(effect)" :src="effectArt(effect)" alt="" />
      <IconGenerator
        v-else
        :config="{ id: `effect-${effect.id}`, glyph: effectGlyph(effect), category: 'effect' }"
      />
      <b v-if="effect.stacks > 1" class="combat-effect-strip__stacks">{{ effect.stacks }}</b>
      <small>{{ remainingSeconds(effect) }}с</small>
    </button>

    <button
      v-if="overflowCount"
      type="button"
      class="combat-effect-strip__overflow"
      :aria-label="expanded ? 'Свернуть список эффектов' : `Показать ещё ${overflowCount} эффектов`"
      @click="expanded = !expanded"
    >
      {{ expanded ? '−' : `+${overflowCount}` }}
    </button>

    <section v-if="selectedEffect" class="combat-effect-strip__details" data-effect-inspection aria-live="polite">
      <div>
        <strong>{{ effectName(selectedEffect) }}</strong>
        <p>{{ effectDescription(selectedEffect) }}</p>
        <small v-if="selectedEffect.stacks > 1">Стаки: {{ selectedEffect.stacks }}</small>
        <small>Осталось: {{ remainingSeconds(selectedEffect) }} сек.</small>
      </div>
      <button type="button" aria-label="Закрыть описание эффекта" @click="selectedEffectKey = null">×</button>
    </section>
  </div>
</template>

<style scoped>
.combat-effect-strip { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; }
.combat-effect-strip__effect, .combat-effect-strip__overflow { position: relative; display: grid; width: 34px; height: 34px; flex: none; place-items: center; padding: 2px; border: 1px solid rgb(191 183 255 / 38%); border-radius: 8px; background: rgb(5 8 15 / 88%); color: var(--ui-color-text-primary); font: inherit; }
.combat-effect-strip__effect--selected { border-color: var(--ui-color-accent, #aaa3ff); box-shadow: 0 0 0 1px rgb(170 163 255 / 28%); }
.combat-effect-strip__effect img, .combat-effect-strip__effect :deep(.icon-generator) { width: 25px; height: 25px; object-fit: cover; }
.combat-effect-strip__effect small { position: absolute; right: -3px; bottom: -4px; padding: 0 3px; border: 1px solid rgb(255 255 255 / 12%); border-radius: 5px; background: #090c13; color: #f4f0dc; font-size: .48rem; line-height: 1.3; }
.combat-effect-strip__stacks { position: absolute; top: -4px; left: -4px; display: grid; min-width: 14px; height: 14px; place-items: center; border-radius: 50%; background: #aaa3ff; color: #090915; font-size: .52rem; line-height: 1; }
.combat-effect-strip__overflow { border-style: dashed; color: var(--ui-color-text-muted); font-size: .62rem; font-weight: 800; }
.combat-effect-strip__details { display: flex; width: 100%; align-items: flex-start; justify-content: space-between; gap: 8px; margin-top: 3px; padding: 7px 9px; border: 1px solid rgb(191 183 255 / 35%); border-radius: var(--ui-radius-sm); background: rgb(5 8 15 / 94%); }
.combat-effect-strip__details strong { display: block; color: var(--ui-color-text-primary); font-size: .65rem; }
.combat-effect-strip__details p { margin: 3px 0; color: var(--ui-color-text-muted); font-size: .58rem; line-height: 1.35; }
.combat-effect-strip__details small { display: inline-block; margin: 2px 8px 0 0; color: var(--ui-color-text-muted); font-size: .52rem; }
.combat-effect-strip__details > button { display: grid; width: 28px; height: 28px; flex: none; place-items: center; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: transparent; color: var(--ui-color-text-primary); font: inherit; }
.combat-effect-strip--enemy .combat-effect-strip__effect { border-color: rgb(245 123 103 / 34%); }
.combat-effect-strip--player { flex: 1; min-width: 0; }
.combat-effect-strip--enemy .combat-effect-strip__details { width: min(76vw, 280px); }
</style>
