<script setup lang="ts">
import { computed, shallowRef } from 'vue'
import type { CombatEffectSnapshot } from '@/api/contracts'
import { itemArtUrl } from '@/assets/itemArt'
import { resolveAbilityArt } from '@/game/talents/talentArt'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import UIModal from '@/ui/components/UIModal.vue'
import type { GlyphName } from '@/ui/icons/icon.types'

const props = defineProps<{
  effects: CombatEffectSnapshot[]
  now: number
  side: 'player' | 'enemy'
  sources?: Record<string, string>
}>()
const expanded = shallowRef(false)
const selectedEffectKey = shallowRef<string | null>(null)
const failedArt = shallowRef(new Set<string>())
const initiallyVisibleCount = 6
const activeEffects = computed(() =>
  props.effects.filter((effect) => Date.parse(effect.expiresAtUtc) > props.now),
)
const visibleEffects = computed(() =>
  expanded.value ? activeEffects.value : activeEffects.value.slice(0, initiallyVisibleCount),
)
const overflowCount = computed(() =>
  Math.max(0, activeEffects.value.length - initiallyVisibleCount),
)
const selectedEffect = computed(
  () => activeEffects.value.find((effect) => effectKey(effect) === selectedEffectKey.value) ?? null,
)
const criticalEffect = computed(() =>
  props.side === 'player'
    ? (activeEffects.value.find((effect) => effect.id === 'BLACK_STAR_LIVING_BOMB_MARK') ?? null)
    : null,
)

function effectKey(effect: CombatEffectSnapshot): string {
  return effect.instanceId ?? `${effect.id}:${effect.sourceActorId ?? ''}:${effect.expiresAtUtc}`
}
function effectName(effect: CombatEffectSnapshot): string {
  return effect.displayName?.trim() || kindLabel(effect)
}
function disposition(effect: CombatEffectSnapshot): 'Beneficial' | 'Harmful' | 'Neutral' {
  return (
    effect.disposition ??
    (['Debuff', 'DamageOverTime', 'Stun', 'Silence', 'Root', 'Fear', 'Disarm'].includes(
      effect.kind ?? '',
    )
      ? 'Harmful'
      : 'Neutral')
  )
}
function dispositionLabel(effect: CombatEffectSnapshot): string {
  return disposition(effect) === 'Beneficial'
    ? 'Усиление'
    : disposition(effect) === 'Harmful'
      ? 'Ослабление'
      : 'Эффект'
}
function kindLabel(effect: CombatEffectSnapshot): string {
  const labels: Record<string, string> = {
    Buff: 'Усиление',
    Debuff: 'Ослабление',
    DamageOverTime: 'Периодический урон',
    HealingOverTime: 'Периодическое исцеление',
    Shield: 'Поглощающий щит',
    StatModifier: 'Изменение характеристик',
    Stun: 'Оглушение',
    Silence: 'Немота',
    Root: 'Опутывание',
    Fear: 'Страх',
    Disarm: 'Обезоруживание',
    DamageReflection: 'Отражение урона',
    LethalDamagePrevention: 'Защита от смерти',
    TemporaryMaxHp: 'Запас здоровья',
  }
  return labels[effect.kind ?? ''] ?? 'Боевой эффект'
}
function remainingSeconds(effect: CombatEffectSnapshot): number {
  return Math.max(0, Math.ceil((Date.parse(effect.expiresAtUtc) - props.now) / 1000))
}
function timeLabel(effect: CombatEffectSnapshot): string {
  const seconds = remainingSeconds(effect)
  return seconds >= 3600
    ? `${Math.ceil(seconds / 3600)}ч`
    : seconds >= 60
      ? `${Math.ceil(seconds / 60)}м`
      : `${seconds}с`
}
function progress(effect: CombatEffectSnapshot): number {
  if (!effect.appliedAtUtc) return 1
  const duration = Date.parse(effect.expiresAtUtc) - Date.parse(effect.appliedAtUtc)
  return duration > 0
    ? Math.min(1, Math.max(0, (Date.parse(effect.expiresAtUtc) - props.now) / duration))
    : 0
}
function effectArt(effect: CombatEffectSnapshot): string | undefined {
  const art = resolveAbilityArt(effect.id, effect.iconId) ?? itemArtUrl(effect.iconId)
  return art && !failedArt.value.has(art) ? art : undefined
}
function rememberFailedArt(effect: CombatEffectSnapshot): void {
  const art = effectArt(effect)
  if (art) failedArt.value = new Set([...failedArt.value, art])
}
function effectGlyph(effect: CombatEffectSnapshot): GlyphName {
  if (effect.kind === 'Shield' || effect.kind === 'LethalDamagePrevention') return 'shield'
  if (effect.kind === 'HealingOverTime' || effect.kind === 'TemporaryMaxHp') return 'holy'
  if (['Stun', 'Root', 'Silence', 'Disarm', 'Fear'].includes(effect.kind ?? '')) return 'lock'
  const id = `${effect.id} ${effect.iconId ?? ''}`.toLowerCase()
  if (/fire|burn|flame|combust|pyro|ignite/.test(id)) return 'fire'
  if (/frost|ice|frozen|chill|cold|winter/.test(id)) return 'ice'
  if (/poison|venom/.test(id)) return 'poison'
  if (/shield|block|guard/.test(id)) return 'shield'
  if (/bow|arrow|mark|archer/.test(id)) return 'bow'
  if (/holy|light|paladin/.test(id)) return 'holy'
  if (effect.kind === 'DamageOverTime') return 'skull'
  return disposition(effect) === 'Harmful' ? 'shadow' : 'refresh'
}
function toggleDetails(effect: CombatEffectSnapshot): void {
  const key = effectKey(effect)
  selectedEffectKey.value = selectedEffectKey.value === key ? null : key
}
</script>

