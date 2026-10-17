<script setup lang="ts">
import { computed, ref } from 'vue'

import {
  type WorldPvpDashboard,
  type WorldPvpFaction,
  type WorldPvpRewardCategory,
  worldPvpFactionLabel,
  worldPvpSeasonLabel,
} from './worldPvpContracts'

type Panel = 'overview' | 'rating' | 'rewards' | 'bounties' | 'titles'
type RatingScope = 'all' | WorldPvpFaction
type RewardFilter = 'all' | WorldPvpRewardCategory

const props = withDefaults(defineProps<{
  dashboard?: WorldPvpDashboard | null
  loading?: boolean
  error?: string | null
  preview?: boolean
}>(), {
  dashboard: null,
  loading: false,
  error: null,
  preview: false,
})

const emit = defineEmits<{ refresh: [] }>()
const panel = ref<Panel>('overview')
const ratingScope = ref<RatingScope>('all')
const rewardFilter = ref<RewardFilter>('all')
const sectionTabs: { id: Panel; label: string; shortLabel: string }[] = [
  { id: 'overview', label: 'Обзор', shortLabel: 'Обзор' },
  { id: 'rating', label: 'Рейтинг', shortLabel: 'Рейтинг' },
  { id: 'rewards', label: 'Награды', shortLabel: 'Награды' },
  { id: 'bounties', label: 'Награды за головы', shortLabel: 'Розыск' },
  { id: 'titles', label: 'Титулы', shortLabel: 'Титулы' },
]
const ratingScopes: { id: RatingScope; label: string }[] = [
  { id: 'all', label: 'Все' },
  { id: 'HUMAN', label: 'Люди' },
  { id: 'UNDEAD', label: 'Нежить' },
]
const rewardFilters: { id: RewardFilter; label: string }[] = [
  { id: 'all', label: 'Все' },
  { id: 'Equipment', label: 'Снаряжение' },
  { id: 'Cosmetic', label: 'Облики' },
  { id: 'Collectible', label: 'Трофеи' },
]
const personal = computed(() => props.dashboard?.personal ?? null)
const standings = computed(() =>
  [...(props.dashboard?.standings ?? [])]
    .filter((entry) => ratingScope.value === 'all' || entry.faction === ratingScope.value)
    .sort((left, right) => left.rank - right.rank),
)
const rewards = computed(() =>
  (props.dashboard?.rewards ?? []).filter(
    (entry) => rewardFilter.value === 'all' || entry.category === rewardFilter.value,
  ),
)
const bounties = computed(() => {
  const ownFaction = personal.value?.faction
  return (props.dashboard?.bounties ?? [])
    .filter((entry) => !ownFaction || entry.targetFaction !== ownFaction)
    .sort((left, right) => right.killStreak - left.killStreak)
})
const titles = computed(() => props.dashboard?.titles ?? [])
const seasonLabel = computed(() =>
  props.dashboard ? worldPvpSeasonLabel(props.dashboard.season.status) : 'Скоро',
)
const formatNumber = (value: number): string => new Intl.NumberFormat('ru-RU').format(value)
const rewardCurrencyLabel = (currency: 'BloodTokens' | 'FieldHonor'): string =>
  currency === 'BloodTokens' ? 'кровавых жетонов' : 'полевой чести'
</script>

