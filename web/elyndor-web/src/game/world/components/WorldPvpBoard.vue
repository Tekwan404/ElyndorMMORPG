<script setup lang="ts">
import { useWorldPvpProgression } from '../useWorldPvpProgression'
const { overview, offers, race, loading, buying, error, message, refresh, buy } =
  useWorldPvpProgression()
</script>

<template>
  <section class="war-board" aria-label="Война фракций" data-world-pvp-board>
    <header>
      <h3>Война фракций</h3>
      <button :disabled="loading || buying" @click="refresh">Обновить</button>
    </header>
    <p>Сражения в Мёртвых Пределах · ур. 60. Отдельный рейтинг открытого мира.</p>
    <p v-if="loading" role="status">Загрузка реестра…</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="message" role="status">{{ message }}</p>
    <template v-if="overview">
      <p>
        Сезон {{ overview.season }} · до
        {{ new Date(overview.endsAtUtc).toLocaleDateString('ru-RU') }}
      </p>
      <p class="war-board__wallet">
        Полевая честь: {{ overview.fieldHonor }} · Кровавые жетоны: {{ overview.bloodyTokens }}
      </p>
      <p>
        Твой рейтинг: {{ overview.self.rating }} · Победы: {{ overview.self.victories }} · Помощь:
        {{ overview.self.assists }} · Смерти: {{ overview.self.deaths }}
      </p>
      <p class="war-board__rules">
        Награждено сегодня: {{ overview.rewardedToday }}/{{ overview.dailyLimit }}. Одна пара
        противников приносит награду раз в 24 часа. Даже быстрая победа приносит рейтинг и честь,
        если за эту пару ещё положена награда. За поражение снимается половина очков,
        полученных победителем (но не ниже нуля). Помощь в группе приносит уменьшенную награду.
        Новый день — в 00:00 UTC.
      </p>
      <label
        >Рейтинг
        <select v-model="race" :disabled="loading || buying" @change="refresh">
          <option value="">Все фракции</option>
          <option value="HUMAN">Люди</option>
          <option value="UNDEAD">Нежить</option>
        </select></label
      >
      <ol class="war-board__ranks">
        <li v-for="leader in overview.leaders" :key="leader.characterId">
          <strong>{{ leader.name }}</strong> · {{ leader.raceId === 'UNDEAD' ? 'Нежить' : 'Люди'
          }}<br />{{ leader.rating }} очк. · {{ leader.victories }} побед ·
          {{ leader.assists }} помощи · {{ leader.deaths }} смертей
        </li>
      </ol>
      <p v-if="!overview.leaders.length">В этом сезоне ещё нет участников с очками.</p>
      <h4>Награды войны</h4>
      <p class="war-board__rules">
        5 полевой чести за зачётную победу или помощь. Основной участник победы над противником с не
        меньшим рейтингом получает жетон. Снаряжение совпадает с ассортиментом арены, цены назначает
        сервер.
      </p>
      <ul class="war-board__offers">
        <li v-for="offer in offers" :key="offer.id">
          <strong>{{ offer.name }}</strong> · ур. {{ offer.requiredLevel }}
          <div class="war-board__actions">
            <button
              data-buy-honor
              :disabled="loading || buying || overview.fieldHonor < offer.fieldHonorPrice"
              @click="buy(offer, false)"
            >
              {{ offer.fieldHonorPrice }} чести</button
            ><button
              :disabled="loading || buying || overview.bloodyTokens < offer.tokenPrice"
              @click="buy(offer, true)"
            >
              {{ offer.tokenPrice }} жет.
            </button>
          </div>
        </li>
      </ul>
    </template>
  </section>
</template>

<style scoped>
.war-board {
  color: var(--ui-text-primary, #e0d4bc);
  display: grid;
  gap: 0.7rem;
  min-width: 0;
}
.war-board header,
.war-board__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
  justify-content: space-between;
}
.war-board p,
.war-board h3,
.war-board h4 {
  margin: 0;
  line-height: 1.5;
}
.war-board__wallet {
  color: #e2be75;
}
.war-board__rules {
  font-size: 0.78rem;
  color: #b6aa94;
}
.war-board button,
.war-board select {
  min-height: 44px;
  border: 1px solid #746043;
  background: #251e17;
  color: #ecd9b4;
  padding: 0.4rem 0.6rem;
  font: inherit;
}
.war-board button:disabled {
  opacity: 0.5;
}
.war-board button:focus-visible,
.war-board select:focus-visible {
  outline: 2px solid #e6c481;
  outline-offset: 2px;
}
.war-board__offers {
  list-style: none;
  padding: 0;
  margin: 0;
}
.war-board li {
  overflow-wrap: anywhere;
  padding: 0.6rem 0;
  border-bottom: 1px solid #4a3e2d;
  font-size: 0.82rem;
  line-height: 1.5;
}
.war-board__actions {
  justify-content: flex-start;
  margin-top: 0.4rem;
}
.war-board__ranks {
  margin: 0;
  padding-left: 1.5rem;
}
</style>
