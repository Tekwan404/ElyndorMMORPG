<script setup lang="ts">
import { ref } from 'vue'
import { adminRequest, AdminApiError } from '@/api'

interface PlayerCharacter {
  id: string
  name: string
  classId: string
  raceId: string
  level: number
  experience: number
  gold: number
  locationId: string | null
  currentHp: number | null
  currentResource: number | null
  inventoryCount: number
  equippedCount: number
  gmItemsCount: number
}

interface PlayerSnapshot {
  telegramUserId: number
  telegramUsername: string | null
  createdAtUtc: string
  lastSeenAtUtc: string
  character: PlayerCharacter | null
}

const emit = defineEmits<{ 'forge-target': [telegramId: string] }>()
const targetId = ref('')
const snapshot = ref<PlayerSnapshot | null>(null)
const error = ref('')
const busy = ref(false)

async function loadPlayer(): Promise<void> {
  if (busy.value) return
  snapshot.value = null
  error.value = ''
  const id = targetId.value.trim()
  if (!/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id))) {
    error.value = 'Укажи корректный числовой Telegram ID.'
    return
  }
  busy.value = true
  try {
    snapshot.value = await adminRequest<PlayerSnapshot>(
      '/api/v1/admin/players/' + encodeURIComponent(id),
    )
  } catch (cause) {
    if (cause instanceof AdminApiError) {
      error.value = cause.status === 404 ? 'Аккаунт с таким Telegram ID не найден.'
        : cause.status === 403 ? 'Нет прав SUPER_ADMIN.'
        : 'Не удалось получить персонажа: ' + cause.code
    } else {
      error.value = 'Сервер недоступен. Проверь соединение и попробуй ещё раз.'
    }
  } finally {
    busy.value = false
  }
}

async function copyCommand(command: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(command)
  } catch {
    error.value = 'Не удалось скопировать команду. Используй кнопку из Telegram-админки.'
  }
}

function formatNumber(value: number | null | undefined): string {
  return value == null ? '—' : new Intl.NumberFormat('ru-RU').format(value)
}

function formatDate(value: string): string {
  return new Date(value).toLocaleString('ru-RU')
}
</script>

<template>
  <main class="players">
    <header>
      <p class="eyebrow">OPERATIONS / READ ONLY</p>
      <h1>Игроки</h1>
      <p>Просмотр состояния персонажа без изменения базы данных. Поиск доступен только SUPER_ADMIN.</p>
    </header>

    <form class="lookup" @submit.prevent="loadPlayer">
      <label for="player-telegram-id">Telegram ID</label>
      <div class="lookup__controls">
        <input id="player-telegram-id" v-model="targetId" type="text" inputmode="numeric" autocomplete="off" placeholder="123456789" />
        <button type="submit" :disabled="busy">{{ busy ? 'Ищем…' : 'Найти персонажа' }}</button>
      </div>
    </form>
    <p v-if="error" class="players-error" role="alert">{{ error }}</p>

    <template v-if="snapshot">
      <section class="player-card">
        <div class="player-card__heading">
          <div>
            <p class="eyebrow">ACCOUNT</p>
            <h2>{{ snapshot.telegramUsername ? '@' + snapshot.telegramUsername : 'Telegram ' + snapshot.telegramUserId }}</h2>
            <small>ID: {{ snapshot.telegramUserId }}</small>
          </div>
          <button type="button" @click="emit('forge-target', String(snapshot.telegramUserId))">
            Открыть GM Forge
          </button>
        </div>
        <dl>
          <div><dt>Создан</dt><dd>{{ formatDate(snapshot.createdAtUtc) }}</dd></div>
          <div><dt>Последняя активность</dt><dd>{{ formatDate(snapshot.lastSeenAtUtc) }}</dd></div>
        </dl>
      </section>

      <section v-if="snapshot.character" class="player-card">
        <h2>{{ snapshot.character.name }} · ур. {{ snapshot.character.level }}</h2>
        <div class="player-grid">
          <div><span>Класс / раса</span><strong>{{ snapshot.character.classId }} / {{ snapshot.character.raceId }}</strong></div>
          <div><span>Локация</span><strong>{{ snapshot.character.locationId || '—' }}</strong></div>
          <div><span>HP / ресурс</span><strong>{{ formatNumber(snapshot.character.currentHp) }} / {{ formatNumber(snapshot.character.currentResource) }}</strong></div>
          <div><span>Опыт</span><strong>{{ formatNumber(snapshot.character.experience) }}</strong></div>
          <div><span>Золото</span><strong>{{ formatNumber(snapshot.character.gold) }}</strong></div>
          <div><span>Инвентарь / надето</span><strong>{{ snapshot.character.inventoryCount }} / {{ snapshot.character.equippedCount }}</strong></div>
          <div><span>GM-предметов</span><strong>{{ snapshot.character.gmItemsCount }}</strong></div>
        </div>
        <div class="player-card__actions">
          <button type="button" @click="copyCommand('/char ' + snapshot.telegramUserId)">Копировать /char</button>
          <button type="button" @click="copyCommand('/gear ' + snapshot.telegramUserId)">Копировать /gear</button>
          <button type="button" @click="copyCommand('/builddump ' + snapshot.telegramUserId)">Копировать /builddump</button>
        </div>
        <p class="hint">Эти команды только копируются, ничего не выполняется автоматически. Для изменения персонажа используй защищённые команды Telegram-админки.</p>
      </section>
      <section v-else class="player-card"><p>Аккаунт существует, но персонаж пока не создан.</p></section>
    </template>
  </main>
