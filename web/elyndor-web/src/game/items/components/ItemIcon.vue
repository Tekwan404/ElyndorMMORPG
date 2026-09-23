<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { itemArtUrl } from '@/assets/itemArt'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName, IconCategory, Rarity } from '@/ui/icons/icon.types'

const props = withDefaults(defineProps<{
  iconId?: string | null
  itemId: string
  name: string
  type: string
  equipmentSlot?: string | null
  rarity?: string | null
  loading?: 'lazy' | 'eager'
  decorative?: boolean
}>(), {
  iconId: null,
  equipmentSlot: null,
  rarity: null,
  loading: 'lazy',
  decorative: false,
})

const failedToLoad = ref(false)
const source = computed(() => failedToLoad.value ? undefined : itemArtUrl(props.iconId))
const rarity = computed<Rarity | undefined>(() => {
  const value = props.rarity?.toLowerCase()
  return value === 'common' || value === 'uncommon' || value === 'rare'
    || value === 'epic' || value === 'legendary' || value === 'unique'
    ? value
    : undefined
})
const fallbackGlyph = computed<GlyphName>(() => {
  if (props.type === 'SpatialArtifact') return 'ring'
  if (props.type === 'Material') return 'ore'
  if (props.type === 'Consumable') return 'potion'
  if (props.equipmentSlot === 'Weapon' || props.equipmentSlot === 'MainHand') return 'sword'
  if (props.equipmentSlot === 'OffHand') return 'shield'
  if (props.equipmentSlot === 'Head') return 'helmet'
  if (props.equipmentSlot === 'Chest' || props.equipmentSlot === 'Shoulders' || props.equipmentSlot === 'Hands'
    || props.equipmentSlot === 'Legs' || props.equipmentSlot === 'Waist' || props.equipmentSlot === 'Wrist') return 'armor'
  if (props.equipmentSlot === 'Boots' || props.equipmentSlot === 'Feet') return 'boots'
  if (props.equipmentSlot === 'Cloak') return 'scroll'
  if (props.equipmentSlot === 'Amulet' || props.equipmentSlot === 'Ring1' || props.equipmentSlot === 'Ring2') return 'ring'
  return 'star'
})
const fallbackCategory = computed<IconCategory>(() => {
  if (props.type === 'Consumable') return 'consumable'
  if (props.type === 'Material') return 'resource'
  return 'equipment'
})

watch(() => props.iconId, () => { failedToLoad.value = false })
</script>

<template>
  <span class="item-icon" :data-icon-id="iconId ?? undefined" :data-equipment-slot="equipmentSlot ?? undefined">
    <img
      v-if="source"
      class="item-icon__image"
      :src="source"
      :alt="decorative ? '' : name"
      :loading="loading"
      decoding="async"
      @error="failedToLoad = true"
    />
    <IconGenerator
      v-else
      class="item-icon__fallback"
      :config="{ id: `item-${itemId}`, glyph: fallbackGlyph, category: fallbackCategory, rarity }"
      :label="decorative ? undefined : name"
    />
    <IconGenerator
      v-if="type === 'Equipment' && equipmentSlot"
      class="item-icon__slot-badge"
      :config="{ id: `slot-${itemId}`, glyph: fallbackGlyph, category: 'equipment' }"
    />
  </span>
</template>

<style scoped>
.item-icon { position: relative; display: inline-grid; width: 100%; height: 100%; }
.item-icon__image, .item-icon__fallback { width: 100%; height: 100%; min-width: 0; min-height: 0; object-fit: contain; }
.item-icon__slot-badge { display: none; }
</style>
