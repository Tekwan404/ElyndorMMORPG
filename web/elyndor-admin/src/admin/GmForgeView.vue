<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { adminRequest, AdminApiError, type ContentAdminCurrent } from '@/api'

const props = defineProps<{
  defaultTelegramId: string
  packageJson: ContentAdminCurrent['payloadJson']
  cloneFromId?: string
}>()

type Mode = 'regular' | 'custom' | 'clone'
type ItemOption = { id: string; name: string; type: string; generationMode: string }
type Override = { stat: string; value: string }
type BatchLine = { mode: 'regular' | 'custom'; specification: string; title: string }
type ForgeResponse = { code: string; message: string; isDuplicate: boolean }
type BatchResult = { items: Array<{ index: number; isSuccess: boolean; isDuplicate: boolean; code: string; message: string }> }
type DirectoryPlayer = { telegramUserId: number; telegramUsername: string | null; character: { name: string; level: number; classId: string } | null }
type Directory = { total: number; page: number; pageSize: number; players: DirectoryPlayer[] }
type OwnedItem = { id: string; name: string; itemDefinitionId: string; stars: number | null; quantity: number; enhancementLevel: number; isEquipped: boolean }

const mode = ref<Mode>(props.cloneFromId ? 'clone' : 'regular')
const recipient = ref(props.defaultTelegramId)
const itemId = ref('')
const cloneId = ref(props.cloneFromId ?? '')
const search = ref('')
const quantity = ref('1')
const quality = ref('NORMAL')
const stars = ref('auto')
const enhance = ref(props.cloneFromId ? 'keep' : '0')
const overrides = ref<Override[]>([])
const statSearch = ref('')
const statCategory = ref('Все')
const pickStats = ref(false)
const queue = ref<BatchLine[]>([])
const busy = ref(false)
const error = ref('')
const result = ref('')
const retryRequestId = ref<string | null>(null)
const showPlayers = ref(false)
const playerSearch = ref('')
const players = ref<Directory | null>(null)
const playerPage = ref(1)
const playerBusy = ref(false)
const ownedItems = ref<OwnedItem[]>([])
const ownedBusy = ref(false)

const groups: Array<{ name: string; entries: Array<[string, string]> }> = [
  { name: 'Урон', entries: [
    ['WEAPON_DAMAGE', 'Урон оружия'], ['ATTACK_POWER', 'Сила атаки'],
    ['SPELL_POWER', 'Сила заклинаний'], ['CRITICAL_DAMAGE', 'Критический урон, %'],
    ['CRITICAL_CHANCE', 'Критический шанс, %'], ['ATTACK_SPEED', 'Скорость атаки, %'],
    ['ACCURACY', 'Меткость, %'],
  ] },
  { name: 'Основные', entries: [
    ['STRENGTH', 'Сила'], ['AGILITY', 'Ловкость'],
    ['INTELLECT', 'Интеллект'], ['STAMINA', 'Выносливость'],
    ['MAX_HP', 'Здоровье'], ['MAX_RESOURCE', 'Запас ресурса'],
  ] },
  { name: 'Защита', entries: [
    ['ARMOR', 'Броня'], ['MAGIC_RESISTANCE', 'Сопротивление магии'],
    ['DODGE', 'Уклонение, %'], ['BLOCK_CHANCE', 'Шанс блока, %'],
    ['BLOCK_VALUE', 'Сила блока'], ['ARMOR_PENETRATION', 'Пробивание брони, %'],
    ['MAGIC_PENETRATION', 'Пробивание магии, %'],
  ] },
  { name: 'Вампиризм', entries: [
    ['PHYSICAL_VAMPIRISM', 'Физический вампиризм, %'],
    ['MAGICAL_VAMPIRISM', 'Магический вампиризм, %'],
    ['UNIVERSAL_VAMPIRISM', 'Общий вампиризм, %'],
  ] },
]
const allStats = groups.flatMap(group => group.entries.map(([id, label]) =>
  ({ id, label, group: group.name })))