</template>

<style scoped>
.players { max-width: 1050px; margin: 0 auto; color: #edeaf3; }
.players header { margin-bottom: 24px; }
.eyebrow { font-size: 11px; letter-spacing: .12em; color: #b5a0da; }
.players h1 { margin: 6px 0; font-size: clamp(26px, 4vw, 36px); }
.players header > p:last-child, .hint { color: #9da5b7; line-height: 1.55; }
.lookup, .player-card { padding: 20px; margin: 14px 0; border: 1px solid #34303b; background: #211e28; border-radius: 14px; }
.lookup label { display: block; margin-bottom: 9px; color: #c6bfd3; }
.lookup__controls { display: flex; gap: 10px; }
input { min-width: 0; flex: 1; padding: 12px; border: 1px solid #51475e; border-radius: 8px; color: #f5f2fa; background: #17151c; font: inherit; }
button { min-height: 43px; border: 1px solid #75628c; border-radius: 8px; background: #413450; color: #f2ebfd; padding: 9px 12px; cursor: pointer; font: inherit; }
button:disabled { opacity: .55; cursor: wait; }
button:focus-visible { outline: 2px solid #c2a2e4; outline-offset: 2px; }
.players-error { color: #ffaaa8; }
.player-card__heading { display: flex; justify-content: space-between; gap: 12px; flex-wrap: wrap; align-items: center; }
.player-card h2 { font-size: 20px; margin: 8px 0; overflow-wrap: anywhere; }
.player-card small { color: #a5a0b0; }
.player-card dl { margin: 12px 0 0; }
.player-card dl > div { display: flex; gap: 12px; justify-content: space-between; padding: 8px 0; border-top: 1px solid #39333f; }
.player-card dt { color: #a5a0b0; }
.player-card dd { margin: 0; overflow-wrap: anywhere; text-align: right; }
.player-grid { display: grid; grid-template-columns: repeat(3,minmax(0,1fr)); gap: 10px; margin: 18px 0; }
.player-grid > div { display: grid; gap: 8px; padding: 12px; background: #17151c; border: 1px solid #332f39; border-radius: 9px; }
.player-grid span { color: #96909f; font-size: 12px; }
.player-grid strong { overflow-wrap: anywhere; }
.player-card__actions { display: flex; flex-wrap: wrap; gap: 8px; }
.hint { margin-bottom: 0; font-size: 12px; }
@media(max-width:680px) {
  .lookup__controls { flex-direction: column; }
  .player-grid { grid-template-columns: repeat(2,minmax(0,1fr)); }
}
@media(max-width:410px) { .player-grid { grid-template-columns: 1fr; } }
</style>
