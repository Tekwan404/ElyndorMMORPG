<script setup lang="ts">
const count = defineModel<number | string>({ required: true })
const props = defineProps<{ max: number; disabled?: boolean }>()
const presets = [1, 5, 10, 25, 50, 100]
function adjust(delta: number) {
  count.value = Math.min(props.max, Math.max(1, (Number(count.value) || 1) + delta))
}
</script>

<template>
  <fieldset class="quantity-selector" :disabled="disabled">
    <legend>Количество пачек</legend>
    <div class="quantity-stepper">
      <button type="button" aria-label="Уменьшить количество" :disabled="Number(count) <= 1" @click="adjust(-1)">−</button>
      <input v-model="count" data-pack-count aria-label="Количество пачек" type="number" inputmode="numeric" min="1" :max="max" step="1" />
      <button type="button" aria-label="Увеличить количество" :disabled="Number(count) >= max" @click="adjust(1)">+</button>
    </div>
    <div class="quantity-presets">
      <button v-for="value in presets" :key="value" type="button" :disabled="value > max" :aria-pressed="Number(count) === value" @click="count = value">{{ value }}</button>
    </div>
  </fieldset>
</template>

<style scoped>
.quantity-selector { min-width: 0; margin: 0; padding: 0; border: 0; }
.quantity-selector legend { margin-bottom: 6px; color: #b5a88b; font-size: 12px; }
.quantity-stepper { display: grid; grid-template-columns: 48px minmax(0, 1fr) 48px; gap: 6px; }
.quantity-stepper input, .quantity-selector button { min-height: 44px; border: 1px solid rgb(205 177 113 / 32%); border-radius: 8px; background: #0b1019; color: #ead8b0; font: inherit; text-align: center; }
.quantity-stepper input { width: 100%; min-width: 0; box-sizing: border-box; font-size: 18px; }
.quantity-selector button { cursor: pointer; }
.quantity-selector button:disabled { opacity: .4; cursor: default; }
.quantity-selector button[aria-pressed="true"] { background: #44341f; border-color: #cfad69; }
.quantity-selector :focus-visible { outline: 2px solid #cfad69; outline-offset: 2px; }
.quantity-presets { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 4px; margin-top: 6px; }
</style>