const statName = (id: string): string => allStats.find(stat => stat.id === id)?.label ?? id
const suggestedStats = computed(() => allStats.filter(stat => {
  const q = statSearch.value.trim().toLowerCase()
  return (statCategory.value === 'Все' || stat.group === statCategory.value)
    && (!q || (stat.label + ' ' + stat.id).toLowerCase().includes(q))
    && !overrides.value.some(row => row.stat === stat.id)
}))

const allItems = computed<ItemOption[]>(() => {
  try {
    const parsed: unknown = JSON.parse(props.packageJson)
    if (!parsed || typeof parsed !== 'object') return []
    const items = (parsed as { items?: unknown }).items
    if (!Array.isArray(items)) return []
    return items
      .filter((item): item is Record<string, unknown> =>
        item !== null && typeof item === 'object' && typeof item.id === 'string')
      .map(item => ({
        id: String(item.id), name: String(item.name ?? item.id),
        type: String(item.type ?? ''), generationMode: String(item.generationMode ?? ''),
      }))
      .sort((a, b) => a.name.localeCompare(b.name, 'ru'))
  } catch {
    return []
  }
})

const selectedItem = computed(() => allItems.value.find(item => item.id === itemId.value))
const visibleItems = computed(() => {
  const query = search.value.trim().toLocaleLowerCase('ru')
  return allItems.value.filter(item =>
    !query || (item.name + ' ' + item.id).toLocaleLowerCase('ru').includes(query)).slice(0, 80)
})
const chosenCount = computed(() => Number(quantity.value))
const limit = computed(() => mode.value === 'regular' ? 1000 : 20)
const currentSpec = computed(() => {
  const id = mode.value === 'clone' ? 'clone:' + cloneId.value.trim() : itemId.value.trim()
  if (mode.value === 'regular') return [id, quantity.value, 'NORMAL'].join(' ')
  const parts = [id, 'quality=' + quality.value, 'qty=' + quantity.value]
  if (stars.value !== 'auto') parts.push('stars=' + stars.value)
  if (enhance.value !== 'keep') parts.push('enhance=' + enhance.value)
  for (const stat of overrides.value) {
    if (stat.value.trim()) parts.push(stat.stat + '=' + stat.value.trim())
  }
  return parts.join(' ')
})
const command = computed(() =>
  (mode.value === 'regular' ? '/giveitem ' : '/gmforge ') + recipient.value.trim() + ' ' + currentSpec.value)
const visualStars = computed(() => quality.value === 'PERFECT' ? 5 : stars.value === 'auto' ? null : Number(stars.value))
const selectedName = computed(() => mode.value === 'clone'
  ? ownedItems.value.find(item => item.id === cloneId.value)?.name ?? 'Копия существующего предмета'
  : selectedItem.value?.name ?? 'Предмет пока не выбран')

watch(() => props.defaultTelegramId, value => { recipient.value = value })
watch(() => props.cloneFromId, value => {
  if (value) {
    cloneId.value = value
    switchMode('clone')
  }
})
watch([recipient, currentSpec], () => { retryRequestId.value = null; error.value = ''; result.value = '' })

function switchMode(value: Mode): void {
  mode.value = value
  quality.value = 'NORMAL'
  stars.value = 'auto'
  enhance.value = value === 'clone' ? 'keep' : '0'
  quantity.value = '1'
  overrides.value = []
  pickStats.value = false
}

function addStat(id: string): void {
  if (overrides.value.some(entry => entry.stat === id)) return
  overrides.value.push({ stat: id, value: '' })
  retryRequestId.value = null
}

function useCrazySword(): void {
  mode.value = 'custom'
  quality.value = 'PERFECT'
  stars.value = '5'
  enhance.value = '5'
  overrides.value = [
    { stat: 'WEAPON_DAMAGE', value: '1500' },
    { stat: 'CRITICAL_DAMAGE', value: '150' },
  ]
}

