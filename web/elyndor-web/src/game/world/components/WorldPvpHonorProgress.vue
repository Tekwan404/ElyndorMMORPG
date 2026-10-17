<script setup lang="ts">
import { computed } from 'vue'
import type { WorldPvpRank } from '../useWorldPvpProgression'
const props = defineProps<{ self: WorldPvpRank }>()
const progress = computed(() => {
  const end = props.self.nextRank?.requiredHonor
  return end === undefined ? 100 : Math.max(0, Math.min(100, props.self.seasonHonor * 100 / end))
})
const roman = ['I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII', 'IX', 'X', 'XI', 'XII', 'XIII', 'XIV']
</script>
<template>
  <section v-if="self.rank" class="honor-rank" aria-label="Воинское звание" data-honor-rank>
    <small>ВОИНСКАЯ СЛАВА · СЕЗОННАЯ ЧЕСТЬ</small>
    <h4>{{ roman[self.rank.level - 1] }} · {{ self.rank.name }}</h4>
    <p v-if="self.nextRank">Следующее: {{ roman[self.nextRank.level - 1] }} · {{ self.nextRank.name }}</p>
    <p v-else>Высшее воинское звание достигнуто</p>
    <div><strong>{{ self.seasonHonor.toLocaleString('ru-RU') }}<template v-if="self.nextRank"> / {{ self.nextRank.requiredHonor.toLocaleString('ru-RU') }}</template></strong><span>{{ Math.floor(progress) }}%</span></div>
    <progress :value="progress" max="100" :aria-label="'Прогресс звания: ' + Math.floor(progress) + '%'" />
    <small>Смерть и покупки не уменьшают заработанную честь.</small>
  </section>
</template>
<style scoped>
.honor-rank { display: grid; gap: 9px; padding: 16px; border: 1px solid #9d7a41; border-radius: 10px; background: linear-gradient(130deg, #332719, #14191e); color: #e4c995; }
.honor-rank h4 { margin: 0; font-size: 1.25rem; color: #f5dfb2; }
.honor-rank p { margin: 0; font-size: .8rem; color: #c4b49b; }
.honor-rank small { font-size: .68rem; line-height: 1.5; }
.honor-rank div { display: flex; justify-content: space-between; gap: 8px; font-variant-numeric: tabular-nums; }
.honor-rank progress { width: 100%; height: 9px; accent-color: #d5ad64; }
</style>
