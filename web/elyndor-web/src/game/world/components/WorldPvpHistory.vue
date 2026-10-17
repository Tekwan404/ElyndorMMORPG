<script setup lang="ts">
import MoneyAmount from '@/ui/components/MoneyAmount.vue'
import type { WorldPvpHistoryEntry } from '../useWorldPvpProgression'
defineProps<{ entries: WorldPvpHistoryEntry[] }>()
</script>
<template>
  <section class="war-history" aria-label="История PvP" data-world-pvp-history>
    <h4>Летопись сражений</h4>
    <p v-if="!entries.length">В этом сезоне ещё нет записей о боях.</p>
    <ol v-else>
      <li v-for="entry in entries" :key="entry.sessionId + ':' + entry.targetName + ':' + entry.defeat">
        <header><strong>{{ entry.defeat ? 'Поражение' : entry.victory === true ? 'Победа' : entry.victory === false ? 'Помощь' : 'Участие' }}</strong><time :datetime="entry.atUtc">{{ new Date(entry.atUtc).toLocaleString('ru-RU') }}</time></header>
        <b>{{ entry.targetName }}</b>
        <small v-if="entry.defeat">Честь сохранена · серия убийств прервана</small>
        <small v-else>{{ entry.targetDefeatNumber === 1 ? 'Первая зачётная встреча с целью сегодня' : 'Зачётная встреча с целью сегодня: ' + entry.targetDefeatNumber }}</small>
        <span>+{{ entry.honor }} чести</span>
        <MoneyAmount v-if="entry.goldEarned > 0" :amount="entry.goldEarned" />
        <small v-if="entry.bloodyTokens">+{{ entry.bloodyTokens }} кровавый жетон</small>
      </li>
    </ol>
  </section>
</template>
<style scoped>
.war-history h4 { color: #efd4a4; margin: 0 0 10px; }
.war-history ol { display: grid; gap: 8px; padding: 0; list-style: none; }
.war-history li { display: grid; gap: 6px; padding: 12px; border-left: 2px solid #a8874f; background: #171b21; border-radius: 5px; font-size: .82rem; }
.war-history header { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px; }
.war-history time, .war-history small, .war-history p { color: #b6aa96; font-size: .73rem; }
.war-history span { color: #e9c37b; }
</style>