<template>
  <section class="world-pvp" aria-label="Война фракций" data-world-pvp>
    <header class="war-head">
      <div class="war-head__eyebrow"><span class="war-head__sigil" aria-hidden="true">✦</span> ELYNDOR · WORLD PvP</div>
      <div class="war-head__title">
        <div>
          <h2>Война фракций</h2>
          <p>Слава, охота за головами и трофеи открытого мира. Отдельно от арены.</p>
        </div>
        <span class="war-head__status" data-world-pvp-season-status>{{ seasonLabel }}</span>
      </div>
      <div class="war-head__factions" aria-label="Противоборствующие фракции">
        <span class="war-head__human">ЛЮДИ</span>
        <span class="war-head__versus">VS</span>
        <span class="war-head__undead">НЕЖИТЬ</span>
      </div>
    </header>

    <p v-if="preview" class="war-preview" role="note" data-world-pvp-preview>
      ДЕМО ИНТЕРФЕЙСА · Все персонажи, балансы, цены и результаты вымышлены. Покупки и награды не выполняются.
    </p>

    <p v-if="error" class="war-alert" role="alert" data-world-pvp-error>
      {{ error }}
      <button type="button" @click="emit('refresh')">Повторить</button>
    </p>

    <nav class="war-tabs" role="tablist" aria-label="Разделы World PvP">
      <button
        v-for="tab in sectionTabs"
        :key="tab.id"
        :id="`war-tab-${tab.id}`"
        type="button"
        role="tab"
        :aria-selected="panel === tab.id"
        :aria-controls="`war-panel-${tab.id}`"
        :class="{ 'is-active': panel === tab.id }"
        :data-world-pvp-tab="tab.id"
        @click="panel = tab.id"
      >{{ tab.shortLabel }}</button>
    </nav>

    <div
      v-if="panel === 'overview'"
      id="war-panel-overview"
      role="tabpanel"
      aria-labelledby="war-tab-overview"
      class="war-section"
      data-world-pvp-panel="overview"
    >
      <div class="war-section__lead">
        <div>
          <small>ЛИЧНОЕ ДОСЬЕ</small>
          <h3>Путь завоевателя</h3>
        </div>
        <span class="war-season">{{ dashboard?.season.name ?? 'Первый сезон впереди' }}</span>
      </div>
      <div v-if="personal" class="war-metrics">
        <article class="war-metric"><span>Очки World PvP</span><strong>{{ formatNumber(personal.score) }}</strong><small>Не рейтинг арены</small></article>
        <article class="war-metric"><span>Место в мире</span><strong>{{ personal.rank === null ? '—' : `#${personal.rank}` }}</strong><small>{{ worldPvpFactionLabel(personal.faction) }}</small></article>
        <article class="war-metric"><span>Победы / помощь</span><strong>{{ personal.kills }} / {{ personal.assists }}</strong><small>Открытый мир</small></article>
        <article class="war-metric"><span>Серия убийств</span><strong>{{ personal.currentStreak }}</strong><small>Лучшая: {{ personal.bestStreak }}</small></article>
      </div>
      <div v-else class="war-empty" data-world-pvp-overview-empty>
        <span class="war-empty__symbol" aria-hidden="true">⚔</span>
        <strong>Твоя военная история ещё не началась</strong>
        <p>Рейтинг, статистика побед и валюта появятся здесь после подключения серверной системы World PvP.</p>
      </div>
      <div class="war-feature-list">
        <article><span class="war-feature-list__symbol" aria-hidden="true">Ⅰ</span><div><strong>Побеждай в открытом мире</strong><p>Полевые победы и вклад в бой формируют отдельные очки World PvP.</p></div></article>
        <article><span class="war-feature-list__symbol" aria-hidden="true">Ⅱ</span><div><strong>Зарабатывай трофеи</strong><p>Полевая честь и кровавые жетоны — отдельная экономика наград.</p></div></article>
        <article><span class="war-feature-list__symbol" aria-hidden="true">Ⅲ</span><div><strong>Стань легендой континента</strong><p>Серии убийств, розыск и награды за голову соперников.</p></div></article>
      </div>
    </div>

    <div
      v-else-if="panel === 'rating'"
      id="war-panel-rating"
      role="tabpanel"
      aria-labelledby="war-tab-rating"
      class="war-section"
      data-world-pvp-panel="rating"
    >
      <div class="war-section__lead">
        <div><small>ТОЛЬКО ОТКРЫТЫЙ МИР</small><h3>Рейтинг завоевателей</h3></div>
        <span class="war-season">Арена не учитывается</span>
      </div>
      <div class="war-filter" role="group" aria-label="Фильтр рейтинга">
        <button v-for="scope in ratingScopes" :key="scope.id" type="button"
          :aria-pressed="ratingScope === scope.id" :class="{ 'is-active': ratingScope === scope.id }"
          :data-world-pvp-rating-scope="scope.id" @click="ratingScope = scope.id">{{ scope.label }}</button>
      </div>
      <div v-if="loading && !dashboard" class="war-empty" role="status">Загружаем рейтинг World PvP…</div>
      <ol v-else-if="standings.length" class="war-board" aria-label="Независимый рейтинг World PvP">
        <li v-for="entry in standings" :key="entry.characterId" class="war-board__row"
          :class="{ 'is-self': entry.characterId === personal?.characterId }"
          :data-world-pvp-standing="entry.characterId">
          <span class="war-board__rank">#{{ entry.rank }}</span>
          <div class="war-board__identity">
            <strong>{{ entry.name }}</strong>
            <small>{{ worldPvpFactionLabel(entry.faction) }} · {{ entry.kills }} побед · {{ entry.assists }} помощи · {{ entry.deaths }} смертей</small>
          </div>
          <span class="war-board__score">{{ formatNumber(entry.score) }} <small>очков</small></span>
        </li>
      </ol>
      <div v-else class="war-empty" data-world-pvp-rating-empty>
        <span class="war-empty__symbol" aria-hidden="true">♛</span>
        <strong>В рейтинге пока нет воинов</strong>
        <p>Здесь появятся участники World PvP. Данные арены не используются и не подставляются вместо результатов мира.</p>
      </div>
    </div>

    <div
      v-else-if="panel === 'rewards'"
      id="war-panel-rewards"
      role="tabpanel"
      aria-labelledby="war-tab-rewards"
      class="war-section"
      data-world-pvp-panel="rewards"
    >
      <div class="war-section__lead">
        <div><small>ВОЕННАЯ ЭКОНОМИКА</small><h3>Награды и трофеи</h3></div>
        <span class="war-season">Отдельно от магазина арены</span>
      </div>
      <div class="war-wallet">
        <div><span class="war-wallet__icon" aria-hidden="true">✥</span><span>Полевая честь<small>За участие в World PvP</small></span><strong>{{ personal ? formatNumber(personal.fieldHonor) : '—' }}</strong></div>
        <div><span class="war-wallet__icon war-wallet__icon--red" aria-hidden="true">◆</span><span>Кровавые жетоны<small>За полевые PvP-активности</small></span><strong>{{ personal ? formatNumber(personal.bloodTokens) : '—' }}</strong></div>
      </div>
      <div class="war-filter" role="group" aria-label="Категории наград">
        <button v-for="filter in rewardFilters" :key="filter.id" type="button"
          :aria-pressed="rewardFilter === filter.id" :class="{ 'is-active': rewardFilter === filter.id }"
          :data-world-pvp-reward-filter="filter.id" @click="rewardFilter = filter.id">{{ filter.label }}</button>
      </div>
      <div v-if="rewards.length" class="war-rewards">
        <article v-for="reward in rewards" :key="reward.id" class="war-reward" :data-world-pvp-reward="reward.id">
          <div class="war-reward__crest" aria-hidden="true">{{ reward.category === 'Equipment' ? '⚔' : reward.category === 'Cosmetic' ? '✧' : '♜' }}</div>
          <div class="war-reward__copy"><strong>{{ reward.name }}</strong><p>{{ reward.description }}</p><small v-if="reward.requiredRank">Требуется место: топ-{{ reward.requiredRank }}</small></div>
          <div class="war-reward__footer"><b>{{ formatNumber(reward.cost) }} {{ rewardCurrencyLabel(reward.currency) }}</b>
            <span>{{ reward.owned ? 'Уже получено' : 'Покупка пока недоступна' }}</span>
          </div>
        </article>
      </div>
      <div v-else class="war-empty" data-world-pvp-rewards-empty>
        <span class="war-empty__symbol" aria-hidden="true">✥</span>
        <strong>{{ dashboard ? 'В этой категории наград пока нет' : 'Военный интендант готовит награды' }}</strong>
        <p>Список предметов, цены и доступность придут с сервера. Никаких покупок или списаний с баланса этот экран не выполняет.</p>
      </div>
    </div>

    <div
      v-else-if="panel === 'bounties'"
      id="war-panel-bounties"
      role="tabpanel"
      aria-labelledby="war-tab-bounties"
      class="war-section"
      data-world-pvp-panel="bounties"
    >
      <div class="war-section__lead">
        <div><small>ДОСКА РОЗЫСКА</small><h3>Охота за головами</h3></div>
        <span class="war-season">Цели вражеской фракции</span>
      </div>
      <p class="war-intro">Опасные противники будут появляться здесь после объявления розыска. Показана только приблизительная область, без точных координат.</p>
      <div v-if="bounties.length" class="war-bounties">
        <article v-for="bounty in bounties" :key="bounty.id" class="war-bounty" :data-world-pvp-bounty="bounty.id">
          <div class="war-bounty__head"><span aria-hidden="true">☠</span><div><strong>{{ bounty.targetName }}</strong><small>{{ worldPvpFactionLabel(bounty.targetFaction) }} · серия {{ bounty.killStreak }}</small></div></div>
          <p>{{ bounty.lastSeenLocationName ? `Последняя известная область: ${bounty.lastSeenLocationName}` : 'Местоположение неизвестно' }}</p>
          <div class="war-bounty__prize">Награда: <strong>{{ formatNumber(bounty.honorReward) }} чести · {{ bounty.tokenReward }} жетонов</strong></div>
          <small>Контракт будет доступен после подключения серверной охоты.</small>
        </article>
      </div>
      <div v-else class="war-empty" data-world-pvp-bounties-empty>
        <span class="war-empty__symbol" aria-hidden="true">☠</span>
        <strong>Никто не объявлен в розыск</strong>
        <p>Когда сервер откроет охоту за опасными игроками, здесь появятся доступные цели и награды за их устранение.</p>
      </div>
    </div>
    <div
      v-else
      id="war-panel-titles"
      role="tabpanel"
      aria-labelledby="war-tab-titles"
      class="war-section"
      data-world-pvp-panel="titles"
    >
      <div class="war-section__lead">
        <div><small>ЛЕТОПИСЬ ГИЛЬДИИ</small><h3>Воинские титулы</h3></div>
        <span class="war-season">Слава открытого мира</span>
      </div>
      <p class="war-intro">
        Титулы за победы, серии убийств и охоту за разыскиваемыми.
        Условия, прогресс и владение подтверждает только сервер.
      </p>
      <div v-if="titles.length" class="war-titles">
        <article v-for="title in titles" :key="title.id" class="war-title"
          :class="{ 'war-title--unlocked': title.unlocked }" :data-world-pvp-title="title.id">
          <span class="war-title__seal" aria-hidden="true">{{ title.unlocked ? '✦' : '♜' }}</span>
          <div class="war-title__copy">
            <strong>{{ title.name }}</strong>
            <p>{{ title.description }}</p>
            <small>{{ title.requirement }}</small>
            <div v-if="title.progress !== null && title.target !== null && title.target > 0"
              class="war-title__progress">
              <div class="war-title__progress-track" role="progressbar" :aria-label="`Прогресс: ${title.name}`"
                :aria-valuenow="Math.min(Math.max(title.progress, 0), title.target)"
                :aria-valuemin="0" :aria-valuemax="title.target">
                <span :style="{ width: `${Math.min(100, Math.max(0, (title.progress / title.target) * 100))}%` }" />
              </div>
              <small>{{ formatNumber(title.progress) }} / {{ formatNumber(title.target) }}</small>
            </div>
          </div>
          <span class="war-title__state">{{ title.equipped ? 'Выбран' : title.unlocked ? 'Получен' : 'Не открыт' }}</span>
        </article>
      </div>
      <div v-else class="war-empty" data-world-pvp-titles-empty>
        <span class="war-empty__symbol" aria-hidden="true">♜</span>
        <strong>Летопись титулов пока пуста</strong>
        <p>Когда гильдия получит подтверждённые награды от сервера, здесь появятся титулы и требования для их получения.</p>
      </div>
    </div>
    <footer class="war-footer">World PvP — самостоятельный режим. Рейтинг, валюта и награды арены не используются.</footer>
  </section>
