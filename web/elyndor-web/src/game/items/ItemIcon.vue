<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { resolveItemArtUrl } from '@/assets/itemArt'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName, IconConfig } from '@/ui/icons/icon.types'

const props = withDefaults(defineProps<{
  iconId?: string | null
  itemId: string
  name: string
  type?: string | null
  slot?: string | null
  weaponCategory?: string | null
  rarity?: string | null
  loading?: 'lazy' | 'eager'
}>(), {
  iconId: null,
  type: null,
  slot: null,
  weaponCategory: null,
  rarity: null,
  loading: 'lazy',
})

const imageFailed = ref(false)
const source = computed(() => imageFailed.value ? undefined : resolveItemArtUrl(props.iconId))

watch(() => props.iconId, () => {
  imageFailed.value = false
})

function fallbackGlyph(): GlyphName {
  if (props.type === 'SpatialArtifact') return 'ring'
  if (props.type === 'Material') return 'ore'
  if (props.type === 'Consumable') return 'potion'

  if (props.weaponCategory === 'BOW' || props.weaponCategory === 'CROSSBOW') return 'bow'
  if (props.weaponCategory === 'STAFF' || props.weaponCategory === 'WAND') return 'staff'
  if (props.weaponCategory === 'DAGGER') return 'dagger'
  if (props.weaponCategory === 'AXE' || props.weaponCategory === 'TWO_HAND_AXE') return 'axe'
  if (props.slot === 'OffHand') return 'shield'
  if (props.slot === 'Head') return 'helmet'
  if (props.slot === 'Chest' || props.slot === 'Shoulders' || props.slot === 'Hands'
    || props.slot === 'Legs' || props.slot === 'Waist' || props.slot === 'Wrist') return 'armor'
  if (props.slot === 'Feet' || props.slot === 'Boots') return 'boots'
  if (props.slot === 'Cloak') return 'scroll'
  if (props.slot === 'Amulet' || props.slot === 'Accessory'
    || props.slot === 'Ring1' || props.slot === 'Ring2') return 'ring'
  if (props.type === 'Equipment' || props.slot === 'Weapon' || props.slot === 'MainHand') return 'sword'
  return 'star'
}

const fallbackConfig = computed<IconConfig>(() => {
  const rarity = props.rarity?.toLowerCase()
  const normalizedRarity = rarity === 'uncommon' || rarity === 'rare' || rarity === 'epic'
    || rarity === 'legendary' || rarity === 'unique' || rarity === 'common'
    ? rarity
    : undefined

  return {
    id: `item-${props.itemId}`,
    glyph: fallbackGlyph(),
    category: props.type === 'Consumable'
      ? 'consumable'
      : props.type === 'Material'
        ? 'resource'
        : props.type === 'Equipment' || props.type === 'SpatialArtifact'
          ? 'equipment'
          : 'utility',
    rarity: normalizedRarity,
  }
})

function handleImageError(): void {
  imageFailed.value = true
}
</script>

<template>
  <span
    class="item-icon"
    :data-icon-id="iconId ?? undefined"
    :data-item-icon-fallback="source ? undefined : 'true'"
  >
    <img
      v-if="source"
      :src="source"
      :alt="name"
      :loading="loading"
      decoding="async"
      @error="handleImageError"
    />
    <IconGenerator v-else :config="fallbackConfig" :label="name" />
  </span>
</template>

<style scoped>
.item-icon {
  display: grid;
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  place-items: center;
}

.item-icon img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.item-icon :deep(.icon-generator) {
  width: 100%;
  height: 100%;
}
</style>