function validateCurrent(): string | null {
  const count = Number(quantity.value)
  if (!Number.isInteger(count) || count < 1 || count > limit.value)
    return 'Количество должно быть от 1 до ' + limit.value + '.'
  if (mode.value === 'clone') {
    if (!/^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(cloneId.value.trim()))
      return 'Выбери существующий предмет из списка вещей игрока.'
  } else if (!itemId.value.trim()) {
    return 'Сначала выбери предмет.'
  }
  if (mode.value === 'custom' && selectedItem.value?.type.toUpperCase() !== 'EQUIPMENT')
    return 'Произвольные характеристики доступны только для экипировки. Для остальных предметов используй обычную выдачу.'
  if (mode.value !== 'regular' && quality.value === 'PERFECT' && stars.value !== 'auto' && stars.value !== '5')
    return 'Для максимального качества выбери 5 звёзд или автоматическое определение.'
  const seen = new Set<string>()
  for (const row of overrides.value) {
    if (seen.has(row.stat)) return 'Характеристика добавлена дважды: ' + statName(row.stat)
    seen.add(row.stat)
    if (!/^\d+(?:\.\d+)?$/.test(row.value.trim()) || Number(row.value) > 1_000_000)
      return 'Введи значение от 0 до 1 000 000 для «' + statName(row.stat) + '».'
  }
  return null
}

function makeLine(): BatchLine {
  return {
    mode: mode.value === 'regular' ? 'regular' : 'custom',
    specification: currentSpec.value,
    title: selectedName.value + ' ×' + quantity.value + (mode.value === 'regular' ? '' : ' · испытательный'),
  }
}

function addToQueue(): void {
  error.value = ''
  const issue = validateCurrent()
  if (issue) { error.value = issue; return }
  if (queue.value.length >= 20) { error.value = 'В одном списке не больше 20 разных позиций.'; return }
  queue.value.push(makeLine())
  retryRequestId.value = null
  result.value = ''
}

function removeFromQueue(index: number): void {
  queue.value.splice(index, 1)
  retryRequestId.value = null
}

function validateRecipient(): string | null {
  const id = recipient.value.trim()
  return /^[1-9]\d*$/.test(id) && Number.isSafeInteger(Number(id))
    ? null : 'Выбери игрока или введи его Telegram ID.'
}

async function issue(): Promise<void> {
  if (busy.value) return
  error.value = ''
  result.value = ''
  const recipientIssue = validateRecipient()
  if (recipientIssue) { error.value = recipientIssue; return }
  if (!queue.value.length) {
    const issue = validateCurrent()
    if (issue) { error.value = issue; return }
  }
  const items = queue.value.length ? queue.value : [makeLine()]
  const requestId = retryRequestId.value ?? crypto.randomUUID()
  retryRequestId.value = requestId
  busy.value = true
  try {
    if (items.length === 1 && items[0]?.mode === 'custom') {
      const answer = await adminRequest<ForgeResponse>('/api/v1/admin/gm-forge/create', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          targetTelegramUserId: Number(recipient.value.trim()),
          specification: items[0].specification, requestId,
        }),
      })
      result.value = answer.message
    } else {
      const response = await adminRequest<BatchResult>('/api/v1/admin/gm-forge/batch', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          targetTelegramUserId: Number(recipient.value.trim()), requestId,
          items: items.map(item => ({ mode: item.mode, specification: item.specification })),
        }),
      })
      const errors = response.items.filter(item => !item.isSuccess)
      const passed = response.items.filter(item => item.isSuccess).length
      result.value = 'Выдано позиций: ' + passed + ' из ' + response.items.length
      if (errors.length) {
        error.value = 'Ошибки: ' + errors.map(item => '№' + (item.index + 1) + ' — ' + readableCode(item.code)).join('; ')
        return
      }
    }
    retryRequestId.value = null
    if (queue.value.length) queue.value = []
  } catch (cause) {
    if (cause instanceof AdminApiError) {
      error.value = readableCode(cause.code)
      if (cause.status < 500) retryRequestId.value = null
    } else {
      error.value = 'Нет ответа от сервера. Повтори запрос — тот же ID предотвратит двойную выдачу.'
    }
  } finally {
    busy.value = false
  }
}