</template>

<style scoped>
.world-pvp {
  display: grid;
  gap: 14px;
  min-width: 0;
  color: #e9e0d0;
  --war-gold: #d6b783;
  --war-muted: #a7a094;
  --war-border: rgb(205 167 107 / 24%);
  --war-panel: #131822;
}
.war-head {
  position: relative;
  overflow: hidden;
  display: grid;
  gap: 14px;
  padding: 20px 18px 15px;
  border: 1px solid var(--war-border);
  border-radius: 18px;
  background: radial-gradient(ellipse at 90% 10%, rgb(142 32 43 / 23%), transparent 50%), linear-gradient(140deg, #222633, #10141d 70%);
}
.war-head::before {
  content: '';
  position: absolute;
  top: -45px;
  right: -54px;
  width: 140px;
  height: 140px;
  border: 1px solid rgb(222 177 126 / 12%);
  border-radius: 50%;
  box-shadow: 0 0 0 28px rgb(222 177 126 / 3%), 0 0 0 55px rgb(222 177 126 / 3%);
  pointer-events: none;
}
.war-head__eyebrow {
  position: relative;
  color: var(--war-gold);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: .18em;
}
.war-head__sigil { color: #e3665f; margin-right: 6px; }
.war-head__title { position: relative; display: flex; align-items: start; justify-content: space-between; gap: 12px; }
.war-head h2 { margin: 0; font: 700 clamp(25px, 6vw, 36px)/1.06 Georgia, serif; color: #faedda; }
.war-head p { margin: 8px 0 0; max-width: 36ch; color: #c4b8a8; font-size: 12px; line-height: 1.5; }
.war-head__status {
  flex-shrink: 0;
  padding: 6px 8px;
  border: 1px solid rgb(227 159 111 / 42%);
  border-radius: 7px;
  background: rgb(100 37 32 / 36%);
  color: #ffc6a3;
  font-size: 10px;
  font-weight: 700;
}
.war-head__factions { position: relative; display: flex; align-items: center; gap: 8px; font: 800 11px/1.2 Georgia, serif; letter-spacing: .12em; }
.war-head__factions span { padding: 8px 12px; border-radius: 7px; }
.war-head__human { background: rgb(65 88 117 / 34%); color: #b9d6f5; border: 1px solid rgb(124 163 212 / 22%); }
.war-head__undead { background: rgb(96 66 111 / 32%); color: #d9bde7; border: 1px solid rgb(184 124 197 / 22%); }
.war-head__factions .war-head__versus { padding: 0; color: #9b6a58; font-size: 10px; }
.war-preview { margin: 0; padding: 10px 12px; border: 1px solid rgb(230 190 107 / 44%); border-radius: 9px; color: #f5dcb1; background: #39301b; font-size: 11px; line-height: 1.5; }
.war-alert { margin: 0; padding: 12px; color: #ffd4c8; background: #3f2226; border: 1px solid #ad5450; border-radius: 9px; font-size: 12px; }
.war-alert button { margin-left: 10px; border: 0; background: none; color: #ffe3ae; text-decoration: underline; cursor: pointer; }
.war-tabs { display: grid; grid-template-columns: repeat(5,minmax(0,1fr)); gap: 5px; padding: 4px; border: 1px solid var(--war-border); border-radius: 12px; background: #11151d; }
.war-tabs button { min-width: 0; min-height: 43px; padding: 8px 3px; border: 0; border-radius: 8px; color: #c0b8ac; background: transparent; font-size: 12px; font-weight: 700; line-height: 1.2; cursor: pointer; }
.war-tabs button.is-active { color: #201911; background: linear-gradient(135deg,#e6c999,#bd965e); box-shadow: 0 3px 12px rgb(0 0 0 / 26%); }
.war-tabs button:focus-visible, .war-filter button:focus-visible { outline: 2px solid #f3c78b; outline-offset: 2px; }
.war-section { display: grid; gap: 13px; min-width: 0; }
.war-section__lead { display: flex; align-items: end; justify-content: space-between; gap: 8px; }
.war-section__lead small { display: block; color: #b8946a; font-size: 10px; font-weight: 700; line-height: 1.4; letter-spacing: .12em; }
.war-section__lead h3 { margin: 2px 0 0; font: 700 21px/1.2 Georgia, serif; color: #e9d5b5; }
.war-season { max-width: 38%; text-align: right; font-size: 10px; line-height: 1.4; color: var(--war-muted); }
.war-metrics { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 9px; }
.war-metric { display: grid; gap: 4px; padding: 13px; border: 1px solid var(--war-border); border-radius: 12px; background: linear-gradient(160deg,#232632,#141922); }
.war-metric span { color: #c5bbae; font-size: 11px; }
.war-metric strong { color: #f4dab0; font: 700 24px/1.2 Georgia,serif; }
.war-metric small { color: var(--war-muted); font-size: 10px; }
.war-empty { display: grid; justify-items: center; gap: 9px; text-align: center; padding: 28px 18px; border: 1px dashed rgb(210 165 111 / 35%); border-radius: 14px; background: linear-gradient(140deg,#1e222c,#11161f); color: #d3c7b5; }
.war-empty__symbol { color: #bc8d6c; font: 700 35px/1 Georgia,serif; }
.war-empty strong { color: #ecdbc2; font: 700 17px/1.3 Georgia,serif; }
.war-empty p { max-width: 42ch; margin: 0; color: var(--war-muted); font-size: 12px; line-height: 1.55; }
.war-feature-list { display: grid; gap: 8px; }
.war-feature-list article { display: flex; align-items: start; gap: 12px; padding: 12px; border: 1px solid rgb(203 167 118 / 14%); border-radius: 11px; background: #171c25; }
.war-feature-list__symbol { display: grid; place-items: center; flex-shrink: 0; width: 35px; height: 35px; color: #e7c498; border: 1px solid rgb(221 173 105 / 26%); border-radius: 8px; background: #28242a; font: 700 16px Georgia,serif; }
.war-feature-list strong { color: #e7d7c2; font-size: 12px; }
.war-feature-list p { margin: 5px 0 0; color: var(--war-muted); font-size: 11px; line-height: 1.5; }
.war-filter { display: flex; gap: 6px; overflow-x: auto; padding: 1px 0 4px; }
.war-filter button { flex-shrink: 0; min-height: 35px; padding: 6px 12px; border: 1px solid var(--war-border); border-radius: 9px; background: #191e28; color: #cfc2b0; font-size: 11px; cursor: pointer; }
.war-filter button.is-active { color: #f8e6c3; border-color: #a77a4f; background: #443328; }
.war-board,.war-rewards,.war-bounties,.war-titles { display: grid; gap: 8px; margin: 0; padding: 0; list-style: none; }
.war-board__row { display: flex; align-items: center; gap: 10px; min-width: 0; padding: 12px 10px; border: 1px solid var(--war-border); border-radius: 10px; background: var(--war-panel); }
.war-board__row.is-self { border-color: #dfb575; background: #28251f; }
.war-board__rank { width: 40px; flex-shrink: 0; text-align: center; color: #f0c888; font: 700 18px Georgia,serif; }
.war-board__identity { min-width: 0; flex: 1; display: grid; gap: 5px; }
.war-board__identity strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: #efdec6; font-size: 13px; }
.war-board__identity small { color: var(--war-muted); font-size: 10px; line-height: 1.35; }
.war-board__score { white-space: nowrap; color: #e5bb7c; font: 700 16px Georgia,serif; text-align: right; }
.war-board__score small { display: block; color: var(--war-muted); font-size: 10px; font-weight: 400; }
.war-wallet { display: grid; gap: 8px; }
.war-wallet > div { display: flex; align-items: center; gap: 10px; padding: 11px 13px; border: 1px solid var(--war-border); border-radius: 12px; background: #1a1e27; }
.war-wallet__icon { display: grid; place-items: center; width: 37px; height: 37px; border-radius: 50%; background: #54472e; color: #f5d59a; font-size: 23px; }
.war-wallet__icon--red { background: #533039; color: #ff9ca1; }
.war-wallet > div > span:nth-child(2) { flex: 1; display: grid; gap: 4px; font-weight: 700; font-size: 12px; }
.war-wallet small { color: var(--war-muted); font-weight: 400; font-size: 10px; }
.war-wallet strong { font: 700 20px Georgia,serif; color: #eed3a5; }
.war-reward { display: grid; grid-template-columns: 44px 1fr; gap: 10px; padding: 13px; border: 1px solid var(--war-border); border-radius: 12px; background: #1a1e28; }
.war-reward__crest { display: grid; place-items: center; align-self: start; width: 44px; height: 44px; border: 1px solid #846443; border-radius: 10px; background: linear-gradient(135deg,#4e4140,#251f29); color: #f1cd8b; font: 26px Georgia,serif; }
.war-reward__copy { min-width: 0; }
.war-reward__copy strong { color: #f1ddbe; font-size: 13px; }
.war-reward__copy p { margin: 4px 0 0; color: #b3ab9d; font-size: 11px; line-height: 1.45; }
.war-reward__copy small { display: block; margin-top: 5px; color: #d6ac85; font-size: 10px; }
.war-reward__footer { grid-column: 1/-1; display: flex; align-items: center; justify-content: space-between; gap: 12px; padding-top: 8px; border-top: 1px solid rgb(225 191 144 / 14%); }
.war-reward__footer b { color: #e2c38a; font-size: 11px; }
.war-reward__footer span { color: #a9a298; font-size: 10px; text-align: right; }
.war-intro { margin: 0; color: var(--war-muted); font-size: 12px; line-height: 1.55; }
.war-bounty { display: grid; gap: 9px; padding: 13px; border: 1px solid rgb(191 91 92 / 40%); border-radius: 12px; background: linear-gradient(125deg,#292025,#151a23); }
.war-bounty__head { display: flex; align-items: center; gap: 10px; }
.war-bounty__head > span { color: #df7979; font-size: 29px; }
.war-bounty__head div { display: grid; gap: 4px; }
.war-bounty__head strong { color: #f4d9d0; font: 700 16px Georgia,serif; }
.war-bounty__head small,.war-bounty > small { color: #b6a5a3; font-size: 11px; }
.war-bounty p { margin: 0; color: #c6b9ac; font-size: 11px; }
.war-bounty__prize { padding: 9px; border-radius: 7px; background: rgb(26 18 20 / 45%); color: #bfb3a6; font-size: 11px; }
.war-bounty__prize strong { color: #ffceac; }
.war-title {
  display: grid;
  grid-template-columns: 36px minmax(0, 1fr);
  gap: 10px;
  padding: 12px;
  border: 1px solid var(--war-border);
  border-radius: 11px;
  background: #171c25;
}
.war-title--unlocked {
  border-color: rgb(215 173 111 / 58%);
  background: linear-gradient(120deg, #2d2826, #171b24);
}
.war-title__seal {
  display: grid;
  width: 36px;
  height: 36px;
  place-items: center;
  border: 1px solid rgb(222 175 115 / 37%);
  border-radius: 50%;
  color: #e5bb8a;
  font: 700 22px Georgia, serif;
}
.war-title__copy {
  display: grid;
  min-width: 0;
  gap: 5px;
}
.war-title__copy strong {
  color: #f3ddbd;
  font: 700 15px Georgia, serif;
}
.war-title__copy p {
  margin: 0;
  color: #b4ada3;
  font-size: 11px;
  line-height: 1.45;
}
.war-title__copy > small {
  color: #c7ad87;
  font-size: 10px;
}
.war-title__progress {
  display: grid;
  gap: 4px;
}
.war-title__progress-track {
  height: 5px;
  overflow: hidden;
  border-radius: 10px;
  background: #36333a;
}
.war-title__progress-track > span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: #dab77f;
}
.war-title__progress > small {
  color: #b9a999;
  font-size: 10px;
}
.war-title__state {
  grid-column: 2;
  justify-self: start;
  color: #d3b28b;
  font-size: 10px;
  font-weight: 700;
}
.war-footer { padding: 12px 0 2px; border-top: 1px solid var(--war-border); text-align: center; color: #8f8c88; font-size: 10px; line-height: 1.5; }
@media (max-width: 410px) {
  .war-head { padding: 16px 12px; }
  .war-head__title { display: grid; }
  .war-head__status { justify-self: start; }
  .war-section__lead h3 { font-size: 19px; }
  .war-board__rank { width: 28px; font-size: 15px; }
  .war-board__score { font-size: 13px; }
}
</style>