<template>
  <div
    v-if="activeEffects.length"
    class="combat-effect-strip"
    :class="`combat-effect-strip--${side}`"
    data-combat-effect-strip
  >
    <div
      v-if="criticalEffect"
      class="combat-effect-strip__critical-warning"
      data-critical-effect-warning
      role="alert"
      aria-live="assertive"
    >
      <IconGenerator :config="{ id: 'critical-effect', glyph: 'skull', category: 'effect' }" />
      <strong>ЧЁРНАЯ ЗВЕЗДА</strong><span aria-hidden="true">·</span>
      <b>{{ Math.max(0, (Date.parse(criticalEffect.expiresAtUtc) - now) / 1000).toFixed(1) }}</b>
    </div>
    <button
      v-for="effect in visibleEffects"
      :key="effectKey(effect)"
      type="button"
      class="combat-effect-strip__effect"
      :data-disposition="disposition(effect)"
      :class="{ 'combat-effect-strip__effect--selected': selectedEffectKey === effectKey(effect) }"
      :aria-label="`${effectName(effect)}. ${dispositionLabel(effect)}. ${effect.stacks > 1 ? `Стаки: ${effect.stacks}. ` : ''}Осталось ${timeLabel(effect)}. Нажмите для описания.`"
      :aria-pressed="selectedEffectKey === effectKey(effect)"
      aria-haspopup="dialog"
      @click="toggleDetails(effect)"
    >
      <img
        v-if="effectArt(effect)"
        :src="effectArt(effect)"
        alt=""
        @error="rememberFailedArt(effect)"
      />
      <IconGenerator
        v-else
        :config="{ id: `effect-${effect.id}`, glyph: effectGlyph(effect), category: 'effect' }"
      />
      <b v-if="effect.stacks > 1" class="combat-effect-strip__stacks">{{ effect.stacks }}</b>
      <small>{{ timeLabel(effect) }}</small>
      <i class="combat-effect-strip__duration" aria-hidden="true"
        ><span :style="{ transform: `scaleX(${progress(effect)})` }"
      /></i>
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
    <UIModal
      :open="selectedEffect !== null"
      :title="selectedEffect ? effectName(selectedEffect) : ''"
      compact
      @close="selectedEffectKey = null"
    >
      <section v-if="selectedEffect" class="effect-inspection" data-effect-inspection>
        <div class="effect-inspection__heading">
          <img
            v-if="effectArt(selectedEffect)"
            :src="effectArt(selectedEffect)"
            alt=""
            @error="rememberFailedArt(selectedEffect)"
          />
          <IconGenerator
            v-else
            :config="{
              id: `inspect-${selectedEffect.id}`,
              glyph: effectGlyph(selectedEffect),
              category: 'effect',
            }"
          />
          <div>
            <strong>{{ dispositionLabel(selectedEffect) }}</strong
            ><span v-if="kindLabel(selectedEffect) !== dispositionLabel(selectedEffect)">{{
              kindLabel(selectedEffect)
            }}</span>
          </div>
        </div>
        <p v-if="selectedEffect.description?.trim()">{{ selectedEffect.description }}</p>
        <dl>
          <div>
            <dt>Осталось</dt>
            <dd>{{ timeLabel(selectedEffect) }}</dd>
          </div>
          <div v-if="selectedEffect.sourceActorId && sources?.[selectedEffect.sourceActorId]">
            <dt>Источник</dt>
            <dd>{{ sources[selectedEffect.sourceActorId] }}</dd>
          </div>
          <div v-if="selectedEffect.stacks > 1 || (selectedEffect.maxStacks ?? 1) > 1">
            <dt>Стаки</dt>
            <dd>
              {{ selectedEffect.stacks
              }}<template v-if="selectedEffect.maxStacks">
                / {{ selectedEffect.maxStacks }}</template
              >
            </dd>
          </div>
        </dl>
      </section>
    </UIModal>
  </div>