function readableCode(code: string): string {
  const errors: Record<string, string> = {
    admin_inventory_full: 'Недостаточно места в инвентаре.',
    admin_target_not_found: 'Игрок не найден.',
    admin_item_not_found: 'Такого предмета нет в игре.',
    admin_gmforge_item_missing: 'Предмет не найден в каталоге.',
    admin_gmforge_not_rolled: 'Для этого предмета не поддерживаются случайные характеристики.',
    admin_gmforge_source_missing: 'Выбранная исходная вещь не найдена.',
    admin_gmforge_not_generated: 'У исходной вещи нет изменяемых характеристик.',
    admin_gmforge_invalid: 'Проверь настройки выдачи.',
  }
  return errors[code] ?? 'Ошибка: ' + code
}

async function loadPlayers(page = 1): Promise<void> {
  if (playerBusy.value) return
  playerBusy.value = true
  try {
    players.value = await adminRequest<Directory>(
      '/api/v1/admin/players?page=' + page + '&search=' + encodeURIComponent(playerSearch.value.trim()),
    )
    playerPage.value = page
  } catch {
    error.value = 'Не удалось загрузить игроков.'
  } finally {
    playerBusy.value = false
  }
}

function selectPlayer(id: number): void {
  recipient.value = String(id)
  showPlayers.value = false
  ownedItems.value = []
}

async function loadOwnedItems(): Promise<void> {
  const bad = validateRecipient()
  if (bad) { error.value = bad; return }
  ownedBusy.value = true
  error.value = ''
  try {
    const response = await adminRequest<{ items: OwnedItem[] }>(
      '/api/v1/admin/players/' + recipient.value.trim() + '/items',
    )
    ownedItems.value = response.items
  } catch {
    error.value = 'Не удалось получить список вещей этого персонажа.'
  } finally {
    ownedBusy.value = false
  }
}

async function copyCommand(): Promise<void> {
  try {
    const lines = queue.value.length
      ? queue.value.map(item => (item.mode === 'regular' ? '/giveitem ' : '/gmforge ')
        + recipient.value.trim() + ' ' + item.specification)
      : [command.value]
    await navigator.clipboard.writeText(lines.join('\n'))
  } catch {
    error.value = 'Не удалось скопировать команды. Выдели их вручную.'
  }
}
</script>

