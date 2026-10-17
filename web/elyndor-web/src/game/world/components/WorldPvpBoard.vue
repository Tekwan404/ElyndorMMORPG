<script setup lang="ts">
import { computed, shallowRef } from 'vue'
import MoneyAmount from '@/ui/components/MoneyAmount.vue'
import { useGameSessionStore } from '@/stores/gameSession'
import WorldPvpHonorProgress from './WorldPvpHonorProgress.vue'
import WorldPvpHistory from './WorldPvpHistory.vue'
import WorldPvpLeaderboard from './WorldPvpLeaderboard.vue'
import WorldPvpRewardShop from './WorldPvpRewardShop.vue'
import { useWorldPvpProgression } from '../useWorldPvpProgression'

const session = useGameSessionStore()
const { overview, offers, race, loading, buying, error, shopError, message, refresh, buy } =
  useWorldPvpProgression()
const level = computed(() => session.snapshot?.character?.level ?? 0)
const panel = shallowRef('overview')
const tabs = [
  { id: 'overview', label: 'Обзор' }, { id: 'rating', label: 'Слава' },
  { id: 'rewards', label: 'Награды' }, { id: 'bounties', label: 'Розыск' },
  { id: 'titles', label: 'Звания' }, { id: 'history', label: 'История' },
]

function changeRace(value: string): void {
  race.value = value
  void refresh()
}
</script>

<template>
  <section class="war-board" aria-label="Война фракций" data-world-pvp data-world-pvp-board :aria-busy="loading">
    <header class="war-board__header">
      <div>
        <small>ГИЛЬДИЯ АВАНТЮРИСТОВ · ОТКРЫТЫЙ МИР</small>
        <h3>Война фракций</h3>
        <p>Мёртвые Пределы · Уровень 60 · Отдельно от арены</p>
      </div>
      <button type="button" :disabled="loading || buying" @click="refresh">
        {{ loading ? 'Обновляем…' : 'Обновить' }}
      </button>
    </header>
    <nav class="war-board__tabs" aria-label="Разделы войны фракций">
      <button v-for="tab in tabs" :key="tab.id" type="button" :data-world-pvp-tab="tab.id"
        :aria-pressed="panel === tab.id" @click="panel = tab.id">{{ tab.label }}</button>
    </nav>
    <p v-if="panel === 'bounties'" class="war-board__feedback" data-world-pvp-bounties-empty>Розыск ещё не открыт. Сервер пока не публикует награды за головы.</p>


    <p v-if="error" class="war-board__feedback war-board__feedback--error" role="alert">{{ error }}</p>
    <p v-if="message" class="war-board__feedback war-board__feedback--success" role="status">{{ message }}</p>
    <p v-if="loading && !overview" class="war-board__feedback" role="status">Загружаем реестр войны…</p>

    <template v-if="overview">
      <div class="war-board__season">
        <span>Сезон {{ overview.season }}</span>
        <small>Завершится {{ new Date(overview.endsAtUtc).toLocaleDateString('ru-RU') }}</small>
      </div>
      <WorldPvpHonorProgress :self="overview.self" />
      <div class="war-board__stats" aria-label="Твоя статистика PvP">
        <div><small>Честь за сезон</small><strong>{{ overview.self.seasonHonor }}</strong></div>
        <div><small>Победы</small><strong>{{ overview.self.victories }}</strong></div>
        <div><small>Помощь</small><strong>{{ overview.self.assists }}</strong></div>
        <div><small>Смерти</small><strong>{{ overview.self.deaths }}</strong></div>
      </div>
      <div class="war-board__balances" aria-label="Валюта войны">
        <p>За убийство<MoneyAmount :amount="overview.rewards?.goldPerKill ?? 0" /></p>
        <p>За помощь<MoneyAmount :amount="overview.rewards?.goldPerAssist ?? 0" /></p>
        <p>Полевая честь: <strong>{{ overview.fieldHonor }}</strong></p>
        <p>Кровавые жетоны: <strong>{{ overview.bloodyTokens }}</strong></p>
      </div>
      <div class="war-board__limit">
        <span>Выплат золота / жетонов сегодня</span>
        <strong>{{ overview.rewardedToday }} / {{ overview.dailyLimit }}</strong>
        <progress :value="overview.rewardedToday" :max="overview.dailyLimit || 1"
          :aria-label="'Экономические награды за сегодня: ' + overview.rewardedToday + ' из ' + overview.dailyLimit" />
      </div>

      <WorldPvpLeaderboard v-if="panel === 'overview' || panel === 'rating'" :leaders="overview.leaders" :self="overview.self" :race="race"
        :disabled="loading || buying" @filter="changeRace" />

      <template v-if="panel === 'overview' || panel === 'rewards'">
      <div class="war-board__divider" role="separator" />
      <p v-if="shopError" class="war-board__feedback war-board__feedback--error" role="alert">{{ shopError }}</p>
      <WorldPvpRewardShop v-else :offers="offers" :field-honor="overview.fieldHonor"
        :bloody-tokens="overview.bloodyTokens" :character-level="level"
        :loading="loading" :buying="buying" @buy="buy" />
      </template>

      <section v-if="panel === 'titles'" aria-label="Воинские звания" class="war-board__ranks">
        <h4>Четырнадцать ступеней славы</h4>
        <ol><li v-for="rank in overview.ranks ?? []" :key="rank.level" :data-unlocked="overview.self.seasonHonor >= rank.requiredHonor">
          <strong>{{ rank.level }} · {{ rank.name }}</strong><span>{{ rank.requiredHonor.toLocaleString('ru-RU') }} чести</span>
          <small>{{ overview.self.rank?.level === rank.level ? 'Текущее звание' : overview.self.seasonHonor >= rank.requiredHonor ? 'Достигнуто' : 'Не открыто' }}</small>
        </li></ol>
      </section>
      <WorldPvpHistory v-if="panel === 'history'" :entries="overview.history ?? []" />
      <WorldPvpHistory v-if="panel === 'overview' && overview.history?.length" :entries="overview.history.slice(0, 1)" />
      <details class="war-board__rules">
        <summary>Как начисляются честь и награды?</summary>
        <p>Честь за сезон определяет звание и место в таблице славы. Смерть и покупки её не уменьшают.
          Полевая честь — отдельная расходуемая валюта магазина. Арена сохраняет свой рейтинг.</p>
        <p>Повторные зачётные встречи с одной целью за сутки дают 100%, 75%, 50%, 25%, затем 0% чести.
          Счётчик направленный: месть приносит полную честь. Сброс в 00:00 UTC.
          Помощь приносит уменьшенную награду. Общего дневного лимита чести нет.</p>
        <p>Золото и жетоны выдаются только при первой зачётной встрече с целью за сутки,
          не более {{ overview.dailyLimit }} экономических выплат в день. Жетон получает основной победитель.
          Победы, помощь и смерти учитываются даже при нулевой награде.</p>
      </details>
    </template>
  </section>
