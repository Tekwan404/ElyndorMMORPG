<script setup lang="ts">
import { useGameSessionStore } from '@/stores/gameSession'
const session = useGameSessionStore()
</script>
<template>
  <section v-if="session.snapshot?.world && session.snapshot.character" class="world">
    <p class="kicker">{{ session.snapshot.world.currentLocation.dangerLevel }}</p>
    <h1>{{ session.snapshot.world.currentLocation.displayName }}</h1>
    <p class="hero">
      {{ session.snapshot.character.name }} · уровень {{ session.snapshot.character.level }}
    </p>
    <div class="scene" aria-hidden="true"><span>♜</span></div>
    <h2>Доступные пути</h2>
    <p v-if="session.snapshot.world.outgoingTransitions.length === 0" class="muted">
      Пути отсюда пока не найдены.
    </p>
    <button
      v-for="location in session.snapshot.world.outgoingTransitions"
      :key="location.id"
      class="travel"
      :disabled="session.mutationPending"
      type="button"
      @click="session.travel(location.id)"
    >
      <span>{{ location.displayName }}</span
      ><small>опасность: {{ location.dangerLevel }} · ур. {{ location.recommendedLevel }}</small>
    </button>
    <p v-if="session.errorCode" class="error" role="alert">{{ session.errorCode }}</p>
  </section>
</template>
<style scoped lang="scss">
.world {
  width: min(100%, 480px);
  margin: auto;
  padding: 28px 18px;
  text-align: center;
}
.kicker {
  margin: 0;
  color: var(--color-gold);
  font-size: 0.7rem;
  letter-spacing: 0.16em;
}
h1 {
  margin: 6px 0;
  font:
    500 clamp(2rem, 10vw, 3rem) Georgia,
    serif;
  color: #f0e7d2;
}
.hero,
.muted {
  color: var(--color-text-muted);
}
.scene {
  display: grid;
  min-height: 180px;
  margin: 22px 0;
  border-block: 1px solid rgb(200 169 99 / 32%);
  background: radial-gradient(circle, rgb(88 74 164 / 32%), transparent 62%);
  place-items: center;
}
.scene span {
  color: var(--color-gold);
  font-size: 4rem;
  filter: drop-shadow(0 0 18px #594da0);
}
h2 {
  font:
    1.1rem Georgia,
    serif;
  color: #e5d8bc;
  text-align: left;
}
.travel {
  display: grid;
  width: 100%;
  margin-top: 9px;
  padding: 12px 14px;
  border: 1px solid var(--color-border);
  background: #101827;
  color: var(--color-text-primary);
  text-align: left;
}
.travel small {
  margin-top: 4px;
  color: var(--color-text-muted);
}
.travel:disabled {
  opacity: 0.5;
}
.error {
  color: #ef8c93;
}
</style>
