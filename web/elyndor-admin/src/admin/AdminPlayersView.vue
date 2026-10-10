<script setup lang="ts">
import { onMounted, ref } from 'vue'
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

interface DirectoryPlayer { telegramUserId: number; telegramUsername: string | null; lastSeenAtUtc: string; character: { name: string; level: number; classId: string } | null }
interface PlayerDirectory { total: number; page: number; pageSize: number; players: DirectoryPlayer[] }
interface PlayerItem { id: string; name: string; itemDefinitionId: string; quantity: number; stars: number | null; enhancementLevel: number; isEquipped: boolean; sourceType: string | null; canClone: boolean }

const emit = defineEmits<{ 'forge-target': [telegramId: string]; 'clone-item': [telegramId: string, itemId: string] }>()
const directory = ref<PlayerDirectory | null>(null)
const directoryQuery = ref('')
const directoryBusy = ref(false)
const directoryError = ref('')
const items = ref<PlayerItem[]>([])
const itemsLoading = ref(false)
const itemsVisible = ref(false)
const targetId = ref('')
const snapshot = ref<PlayerSnapshot | null>(null)
const error = ref('')
const busy = ref(false)

async function loadPlayer(): Promise<void> {
  if (busy.value) return
  snapshot.value = null
  items.value = []
  itemsVisible.value = false
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

async function loadDirectory(page = 1): Promise<void> {
  if (directoryBusy.value) return
  directoryBusy.value = true
  directoryError.value = ''
  try {
    directory.value = await adminRequest<PlayerDirectory>(
      '/api/v1/admin/players?page=' + page + '&search=' + encodeURIComponent(directoryQuery.value.trim()),
    )
  } catch {
    directoryError.value = 'Не удалось загрузить список игроков.'
  } finally {
    directoryBusy.value = false
  }
}

async function choosePlayer(id: number): Promise<void> {
  targetId.value = String(id)
  await loadPlayer()
}

async function loadItems(): Promise<void> {
  if (!snapshot.value?.character || itemsLoading.value) return
  itemsVisible.value = true
  itemsLoading.value = true
  error.value = ''
  try {
    const response = await adminRequest<{ items: PlayerItem[] }>(
      '/api/v1/admin/players/' + snapshot.value.telegramUserId + '/items',
    )
    items.value = response.items
  } catch {
    error.value = 'Не удалось загрузить инвентарь персонажа.'
  } finally {
    itemsLoading.value = false
  }
}

onMounted(() => { void loadDirectory() })

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
      <p class="eyebrow">Управление · Просмотр</p>
      <h1>Игроки</h1>
      <p>Выбери игрока из списка или найди по имени, нику либо Telegram ID.</p>
    </header>

    <section class="player-card">
      <div class="player-card__heading"><h2>Все игроки</h2><span v-if="directory">Всего: {{ directory.total }}</span></div>
      <form class="lookup__controls" @submit.prevent="loadDirectory(1)">
        <input v-model="directoryQuery" aria-label="Поиск игроков" placeholder="Имя персонажа, ник или Telegram ID" />
        <button type="submit" :disabled="directoryBusy">Найти</button>
      </form>
      <p v-if="directoryError" role="alert" class="players-error">{{ directoryError }}</p>
      <p v-if="directoryBusy && !directory">Загружаем игроков…</p>
      <div v-if="directory" class="directory">
        <button v-for="player in directory.players" :key="player.telegramUserId" type="button"
          class="directory__item" @click="choosePlayer(player.telegramUserId)">
          <strong>{{ player.character?.name ?? (player.telegramUsername ? '@' + player.telegramUsername : 'Без персонажа') }}</strong>
          <span>{{ player.character ? 'Ур. ' + player.character.level + ' · ' + player.character.classId : 'Персонаж не создан' }}</span>
          <small>{{ player.telegramUsername ? '@' + player.telegramUsername + ' · ' : '' }}{{ player.telegramUserId }}</small>
        </button>
        <p v-if="!directory.players.length">Игроки не найдены.</p>
      </div>
      <div v-if="directory" class="directory__paging">
        <button type="button" :disabled="directoryBusy || directory.page <= 1" @click="loadDirectory(directory.page - 1)">Назад</button>
        <span>Страница {{ directory.page }} из {{ Math.max(1, Math.ceil(directory.total / directory.pageSize)) }}</span>
        <button type="button" :disabled="directoryBusy || directory.page * directory.pageSize >= directory.total" @click="loadDirectory(directory.page + 1)">Вперёд</button>
      </div>
    </section>

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
            <p class="eyebrow">Учётная запись</p>
            <h2>{{ snapshot.telegramUsername ? '@' + snapshot.telegramUsername : 'Telegram ' + snapshot.telegramUserId }}</h2>
            <small>ID: {{ snapshot.telegramUserId }}</small>
          </div>
          <button type="button" @click="emit('forge-target', String(snapshot.telegramUserId))">
            Выдать предметы
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
          <button type="button" :disabled="itemsLoading" @click="loadItems">{{ itemsLoading ? 'Загружаем вещи…' : 'Посмотреть инвентарь' }}</button>
          <button type="button" @click="copyCommand('/char ' + snapshot.telegramUserId)">Копировать /char</button>
          <button type="button" @click="copyCommand('/gear ' + snapshot.telegramUserId)">Копировать /gear</button>
          <button type="button" @click="copyCommand('/builddump ' + snapshot.telegramUserId)">Копировать /builddump</button>
        </div>
        <section v-if="itemsVisible" class="inventory-list">
          <h3>Вещи персонажа</h3>
          <p v-if="!items.length && !itemsLoading">Предметов нет.</p>
          <div v-for="item in items" :key="item.id" class="inventory-list__item">
            <div><strong>{{ item.name }}</strong>
              <small>{{ item.quantity }} шт. · {{ item.stars ?? '—' }}★ · +{{ item.enhancementLevel }}{{ item.isEquipped ? ' · Надето' : '' }}</small>
              <small class="inventory-list__id">Экземпляр: {{ item.id }}</small>
            </div>
            <button type="button"  :disabled="!item.canClone" @click="emit('clone-item', String(snapshot.telegramUserId), item.id)">{{ item.canClone ? 'Сделать копию' : 'Нельзя копировать' }}</button>
          </div>
        </section>
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
.directory { display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px;margin-top:16px;max-height:420px;overflow:auto; }
.directory__item { text-align:left;display:grid;gap:4px;background:#17151c;border:1px solid #3d3547; }
.directory__item span,.directory__item small { color:#a8a1b6;font-size:12px; }
.directory__paging { display:flex;align-items:center;justify-content:space-between;gap:8px;margin-top:14px; }
.inventory-list { border-top:1px solid #40394a;margin-top:18px;padding-top:16px; }
.inventory-list__item { display:flex;align-items:center;justify-content:space-between;gap:12px;border-bottom:1px solid #40394a;padding:10px 0; }
.inventory-list__item > div { display:grid;gap:5px;min-width:0; }
.inventory-list__item small { font-size:12px;color:#a8a1b6; }
.inventory-list__id { overflow-wrap:anywhere; }
@media(max-width:620px) { .directory { grid-template-columns:1fr; } .inventory-list__item { flex-direction:column;align-items:stretch; } }
</style>