<template>
  <main class="forge-admin">
    <header class="page-header">
      <div>
        <p class="muted-label">Администрирование · Предметы</p>
        <h1>Выдача предметов</h1>
        <p>Выдавай обычные вещи, создавай испытательное снаряжение или копируй предметы игроков.</p>
      </div>
      <div class="header-tag">Только для администратора</div>
    </header>

    <div class="forge-layout">
      <div class="forge-main">
        <section class="panel">
          <h2><span class="step-number">1</span> Кому выдаём?</h2>
          <label for="forge-player">Игрок</label>
          <div class="inline-controls">
            <input id="forge-player" v-model="recipient" inputmode="numeric" placeholder="Telegram ID игрока" />
            <button class="secondary" type="button" @click="showPlayers = !showPlayers; showPlayers && loadPlayers(1)">
              Выбрать из списка
            </button>
          </div>
          <div v-if="showPlayers" class="player-picker">
            <div class="inline-controls">
              <input v-model="playerSearch" aria-label="Поиск игрока" placeholder="Имя персонажа, ник или ID" @keyup.enter="loadPlayers(1)" />
              <button class="secondary" type="button" :disabled="playerBusy" @click="loadPlayers(1)">Поиск</button>
            </div>
            <div v-if="players" class="player-results">
              <button v-for="player in players.players" :key="player.telegramUserId" type="button"
                class="player-choice" @click="selectPlayer(player.telegramUserId)">
                <strong>{{ player.character?.name ?? (player.telegramUsername ? '@' + player.telegramUsername : 'Без персонажа') }}</strong>
                <span>{{ player.character ? 'Ур. ' + player.character.level : 'Без персонажа' }} · {{ player.telegramUserId }}</span>
              </button>
              <p v-if="!players.players.length" class="help">Игроки не найдены.</p>
              <div class="paging">
                <button type="button" :disabled="playerBusy || players.page <= 1" @click="loadPlayers(players.page - 1)">Назад</button>
                <span>Стр. {{ players.page }} · всего {{ players.total }}</span>
                <button type="button" :disabled="playerBusy || players.page * players.pageSize >= players.total" @click="loadPlayers(players.page + 1)">Далее</button>
              </div>
            </div>
          </div>
        </section>

        <section class="panel">
          <h2><span class="step-number">2</span> Что выдаём?</h2>
          <div class="mode-switch" role="group" aria-label="Способ выдачи">
            <button type="button" :class="{ active: mode === 'regular' }" @click="switchMode('regular')">Обычный предмет</button>
            <button type="button" :class="{ active: mode === 'custom' }" @click="switchMode('custom')">С изменёнными статами</button>
            <button type="button" :class="{ active: mode === 'clone' }" @click="switchMode('clone')">Копировать существующий</button>
          </div>

          <template v-if="mode !== 'clone'">
            <label for="forge-search">Найти предмет</label>
            <input id="forge-search" v-model="search" placeholder="Например, меч, руда, зелье или часть сета" />
            <label for="forge-item">Выбрать из каталога</label>
            <select id="forge-item" v-model="itemId">
              <option value="">Выбери предмет…</option>
              <option v-if="itemId && !visibleItems.some(item => item.id === itemId)" :value="itemId">
                {{ selectedItem?.name ?? itemId }}
              </option>
              <option v-for="item in visibleItems" :key="item.id" :value="item.id">{{ item.name }} · {{ item.id }}</option>
            </select>
            <p class="help">{{ allItems.length }} предметов в каталоге. Можно выдавать материалы, расходники, обычную и редкую экипировку.</p>
          </template>
          <template v-else>
            <p class="help">Выбери предмет из инвентаря игрока. Искать его технический GUID вручную не нужно.</p>
            <button type="button" class="secondary" :disabled="ownedBusy" @click="loadOwnedItems">
              {{ ownedBusy ? 'Загружаем…' : 'Показать вещи игрока' }}
            </button>
            <div v-if="ownedItems.length" class="owned-list">
              <button v-for="item in ownedItems" :key="item.id" type="button" class="owned-choice"
                :class="{ chosen: cloneId === item.id }" @click="cloneId = item.id">
                <span><strong>{{ item.name }}</strong><small>{{ item.stars ?? '—' }}★ · +{{ item.enhancementLevel }}{{ item.isEquipped ? ' · Надето' : '' }}</small></span>
                <span>{{ cloneId === item.id ? 'Выбрано ✓' : 'Выбрать' }}</span>
              </button>
            </div>
            <details class="technical">
              <summary>Ввести идентификатор вещи вручную</summary>
              <input id="forge-clone" v-model="cloneId" placeholder="Идентификатор экземпляра (GUID)" />
            </details>
          </template>

          <div class="fields-grid">
            <div>
              <label for="forge-quantity">Сколько штук?</label>
              <input id="forge-quantity" v-model="quantity" type="number" min="1" :max="limit" step="1" />
              <small class="help">Максимум {{ limit }} за одну позицию</small>
            </div>
            <template v-if="mode !== 'regular'">
              <div>
                <label for="forge-quality">Качество</label>
                <select id="forge-quality" v-model="quality">
                  <option value="NORMAL">{{ mode === 'clone' ? 'Сохранить качество' : 'Обычное' }}</option>
                  <option v-if="mode === 'custom'" value="ELITE">Элитное</option>
                  <option v-if="mode === 'custom'" value="BOSS">Как у босса</option>
                  <option value="PERFECT">Максимальное — 100%</option>
                </select>
              </div>
              <div>
                <label for="forge-stars">Звёзды</label>
                <select id="forge-stars" v-model="stars">
                  <option value="auto">{{ mode === 'clone' ? 'Оставить как есть' : 'Определить автоматически' }}</option>
                  <option v-for="number in 5" :key="number" :value="String(number)">{{ number }} ★</option>
                </select>
              </div>
              <div>
                <label for="forge-enhance">Улучшение</label>
                <select id="forge-enhance" v-model="enhance">
                  <option v-if="mode === 'clone'" value="keep">Оставить как есть</option>
                  <option v-for="number in 6" :key="number" :value="String(number - 1)">+{{ number - 1 }}</option>
                </select>
              </div>
            </template>
          </div>
        </section>

        <section v-if="mode !== 'regular'" class="panel">
          <h2><span class="step-number">3</span> Характеристики</h2>
          <p class="help">Можешь оставить обычные случайные характеристики или задать любые значения вручную.</p>
          <div class="preset-row">
            <button class="secondary" type="button" @click="useCrazySword">Пресет: 1500 урона / 150% крит. урона / +5</button>
            <button class="secondary" type="button" @click="quality = 'PERFECT'; stars = '5'">Все показатели — 100%</button>
          </div>
          <div v-for="(row, index) in overrides" :key="row.stat" class="stat-row">
            <label :for="'stat-value-' + index">{{ statName(row.stat) }}</label>
            <input :id="'stat-value-' + index" v-model="row.value" type="number" min="0" max="1000000" step="0.1" placeholder="Значение" />
            <button type="button" class="icon-button" :aria-label="'Удалить ' + statName(row.stat)" @click="overrides.splice(index, 1)">×</button>
          </div>
          <p v-if="!overrides.length" class="empty-info">Пока без ручных характеристик</p>
          <button type="button" class="secondary" @click="pickStats = !pickStats">
            {{ pickStats ? 'Скрыть характеристики' : '+ Добавить характеристику' }}
          </button>
          <div v-if="pickStats" class="stat-picker">
            <input v-model="statSearch" aria-label="Поиск характеристики" placeholder="Найти по названию…" />
            <div class="chips">
              <button v-for="category in ['Все', ...groups.map(group => group.name)]" :key="category"
                type="button" :class="{ active: statCategory === category }" @click="statCategory = category">{{ category }}</button>
            </div>
            <div class="stat-options">
              <button v-for="stat in suggestedStats" :key="stat.id" type="button" @click="addStat(stat.id)">
                <span>+</span> {{ stat.label }}
              </button>
              <p v-if="!suggestedStats.length" class="help">Все подходящие характеристики уже выбраны.</p>
            </div>
          </div>
          <p class="help">Важно: 5 звёзд вручную не меняют значения аффиксов. Для настоящего идеального предмета выбирай качество «Максимальное — 100%».</p>
        </section>

        <section class="panel">
          <h2><span class="step-number">{{ mode === 'regular' ? '3' : '4' }}</span> Выдача</h2>
          <div class="action-row">
            <button class="secondary" type="button" :disabled="busy || queue.length >= 20" @click="addToQueue">+ Добавить в список</button>
            <button type="button" class="primary" :disabled="busy" @click="issue">
              {{ busy ? 'Выдаём…' : queue.length ? 'Выдать всё (' + queue.length + ')' : 'Создать и выдать предмет' }}
            </button>
          </div>
          <p v-if="error" class="message message-error" role="alert">{{ error }}</p>
          <p v-if="result" class="message message-success" role="status">{{ result }}</p>
        </section>
      </div>

      <aside class="forge-aside">
        <section class="panel">
          <h2>Что получится</h2>
          <strong class="preview-name">{{ selectedName }}</strong>
          <p v-if="mode !== 'regular'" class="stars">{{ visualStars === null ? 'Звёзды по генерации' : '★'.repeat(visualStars) }}</p>
          <dl>
            <div><dt>Способ</dt><dd>{{ mode === 'regular' ? 'Обычная выдача' : mode === 'custom' ? 'Испытательный предмет' : 'Копия предмета' }}</dd></div>
            <div><dt>Количество</dt><dd>{{ quantity }}</dd></div>
            <template v-if="mode !== 'regular'">
              <div><dt>Качество</dt><dd>{{ quality === 'PERFECT' ? '100%' : quality === 'NORMAL' ? 'Обычное' : quality === 'ELITE' ? 'Элитное' : 'Босс' }}</dd></div>
              <div><dt>Улучшение</dt><dd>{{ enhance === 'keep' ? 'Как в оригинале' : '+' + enhance }}</dd></div>
              <div v-for="stat in overrides" :key="stat.stat"><dt>{{ statName(stat.stat) }}</dt><dd>+{{ stat.value || '—' }}</dd></div>
            </template>
          </dl>
          <p v-if="mode !== 'regular'" class="help">Испытательные вещи защищены от продажи и обмена, но их можно использовать в кузнице и уничтожать.</p>
          <div class="forge-command">
            <details class="technical">
              <summary>Техническая команда</summary>
              <code>{{ command }}</code>
              <button class="secondary" type="button" @click="copyCommand">Скопировать</button>
            </details>
          </div>
        </section>
        <section class="panel">
          <div class="queue-header">
            <h2>Список выдачи</h2><span>{{ queue.length }} / 20</span>
          </div>
          <p v-if="!queue.length" class="help">Добавь несколько разных предметов. Они будут выданы одной операцией.</p>
          <div v-for="(line, index) in queue" :key="index" class="queue-line">
            <span><strong>{{ line.title }}</strong><small>{{ line.mode === 'regular' ? 'Обычный' : 'Испытательный' }}</small></span>
            <button type="button" :aria-label="'Убрать позицию ' + (index + 1)" @click="removeFromQueue(index)">×</button>
          </div>
          <button v-if="queue.length" type="button" class="secondary" @click="queue = []; retryRequestId = null">Очистить список</button>
        </section>
      </aside>
    </div>
  </main>