</template>

<style scoped>
.war-board__ranks ol { list-style: none; padding: 0; display: grid; gap: 7px; }
.war-board__ranks li { display: grid; gap: 5px; padding: 10px; border: 1px solid #514738; border-radius: 6px; font-size: .8rem; color: #9c9485; }
.war-board__ranks li[data-unlocked='true'] { color: #e6c88f; border-color: #a37e42; }
.war-board__ranks small { font-size: .7rem; }

.war-board__tabs { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .35rem; }
.war-board__tabs button { min-height: 44px; padding: .4rem; color: #c9bca4; background: #1c1b1a; border: 1px solid #4b4336; border-radius: 7px; font: inherit; font-size: .78rem; }
.war-board__tabs button[aria-pressed='true'] { color: #f4ddaa; border-color: #b08d51; background: #33281b; }
.war-board__balances .money-amount { font-size: 1.3rem; font-weight: 700; }
.war-board {
  display: grid;
  min-width: 0;
  width: 100%;
  gap: 14px;
  color: #e8dcc6;
}
.war-board__header { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-start; gap: 12px; }
.war-board__header > div { flex: 1 1 200px; min-width: 0; }
.war-board__header small { color: #d5b274; font-size: .65rem; font-weight: 800; letter-spacing: .1em; }
.war-board h3 { margin: 5px 0 3px; color: #f4e6ce; font-size: clamp(1.15rem, 4.5vw, 1.55rem); }
.war-board__header p { margin: 0; color: #b9ae9d; font-size: .8rem; line-height: 1.45; }
.war-board__header button {
  min-height: 44px; padding: 8px 12px; border: 1px solid #88704b; border-radius: 9px;
  background: #2b241c; color: #f2daae; font: inherit; font-size: .8rem; cursor: pointer;
}
.war-board button:disabled { opacity: .5; cursor: not-allowed; }
.war-board button:focus-visible, .war-board summary:focus-visible { outline: 2px solid #e6c481; outline-offset: 2px; }
.war-board__feedback { margin: 0; padding: 10px; border: 1px solid #554b37; border-radius: 9px; color: #dbccb4; font-size: .8rem; line-height: 1.45; }
.war-board__feedback--error { border-color: #92544a; color: #ffc7b5; }
.war-board__feedback--success { border-color: #5f8061; color: #c9e5bc; }
.war-board__season { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: baseline; gap: 4px 12px; color: #d7b878; font-size: .82rem; }
.war-board__season span { font-weight: 800; }
.war-board__season small { color: #bdb19e; font-size: .75rem; }
.war-board__stats { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 6px; }
.war-board__stats > div {
  display: grid; gap: 5px; min-width: 0; padding: 10px 5px; text-align: center;
  border: 1px solid rgb(186 153 97 / 30%); border-radius: 9px; background: #191c22;
}
.war-board__stats small { color: #bcb19c; font-size: clamp(.65rem, 2.5vw, .75rem); }
.war-board__stats strong { color: #f7e5c7; font-size: clamp(1.1rem, 5vw, 1.4rem); font-variant-numeric: tabular-nums; }
.war-board__balances { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
.war-board__balances p {
  display: grid; gap: 3px; margin: 0; padding: 11px; border-radius: 9px;
  border: 1px solid rgb(195 156 96 / 24%); background: rgb(128 89 43 / 13%);
  color: #d6bc91; font-size: .76rem; line-height: 1.3;
}
.war-board__balances strong { color: #f8d795; font-size: 1.15rem; font-variant-numeric: tabular-nums; }
.war-board__limit { display: grid; grid-template-columns: 1fr auto; align-items: center; gap: 7px; color: #ac9e8a; font-size: .76rem; }
.war-board__limit strong { color: #dac093; font-variant-numeric: tabular-nums; }
.war-board__limit progress { grid-column: 1 / -1; width: 100%; height: 7px; accent-color: #c9a266; }
.war-board__divider { height: 1px; background: linear-gradient(90deg, transparent, #7e6242, transparent); }
.war-board__rules { padding: 10px 12px; border: 1px solid rgb(189 152 95 / 25%); border-radius: 10px; color: #bfb19d; font-size: .77rem; line-height: 1.55; }
.war-board__rules summary { min-height: 32px; color: #d9bd8d; cursor: pointer; font-weight: 700; }
.war-board__rules p { margin: 9px 0 0; }
@media (max-width: 360px) {
  .war-board__balances { grid-template-columns: 1fr; }
}
</style>
