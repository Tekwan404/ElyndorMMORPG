<script setup lang="ts">
import { computed, shallowRef } from 'vue'
import type { WorldPvpRank } from '../useWorldPvpProgression'

const props = defineProps<{
  leaders: WorldPvpRank[]
  self: WorldPvpRank
  race: string
  disabled: boolean
}>()
const emit = defineEmits<{ filter: [race: string] }>()
const expanded = shallowRef(false)
const visibleLeaders = computed(() => expanded.value ? props.leaders : props.leaders.slice(0, 10))
const selfListed = computed(() => props.leaders.some(player => player.characterId === props.self.characterId))
const raceLabel = (race: string) => race === 'UNDEAD' ? 'Нежить' : 'Люди'

function updateRace(event: Event): void {
  emit('filter', (event.target as HTMLSelectElement).value)
}
</script>

<template>
  <section class="war-ranking" aria-label="Сезонная честь World PvP" data-world-pvp-ranking>
    <div class="war-ranking__heading">
      <div>
        <small>ЛЕТОПИСЬ ВОИНОВ</small>
        <h4>Слава открытого мира</h4>
      </div>
      <label class="war-ranking__filter">
        <span>Фракция</span>
        <select :value="race" :disabled="disabled" @change="updateRace">
          <option value="">Все</option>
          <option value="HUMAN">Люди</option>
          <option value="UNDEAD">Нежить</option>
        </select>
      </label>
    </div>
    <ol v-if="leaders.length" class="war-ranking__list">
      <li v-for="(leader, i) in visibleLeaders" :key="leader.characterId"
        class="war-ranking__entry" :data-self="leader.characterId === self.characterId">
        <span class="war-ranking__place" aria-label="Место">{{ i + 1 }}</span>
        <div class="war-ranking__identity">
          <strong>{{ leader.name }} <span v-if="leader.characterId === self.characterId">(ты)</span></strong>
          <small>{{ leader.rank?.name }} · {{ raceLabel(leader.raceId) }} · {{ leader.victories }} побед · {{ leader.assists }} помощи · {{ leader.deaths }} смертей</small>
        </div>
        <b>{{ leader.seasonHonor }} <small>чести</small></b>
      </li>
    </ol>
    <p v-else class="war-ranking__empty" role="status">
      В выбранной фракции ещё нет участников с сезонной честью.
    </p>
    <button v-if="leaders.length > 10" type="button" class="war-ranking__more"
      :aria-expanded="expanded" @click="expanded = !expanded">
      {{ expanded ? 'Свернуть список' : `Показать всех ( ${leaders.length} )` }}
    </button>
    <p v-if="!selfListed && self.seasonHonor > 0" class="war-ranking__note">
      Твоя честь — {{ self.seasonHonor }}. Ты вне отображаемого списка этой фракции или топ-50.
    </p>
  </section>
</template>

<style scoped>
.war-ranking { display: grid; gap: 10px; min-width: 0; }
.war-ranking__heading { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 10px; }
.war-ranking__heading > div { min-width: 0; }
.war-ranking__heading small { color: #c6a46f; font-size: .65rem; font-weight: 800; letter-spacing: .1em; }
.war-ranking h4 { margin: 4px 0 0; color: #f0e2c5; font-size: 1rem; }
.war-ranking__filter { display: grid; gap: 3px; min-width: 115px; color: #c1b7a3; font-size: .7rem; }
.war-ranking__filter select, .war-ranking__more {
  min-height: 44px; padding: 8px 10px; border: 1px solid #715b3d; border-radius: 8px;
  background: #221e19; color: #f0d8af; font: inherit;
}
.war-ranking__list { display: grid; gap: 6px; padding: 0; margin: 0; list-style: none; }
.war-ranking__entry {
  display: flex; align-items: center; gap: 10px; min-width: 0;
  padding: 10px; border: 1px solid rgb(176 142 85 / 18%); border-radius: 10px;
  background: rgb(8 13 18 / 55%);
}
.war-ranking__entry[data-self='true'] { border-color: #c1a26d; background: rgb(115 82 42 / 22%); }
.war-ranking__place { display: grid; width: 30px; height: 32px; flex: 0 0 30px; place-items: center; border-radius: 7px; background: #373028; color: #d9bb83; font-weight: 800; }
.war-ranking__entry:first-child .war-ranking__place { background: #6a4d22; color: #fff1b9; }
.war-ranking__identity { display: grid; flex: 1 1 auto; gap: 3px; min-width: 0; }
.war-ranking__identity strong { overflow-wrap: anywhere; color: #f0e5d2; font-size: .82rem; }
.war-ranking__identity strong span { color: #d4b67e; font-size: .7rem; font-weight: 400; }
.war-ranking__identity small { color: #a99f8d; font-size: .7rem; line-height: 1.3; }
.war-ranking__entry > b { flex: 0 0 auto; color: #f0c77d; font-size: 1rem; font-variant-numeric: tabular-nums; }
.war-ranking__entry > b small { color: #aa987e; font-size: .65rem; }
.war-ranking__more { width: 100%; cursor: pointer; }
.war-ranking__more:focus-visible, .war-ranking select:focus-visible { outline: 2px solid #e6c481; outline-offset: 2px; }
.war-ranking__empty, .war-ranking__note { margin: 0; padding: 12px; color: #b5a791; font-size: .8rem; line-height: 1.45; }
</style>