</template>

<style scoped>
.forge-admin { max-width:1240px; margin:0 auto; padding:18px 14px 48px; color:#f0ecf5; }
.page-header { display:flex; justify-content:space-between; align-items:start; gap:18px; margin-bottom:20px; }
.page-header h1 { font-size:clamp(25px,3vw,36px); margin:6px 0; }
.page-header p { color:#aba5b8; line-height:1.5; margin:0; }
.muted-label { color:#bba4d9!important; font-size:11px; letter-spacing:.08em; }
.header-tag { font-size:12px; border:1px solid #695079; padding:8px 12px; border-radius:99px; white-space:nowrap; color:#d9c5ed; }
.forge-layout { display:grid; grid-template-columns:minmax(0,1.55fr) minmax(265px,.9fr); gap:15px; align-items:start; }
.forge-main,.forge-aside { display:grid; gap:15px; min-width:0; }
.panel { background:#201d28; border:1px solid #38313f; border-radius:15px; padding:20px; min-width:0; }
.panel h2 { margin:0 0 18px; font-size:18px; display:flex; align-items:center; gap:10px; }
.step-number { display:inline-grid; place-items:center; width:27px; height:27px; border-radius:50%; background:#4c3762; color:#ede0fb; font-size:13px; }
.panel label { display:block; font-size:13px; margin:16px 0 7px; color:#b9b1c5; }
.panel input,.panel select { width:100%; background:#14121a; border:1px solid #554b60; color:#f5f1fa; border-radius:9px; min-height:43px; padding:9px 12px; box-sizing:border-box; font:inherit; }
button { cursor:pointer; font:inherit; }
button:disabled { opacity:.52; cursor:not-allowed; }
button:focus-visible,input:focus-visible,select:focus-visible { outline:2px solid #b894e0; outline-offset:2px; }
.secondary,.primary { padding:11px 14px; border-radius:9px; min-height:42px; color:#f4eefb; }
.secondary { background:#30283d; border:1px solid #655477; }
.primary { background:#7954a0; border:1px solid #9772bc; font-weight:700; }
.inline-controls,.action-row,.preset-row { display:flex; gap:9px; flex-wrap:wrap; align-items:center; }
.inline-controls input { flex:1 1 170px; width:auto; }
.mode-switch { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:8px; margin-bottom:18px; }
.mode-switch button { min-height:58px; background:#17131d; border:1px solid #494052; border-radius:9px; color:#c8bed5; padding:9px; }
.mode-switch button.active { border-color:#ac84d1; background:#3a2b4d; color:white; }
.fields-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); column-gap:12px; }
.help { color:#a59eaf; font-size:12px; line-height:1.6; }
.fields-grid small { display:block; margin-top:5px; }
.stat-row { display:grid; grid-template-columns:minmax(0,1.4fr) minmax(95px,.6fr) 33px; gap:8px; align-items:end; margin:9px 0; }
.stat-row label { padding-bottom:12px; margin:0; }
.icon-button { min-height:43px; background:#3d2a38; color:#f5dde2; border:1px solid #674a59; border-radius:8px; font-size:20px; }
.empty-info { color:#8e889a; font-size:13px; }
.stat-picker,.player-picker { margin-top:12px; border:1px solid #403747; border-radius:11px; padding:13px; background:#18151f; }
.chips { display:flex;flex-wrap:wrap;gap:6px;margin:11px 0; }
.chips button { padding:7px 10px; background:#30283d; border:1px solid #453a52; border-radius:99px; color:#e5dced; font-size:12px; }
.chips button.active { background:#65507c; }
.stat-options { display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:7px;max-height:240px;overflow-y:auto; }
.stat-options button { border:1px solid #4c4154; background:#272131; color:#ddd1eb; text-align:left; border-radius:8px; padding:10px; font-size:12px; }
.stat-options button span { color:#cbace8; font-weight:700; }
.player-results,.owned-list { max-height:265px; overflow:auto; display:grid;gap:7px;margin-top:11px; }
.player-choice,.owned-choice { background:#282230; border:1px solid #4a3b57; color:#f2eefa; padding:10px; border-radius:8px; display:flex;justify-content:space-between;gap:10px;align-items:center;text-align:left; }
.player-choice { flex-direction:column;align-items:stretch; }
.player-choice span,.owned-choice small { color:#ada0bb; font-size:12px; }
.owned-choice span:first-child { display:grid;gap:4px; }
.owned-choice.chosen { border-color:#bc91e1;background:#433253; }
.paging { display:flex;justify-content:space-between;align-items:center;gap:8px;margin-top:12px;font-size:12px; }
.paging button { background:#30283d;border:1px solid #50415d;color:#e5dded;padding:6px 9px;border-radius:7px; }
.technical { color:#a79db6; font-size:12px; margin-top:15px; }
.technical summary { cursor:pointer; padding:9px 0; }
.technical code { overflow-wrap:anywhere;display:block;background:#131119;padding:10px;border-radius:8px;margin:8px 0; }
.forge-aside { position:sticky;top:14px; }
.preview-name { display:block;font-size:17px;overflow-wrap:anywhere; }
.stars { letter-spacing:2px;color:#e1c178; }
.panel dl > div { display:flex;align-items:start;justify-content:space-between;gap:12px;padding:9px 0;border-bottom:1px solid #39313f;font-size:13px; }
.panel dt { color:#b0a9bb; }
.panel dd { text-align:right;margin:0;max-width:56%;overflow-wrap:anywhere; }
.queue-header { display:flex;justify-content:space-between;align-items:center; }
.queue-header h2 { margin:0; }
.queue-header span { color:#bba7d2;font-size:12px; }
.queue-line { display:flex;justify-content:space-between;gap:8px;align-items:center;border-bottom:1px solid #39313f;padding:9px 0; }
.queue-line span { display:grid;gap:3px;font-size:13px; }
.queue-line small { color:#a8a0b3; }
.queue-line button { background:none;border:0;color:#ebadac;font-size:20px; }
.message { padding:11px;border-radius:8px;overflow-wrap:anywhere; }
.message-error { color:#ffc4c1;background:#40272b; }
.message-success { color:#b5f1cb;background:#243d33; }
@media(max-width:840px) { .forge-layout { grid-template-columns:1fr; } .forge-aside { position:static; } }
@media(max-width:540px) {
  .panel { padding:15px; }
  .page-header { flex-direction:column; }
  .mode-switch { grid-template-columns:1fr; }
  .mode-switch button { min-height:40px; }
  .stat-options { grid-template-columns:1fr; }
  .action-row > button { flex:1 1 150px; }
  .stat-row { grid-template-columns:minmax(0,1fr) 100px 33px; }
}
</style>