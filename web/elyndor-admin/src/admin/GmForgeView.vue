<script setup lang="ts">
import { computed, ref } from 'vue'
import { adminRequest, type ContentAdminCurrent } from '@/api'

const props = defineProps<{ defaultTelegramId: string; packageJson: ContentAdminCurrent['payloadJson'] }>()
type ItemOption = { id: string; name: string }
type Override = { stat: string; value: string }
type ForgeResponse = { code: string; message: string; isDuplicate: boolean }

const mode = ref<'new' | 'clone'>('new')
const recipient = ref(props.defaultTelegramId)
const itemId = ref('')
const cloneId = ref('')
const search = ref('')
const quality = ref('NORMAL')
const stars = ref('auto')
const enhance = ref('0')
const overrides = ref<Override[]>([])
const busy = ref(false)
const error = ref('')
const result = ref<ForgeResponse | null>(null)

const statIds = [
  'WEAPON_DAMAGE', 'CRITICAL_DAMAGE', 'CRITICAL_CHANCE', 'STRENGTH',
  'AGILITY', 'INTELLECT', 'STAMINA', 'ATTACK_POWER', 'SPELL_POWER',
  'MAX_HP', 'MAX_RESOURCE', 'ATTACK_SPEED', 'ACCURACY', 'ARMOR',
  'MAGIC_RESISTANCE', 'DODGE', 'ARMOR_PENETRATION', 'MAGIC_PENETRATION',
  'BLOCK_CHANCE', 'BLOCK_VALUE', 'PHYSICAL_VAMPIRISM',
  'MAGICAL_VAMPIRISM', 'UNIVERSAL_VAMPIRISM',
]
const statNames: Record<string, string> = {
  WEAPON_DAMAGE: 'Урон оружия', CRITICAL_DAMAGE: 'Крит. урон (%)',
  CRITICAL_CHANCE: 'Крит. шанс (%)', STRENGTH: 'Сила',
  AGILITY: 'Ловкость', INTELLECT: 'Интеллект', STAMINA: 'Выносливость',
  ATTACK_POWER: 'Сила атаки', SPELL_POWER: 'Сила заклинаний',
  MAX_HP: 'Здоровье', MAX_RESOURCE: 'Ресурс', ATTACK_SPEED: 'Скорость атаки (%)',
  ACCURACY: 'Меткость (%)', ARMOR: 'Броня', MAGIC_RESISTANCE: 'Сопротивление магии',
  DODGE: 'Уклонение (%)', ARMOR_PENETRATION: 'Пробивание брони (%)',
  MAGIC_PENETRATION: 'Магическое пробивание (%)',
  BLOCK_CHANCE: 'Шанс блока (%)', BLOCK_VALUE: 'Величина блока',
  PHYSICAL_VAMPIRISM: 'Физ. вампиризм (%)',
  MAGICAL_VAMPIRISM: 'Маг. вампиризм (%)',
  UNIVERSAL_VAMPIRISM: 'Общий вампиризм (%)',
}

const allItems = computed<ItemOption[]>(() => {
  try {
    const parsed: unknown = JSON.parse(props.packageJson)
    if (!parsed || typeof parsed !== 'object') return []
    const items = (parsed as { items?: unknown }).items
    if (!Array.isArray(items)) return []
    return items
      .filter((item): item is Record<string, unknown> =>
        !!item && typeof item === 'object' && typeof item.id === 'string'
        && String(item.type).toUpperCase() === 'EQUIPMENT'
        && String(item.generationMode).toUpperCase() !== 'FIXED')
      .map(item => ({ id: String(item.id), name: String(item.name ?? item.id) }))
  } catch {
    return []
  }
})

const matchedItems = computed(() => {
  const query = search.value.trim().toLowerCase()
  return allItems.value
    .filter(item => !query || (item.id + ' ' + item.name).toLowerCase().includes(query))
    .slice(0, 40)
})