</template>

<style scoped>
.combat-effect-strip {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
}
.combat-effect-strip__effect,
.combat-effect-strip__overflow {
  position: relative;
  display: grid;
  width: 44px;
  height: 44px;
  flex: none;
  place-items: center;
  overflow: hidden;
  padding: 3px 3px 8px;
  border: 1px solid #6c7181;
  border-radius: 8px;
  background: #0b111a;
  color: #f4f0dc;
  cursor: pointer;
  touch-action: manipulation;
}
.combat-effect-strip__effect[data-disposition='Beneficial'] {
  border-color: #789d8e;
}
.combat-effect-strip__effect[data-disposition='Harmful'] {
  border-color: #c47466;
}
.combat-effect-strip__effect--selected {
  box-shadow: inset 0 0 0 2px #dfc380;
}
.combat-effect-strip__effect:focus-visible,
.combat-effect-strip__overflow:focus-visible {
  outline: 2px solid #dfc380;
  outline-offset: 2px;
}
.combat-effect-strip__effect img,
.combat-effect-strip__effect :deep(.icon-generator) {
  width: 32px;
  height: 32px;
  border-radius: 4px;
  object-fit: cover;
}
.combat-effect-strip__effect small {
  position: absolute;
  right: 0;
  bottom: 3px;
  left: 0;
  background: rgb(3 6 10 / 88%);
  color: #fff4dc;
  font-size: 10px;
  font-weight: 700;
  line-height: 12px;
  text-align: center;
  font-variant-numeric: tabular-nums;
}
.combat-effect-strip__stacks {
  position: absolute;
  top: 1px;
  right: 1px;
  min-width: 14px;
  padding: 0 2px;
  border-radius: 3px;
  background: #d5b775;
  color: #10141b;
  font-size: 10px;
  line-height: 14px;
  text-align: center;
}
.combat-effect-strip__duration {
  position: absolute;
  right: 0;
  bottom: 0;
  left: 0;
  height: 2px;
  background: #242d38;
}
.combat-effect-strip__duration span {
  display: block;
  height: 100%;
  background: #d5b775;
  transform-origin: left;
}
.combat-effect-strip__effect[data-disposition='Harmful'] .combat-effect-strip__duration span {
  background: #de8d78;
}
.combat-effect-strip__overflow {
  padding: 3px;
  border-style: dashed;
  font-size: 12px;
  font-weight: 800;
}
.combat-effect-strip__critical-warning {
  display: flex;
  width: 100%;
  min-height: 38px;
  align-items: center;
  justify-content: center;
  gap: 7px;
  padding: 6px 10px;
  border: 1px solid #c65e52;
  border-radius: 8px;
  background: linear-gradient(90deg, #2d0409, #5e0c11, #2d0409);
  color: #fff1dc;
}
.combat-effect-strip__critical-warning :deep(.icon-generator) {
  width: 22px;
  height: 22px;
}
.combat-effect-strip__critical-warning strong {
  font-size: 12px;
  letter-spacing: 0.06em;
}
.combat-effect-strip__critical-warning b {
  color: #ffcf66;
  font-variant-numeric: tabular-nums;
}
.effect-inspection {
  color: var(--ui-color-text-primary);
  font-size: 14px;
  line-height: 1.55;
}
.effect-inspection__heading {
  display: flex;
  align-items: center;
  gap: 12px;
}
.effect-inspection__heading > img,
.effect-inspection__heading > :deep(.icon-generator) {
  width: 54px;
  height: 54px;
  border: 1px solid #b39356;
  border-radius: 8px;
  object-fit: cover;
}
.effect-inspection__heading div {
  display: grid;
  gap: 3px;
}
.effect-inspection__heading strong {
  color: #d9bf83;
  font-size: 12px;
}
.effect-inspection__heading span {
  color: var(--ui-color-text-muted);
  font-size: 12px;
}
.effect-inspection p {
  margin: 16px 0;
}
.effect-inspection dl {
  display: flex;
  flex-wrap: wrap;
  gap: 10px 24px;
  margin: 12px 0 0;
  padding-top: 12px;
  border-top: 1px solid rgb(205 177 113 / 20%);
}
.effect-inspection dl div {
  display: grid;
  gap: 2px;
}
.effect-inspection dt {
  color: var(--ui-color-text-muted);
  font-size: 11px;
}
.effect-inspection dd {
  margin: 0;
  color: #f0d99e;
  font-size: 13px;
  font-variant-numeric: tabular-nums;
}
</style>
