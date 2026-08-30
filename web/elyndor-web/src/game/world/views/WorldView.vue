<template>
  <section class="world-view">
    <div class="world-view__content">
      <p class="world-view__kicker">Первый игровой контур</p>
      <h1>Северные земли</h1>
      <p class="world-view__description">
        Основа мира готова к подключению server-authoritative персонажа, путешествий и охоты.
      </p>

      <div class="world-view__divider" aria-hidden="true">
        <span></span>
        <b>◆</b>
        <span></span>
      </div>

      <dl class="foundation-list">
        <div>
          <dt>Backend</dt>
          <dd>ASP.NET Core / PostgreSQL</dd>
        </div>
        <div>
          <dt>Runtime</dt>
          <dd>Aspire / Docker</dd>
        </div>
        <div>
          <dt>Client</dt>
          <dd>Vue 3 / Telegram Mini App</dd>
        </div>
      </dl>

      <p class="world-view__notice">
        Следующий vertical slice: Telegram identity → character → world.
      </p>
    </div>
  </section>
</template>

<style scoped lang="scss">
.world-view {
  display: grid;
  min-height: 100%;
  padding: 26px 18px;
  place-items: center;
}

.world-view__content {
  width: min(100%, 440px);
  padding: 28px 22px;
  border-block: 1px solid rgb(200 169 99 / 32%);
  background: linear-gradient(
    90deg,
    transparent,
    rgb(19 28 47 / 72%) 18%,
    rgb(19 28 47 / 72%) 82%,
    transparent
  );
  text-align: center;
}

.world-view__kicker {
  margin: 0 0 8px;
  color: var(--color-gold);
  font-size: 0.67rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
}

h1 {
  margin: 0;
  color: #f0e7d2;
  font-family: Georgia, 'Times New Roman', serif;
  font-size: clamp(2rem, 10vw, 3.25rem);
  font-weight: 500;
  letter-spacing: 0.03em;
  text-shadow: 0 2px 22px rgb(95 80 190 / 55%);
}

.world-view__description {
  max-width: 34rem;
  margin: 14px auto 0;
  color: var(--color-text-secondary);
  line-height: 1.55;
}

.world-view__divider {
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  gap: 10px;
  align-items: center;
  margin: 24px 0;
  color: var(--color-gold);
}

.world-view__divider span {
  height: 1px;
  background: linear-gradient(90deg, transparent, rgb(201 168 94 / 55%));
}

.world-view__divider span:last-child {
  background: linear-gradient(90deg, rgb(201 168 94 / 55%), transparent);
}

.foundation-list {
  display: grid;
  gap: 12px;
  margin: 0;
  text-align: left;
}

.foundation-list > div {
  display: flex;
  gap: 16px;
  justify-content: space-between;
  padding-bottom: 10px;
  border-bottom: 1px solid rgb(142 156 191 / 15%);
}

.foundation-list dt {
  color: var(--color-text-muted);
  font-size: 0.7rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

.foundation-list dd {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 0.82rem;
  text-align: right;
}

.world-view__notice {
  margin: 24px 0 0;
  color: var(--color-text-muted);
  font-size: 0.74rem;
  line-height: 1.5;
}
</style>