const specification = computed(() => {
  const base = mode.value === 'clone'
    ? 'clone:' + cloneId.value.trim()
    : itemId.value.trim().toUpperCase()
  const parameters: string[] = [base, 'quality=' + quality.value]
  if (stars.value !== 'auto') parameters.push('stars=' + stars.value)
  if (enhance.value !== 'keep') parameters.push('enhance=' + enhance.value)
  for (const override of overrides.value) {
    if (override.value.trim() !== '') parameters.push(override.stat + '=' + override.value.trim())
  }
  return parameters.join(' ')
})
const command = computed(() => '/gmforge ' + recipient.value.trim() + ' ' + specification.value)
const previewStars = computed(() => quality.value === 'PERFECT' ? 5 : stars.value === 'auto' ? null : Number(stars.value))

function addOverride(): void {
  const stat = statIds.find(id => !overrides.value.some(row => row.stat === id))
  if (stat) overrides.value.push({ stat, value: '' })
}

function useCrazySword(): void {
  quality.value = 'PERFECT'
  stars.value = '5'
  enhance.value = '5'
  overrides.value = [
    { stat: 'WEAPON_DAMAGE', value: '1500' },
    { stat: 'CRITICAL_DAMAGE', value: '150' },
  ]
}

function validate(): string | null {
  if (!/^[1-9]\d*$/.test(recipient.value.trim())) return 'Нужен Telegram ID получателя.'
  if (mode.value === 'new' && !itemId.value.trim()) return 'Выбери ID предмета.'
  if (mode.value === 'clone'
      && !/^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(cloneId.value.trim()))
    return 'Для копирования нужен GUID экземпляра предмета.'
  if (quality.value === 'PERFECT' && stars.value !== 'auto' && stars.value !== '5')
    return 'Идеальный предмет всегда имеет 5 звёзд.'
  const seen = new Set<string>()
  for (const row of overrides.value) {
    if (seen.has(row.stat)) return 'Повторяющийся аффикс: ' + row.stat
    seen.add(row.stat)
    const value = row.value.trim()
    if (!/^\d+(?:\.\d+)?$/.test(value) || Number(value) > 1_000_000)
      return 'Некорректное значение для ' + row.stat + '. Допустимо 0–1 000 000.'
  }
  return null
}

async function forge(): Promise<void> {
  error.value = ''
  result.value = null
  const issue = validate()
  if (issue) { error.value = issue; return }

  busy.value = true
  try {
    result.value = await adminRequest<ForgeResponse>('/api/v1/admin/gm-forge/create', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        targetTelegramUserId: Number(recipient.value.trim()),
        specification: specification.value,
        requestId: crypto.randomUUID(),
      }),
    })
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : 'Не удалось создать предмет.'
  } finally {
    busy.value = false
  }
}

async function copyCommand(): Promise<void> {
  error.value = ''
  try {
    await navigator.clipboard.writeText(command.value)
  } catch {
    error.value = 'Не удалось скопировать команду. Выдели её вручную.'
  }
}
</script>

<template>
  <div class="forge">
    <header class="forge-head">
      <div>
        <p class="eyebrow">OPERATIONS / SUPER_ADMIN</p>
        <h1>GM Forge</h1>
        <p>Лаборатория экипировки: честные роллы или любые характеристики для тестирования боя.</p>
      </div>
      <span class="forge-tag">DEV ONLY</span>
    </header>

    <div class="forge-cols">
      <section class="forge-card">
        <div class="forge-tabs">
          <button type="button" :class="{ selected: mode === 'new' }" @click="mode = 'new'; enhance = '0'; quality = 'NORMAL'">Создать предмет</button>
          <button type="button" :class="{ selected: mode === 'clone' }" @click="mode = 'clone'; enhance = 'keep'; quality = 'NORMAL'">Копировать существующий</button>
        </div>

        <label for="forge-player">Telegram ID получателя</label>
        <input id="forge-player" v-model="recipient" inputmode="numeric" placeholder="123456789" />

        <template v-if="mode === 'new'">
          <label for="forge-search">Поиск экипировки</label>
          <input id="forge-search" v-model="search" placeholder="Название или ID…" />
          <label for="forge-item">Предмет</label>
          <select id="forge-item" v-model="itemId">
            <option value="">Выбери предмет</option>
            <option v-if="itemId && !matchedItems.some(x => x.id === itemId)" :value="itemId">{{ itemId }}</option>
            <option v-for="item in matchedItems" :key="item.id" :value="item.id">{{ item.name }} · {{ item.id }}</option>
          </select>
          <small v-if="!allItems.length">Каталог недоступен — введи ID вручную:</small>
          <input v-if="!allItems.length" v-model="itemId" placeholder="ITEM_ID" />
        </template>
        <template v-else>
          <label for="forge-clone">GUID существующего предмета</label>
          <input id="forge-clone" v-model="cloneId" placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" />
          <small>Новая копия появится в инвентаре, оригинал останется без изменений.</small>
        </template>

        <div class="forge-grid">
          <div>
            <label for="forge-quality">Профиль качества</label>
            <select id="forge-quality" v-model="quality">
              <option value="NORMAL">{{ mode === 'clone' ? 'Сохранить роллы' : 'Обычный' }}</option>
              <option v-if="mode === 'new'" value="ELITE">Элитный</option>
              <option v-if="mode === 'new'" value="BOSS">Босс</option>
              <option value="PERFECT">Идеальный — 100%</option>
            </select>
          </div>
          <div>
            <label for="forge-stars">Звёзды</label>
            <select id="forge-stars" v-model="stars">
              <option value="auto">{{ mode === 'clone' ? 'Как в оригинале' : 'По генерации' }}</option>
              <option v-for="value in 5" :key="value" :value="String(value)">{{ value }} ★</option>
            </select>
          </div>
          <div>
            <label for="forge-enhance">Улучшение</label>
            <select id="forge-enhance" v-model="enhance">
              <option v-if="mode === 'clone'" value="keep">Как в оригинале</option>
              <option v-for="value in 6" :key="value" :value="String(value - 1)">+{{ value - 1 }}</option>
            </select>
          </div>
        </div>

        <div class="forge-row">
          <h2>Аффиксы</h2>
          <button class="forge-subtle" type="button" @click="addOverride">+ Добавить</button>
        </div>
        <div v-for="(row, index) in overrides" :key="index" class="forge-affix">
          <select v-model="row.stat" :aria-label="'Стат аффикса ' + (index + 1)">
            <option v-for="stat in statIds" :key="stat" :value="stat">{{ statNames[stat] ?? stat }}</option>
          </select>
          <input v-model="row.value" type="number" min="0" max="1000000" step="0.1" :aria-label="'Значение аффикса ' + (index + 1)" />
          <button type="button" aria-label="Удалить аффикс" @click="overrides.splice(index, 1)">×</button>
        </div>
        <p v-if="!overrides.length" class="forge-muted">Без ручных аффиксов — генерация по стандартным правилам.</p>
        <button type="button" class="forge-subtle" @click="useCrazySword">Пресет: 1500 урона / 150% крит. урона / +5</button>
      </section>

      <aside class="forge-card forge-preview">
        <p class="eyebrow">ПРЕДПРОСМОТР ПАРАМЕТРОВ</p>
        <h2>{{ mode === 'new' ? (allItems.find(x => x.id === itemId)?.name ?? itemId) : 'Копия предмета' }}</h2>
        <p class="forge-stars">{{ previewStars === null ? (mode === 'clone' ? '★ как в оригинале' : '★ по генерации') : '★'.repeat(previewStars) }}</p>
        <dl>
          <div><dt>Качество</dt><dd>{{ quality === 'PERFECT' ? '100% роллов' : quality }}</dd></div>
          <div><dt>Улучшение</dt><dd>{{ enhance === 'keep' ? 'Как в оригинале' : '+' + enhance }}</dd></div>
          <div v-for="row in overrides" :key="row.stat"><dt>{{ statNames[row.stat] ?? row.stat }}</dt><dd>+{{ row.value || '—' }}</dd></div>
        </dl>
        <p class="forge-muted">Ручные значения — бонусы к статам, а не проценты качества. Например, WEAPON_DAMAGE=1500 добавит 1500 к обоим значениям урона оружия.</p>
        <p v-if="stars !== 'auto' && quality !== 'PERFECT'" class="forge-muted">Установка звёзд вручную изменяет классификацию, но не качество аффиксов. Для настоящего максимального ролла используй «Идеальный — 100%».</p>
        <div class="forge-warning">Тестовый предмет привязан и заблокирован от продажи, обмена и аукциона. Используй тестового персонажа для боёв с наградами.</div>
        <button class="forge-primary" type="button" :disabled="busy" @click="forge">
          {{ busy ? 'Создаётся…' : 'Создать и выдать предмет' }}
        </button>
        <p v-if="error" class="forge-error" role="alert">{{ error }}</p>
        <p v-if="result" class="forge-success" role="status">{{ result.message }}</p>
        <div class="forge-command">
          <code>{{ command }}</code>
          <button type="button" @click="copyCommand">Скопировать Telegram-команду</button>
        </div>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.forge { padding: 24px; max-width: 1240px; margin: 0 auto; color: #edeaf3; }
.forge-head { display: flex; justify-content: space-between; gap: 16px; align-items: start; margin-bottom: 22px; }
.forge-head h1 { font-size: clamp(28px, 3vw, 38px); margin: 6px 0; }
.forge-head p { color: #aaa4b5; margin: 0; line-height: 1.55; }
.forge-tag { border: 1px solid #856e44; color: #dbc18a; border-radius: 999px; padding: 7px 12px; font-size: 11px; white-space: nowrap; }
.forge-cols { display: grid; grid-template-columns: minmax(0,1.2fr) minmax(280px,.8fr); gap: 18px; align-items: start; }
.forge-card { min-width: 0; border: 1px solid #34303b; background: #211e28; border-radius: 16px; padding: 22px; }
.forge-card label { display: block; color: #bdb6c9; font-size: 13px; margin: 16px 0 7px; }
.forge-card input, .forge-card select { width: 100%; box-sizing: border-box; padding: 12px; border: 1px solid #453e4d; border-radius: 8px; color: #f1edf5; background: #17151c; font: inherit; }
.forge-card select { min-height: 43px; }
.forge-card small { display: block; color: #8d8698; margin-top: 7px; }
.forge-tabs { display: flex; gap: 8px; margin-bottom: 16px; }
.forge-tabs button, .forge-subtle, .forge-affix button, .forge-command button { border-radius: 8px; border: 1px solid #52475e; background: #2e2738; color: #d9d0e8; padding: 10px; cursor: pointer; }
.forge-tabs button { flex: 1; }
.forge-tabs .selected { background: #49345b; border-color: #9a79b5; color: #fff; }
.forge-grid { display: grid; grid-template-columns: repeat(3,minmax(0,1fr)); gap: 12px; margin-top: 8px; }
.forge-row { display: flex; align-items: center; justify-content: space-between; margin: 24px 0 12px; }
.forge-row h2 { margin: 0; font-size: 18px; }
.forge-affix { display: grid; grid-template-columns: minmax(0,1.4fr) minmax(75px,.6fr) 32px; gap: 8px; margin-bottom: 10px; }
.forge-affix button { padding: 0; font-size: 18px; }
.forge-muted { color: #9d96aa; font-size: 13px; line-height: 1.55; }
.forge-preview h2 { font-size: 19px; overflow-wrap: anywhere; }
.forge-stars { color: #e6bb64; font-size: 23px; letter-spacing: 2px; }
.forge-preview dl > div { display: flex; justify-content: space-between; padding: 8px 0; gap: 12px; border-bottom: 1px solid #39333f; }
.forge-preview dt { color: #a9a1b4; }
.forge-preview dd { margin: 0; text-align: right; }
.forge-warning { border-left: 3px solid #a4834d; background: #332b26; color: #dec9aa; padding: 12px; font-size: 13px; line-height: 1.5; margin: 18px 0; }
.forge-primary { background: #8c62b0; border: 0; border-radius: 9px; color: white; width: 100%; font-weight: 700; padding: 13px; cursor: pointer; }
.forge-primary:disabled { opacity: .55; cursor: progress; }
.forge-error { color: #ffaaa8; overflow-wrap: anywhere; }
.forge-success { color: #a2e3b8; overflow-wrap: anywhere; }
.forge-command { margin-top: 22px; display: flex; flex-direction: column; gap: 8px; }
.forge-command code { overflow-wrap: anywhere; font-size: 12px; line-height: 1.5; background: #121117; padding: 12px; border-radius: 8px; }
@media(max-width: 860px) { .forge-cols { grid-template-columns: 1fr; } .forge { padding: 14px; } .forge-card { padding: 16px; } }
@media(max-width: 500px) { .forge-grid { grid-template-columns: 1fr 1fr; } .forge-head { flex-direction: column; } }
</style>
