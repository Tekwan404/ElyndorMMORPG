<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'

import { ApiRequestError } from '@/api/apiClient'
import { locationPresentation } from '@/game/world/locationPresentation'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UILoadingState } from '@/ui/components'
import ItemIcon from '@/game/items/components/ItemIcon.vue'
import professionAtlas from '@/assets/professions/workshop-atlas.png'
import { recipeBlockReason, usableMaterialQuantities } from '../professionWorkshop'
import {
  craftProfessionRecipe,
  gatherProfessionNode,
  disenchantProfessionItem,
  enchantProfessionItem,
  forgetProfession,
  type ProfessionGatheringNode,
  getProfessionState,
  learnProfession,
  type ProfessionMutationResult,
  type ProfessionRecipeState,
  type ProfessionStateSnapshot,
} from '@/game/professions/professionApi'

const props = defineProps<{ initialProfessionId?: string }>()
const session = useGameSessionStore()
const state = ref<ProfessionStateSnapshot | null>(null)
const loading = ref(true)
const pendingKey = ref<string | null>(null)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const category = ref<'all' | 'materials' | 'equipment'>('all')

const selectedProfessionId = ref<string | null>(props.initialProfessionId ?? null)
const selectedItemId = ref('')
const confirmDisenchant = ref(false)
const forgetId = ref<string | null>(null)
const now = ref(Date.now())
const clockTimer = setInterval(() => {
  now.value = Date.now()
}, 1000)
onUnmounted(() => clearInterval(clockTimer))
const presentation: Record<string, { description: string; art: number; category: string }> = {
  SKINNING: {
    description: 'Снимайте шкуры с поверженных зверей. Сырьё для кожевничества.',
    art: 0,
    category: 'СОБИРАТЕЛЬСТВО',
  },
  LEATHERWORKING: {
    description: 'Выделывайте кожу и сшейте снаряжение для охоты и приключений.',
    art: 1,
    category: 'РЕМЕСЛО',
  },
  MINING: {
    description: 'Находите рудные жилы, добывайте руду и выплавляйте слитки.',
    art: 2,
    category: 'СОБИРАТЕЛЬСТВО',
  },
  BLACKSMITHING: {
    description: 'Превращайте слитки в оружие и тяжёлые доспехи у наковальни.',
    art: 3,
    category: 'РЕМЕСЛО',
  },
  ENCHANTING: {
    description: 'Распылите ненужное снаряжение и наделите доспехи магией.',
    art: 4,
    category: 'РЕМЕСЛО',
  },
}
const professionCatalog = computed(() =>
  (
    state.value?.available ?? [
      { id: 'SKINNING', name: 'Снятие шкур', maxSkill: 300 },
      { id: 'LEATHERWORKING', name: 'Кожевничество', maxSkill: 300 },
      { id: 'MINING', name: 'Горное дело', maxSkill: 300 },
      { id: 'BLACKSMITHING', name: 'Кузнечное дело', maxSkill: 300 },
      { id: 'ENCHANTING', name: 'Наложение чар', maxSkill: 300 },
    ]
  )
    .map((profession) => ({
      ...profession,
      ...presentation[profession.id],
      learned: state.value?.learned.find((item) => item.id === profession.id),
    }))
    .sort(
      (a, b) =>
        Number(b.id === props.initialProfessionId) - Number(a.id === props.initialProfessionId),
    ),
)
const activeProfession = computed(() => {
  if (selectedProfessionId.value)
    return state.value?.learned.find((item) => item.id === selectedProfessionId.value) ?? null
  return (
    state.value?.learned.find((item) => item.id !== 'SKINNING') ?? state.value?.learned[0] ?? null
  )
})
const enchantingItems = computed(() =>
  (session.snapshot?.character?.inventory.items ?? []).filter(
    (item) =>
      item.type === 'Equipment' && !item.isLocked && !item.equippedSlot && !item.transactionLocked,
  ),
)
const selectedItem = computed(() =>
  enchantingItems.value.find((item) => item.id === selectedItemId.value),
)
const gatheringCooldown = computed(
  () =>
    Math.max(0, Math.ceil((Date.parse(state.value?.nextGatherAtUtc ?? '') - now.value) / 1000)) ||
    0,
)
watch(selectedItemId, () => {
  confirmDisenchant.value = false
})

const skinning = computed(() => state.value?.learned.find((item) => item.id === 'SKINNING') ?? null)
const currentLocationId = computed(() => session.snapshot?.world?.currentLocation.id ?? null)
const inventoryQuantities = computed(() =>
  usableMaterialQuantities(session.snapshot?.character?.inventory.items ?? []),
)
const materialIds = computed(
  () =>
    new Set(
      state.value?.materials?.map((item) => item.id) ?? [
        'ROUGH_LEATHER',
        'LIGHT_LEATHER',
        'THICK_LEATHER',
      ],
    ),
)
const recipes = computed(() =>
  (state.value?.recipes ?? []).filter(
    (recipe) =>
      recipe.professionId === activeProfession.value?.id &&
      (category.value === 'all' ||
        (category.value === 'materials') === materialIds.value.has(recipe.outputItemId)),
  ),
)
const gatheringRoutes = computed(() => {
  const routes = new Map<
    string,
    { itemName: string; locationName: string; requiredSkill: number; skillUpUntil?: number }
  >()
  for (const source of state.value?.materialSources ?? []) {
    if (state.value?.gatheringNodes?.some((node) => node.itemId === source.itemId)) continue
    const key = `${source.itemId}:${source.locationId}`
    const previous = routes.get(key)
    if (!previous || source.requiredSkill < previous.requiredSkill) routes.set(key, source)
  }
  return [...routes.values()]
})
const maximumGatheringStep = computed(() =>
  Math.max(0, ...gatheringRoutes.value.map((route) => route.skillUpUntil ?? 0)),
)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    state.value = await getProfessionState()
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    loading.value = false
  }
}

async function learn(id: string): Promise<void> {
  await mutate(
    `learn:${id}`,
    () => learnProfession(id),
    () => {
      selectedProfessionId.value = id
      const name = professionCatalog.value.find((item) => item.id === id)?.name ?? 'Профессия'
      return `${name} изучено.`
    },
  )
}

async function craft(recipe: ProfessionRecipeState): Promise<void> {
  await mutate(
    `craft:${recipe.id}`,
    () => craftProfessionRecipe(recipe.id),
    (result) =>
      `Создано: ${recipe.name} ×${result.quantity}${result.skillIncreased ? ' · навык +1' : ''}`,
    true,
  )
}

async function mutate(
  key: string,
  action: () => Promise<ProfessionMutationResult>,
  successMessage: (result: ProfessionMutationResult) => string,
  refreshCharacter = false,
): Promise<void> {
  if (pendingKey.value) return
  pendingKey.value = key
  error.value = null
  notice.value = null
  try {
    const result = await action()
    if (result.state) state.value = result.state
    else state.value = await getProfessionState()
    if (refreshCharacter) await session.refreshSnapshot()
    notice.value = successMessage(result)
  } catch (caught) {
    const failureMessage = errorMessage(caught)
    await load()
    error.value = failureMessage
  } finally {
    pendingKey.value = null
  }
}

function canCraft(recipe: ProfessionRecipeState): boolean {
  return blockReason(recipe) === null
}

function blockReason(recipe: ProfessionRecipeState): string | null {
  return recipeBlockReason(
    recipe,
    state.value?.learned.find((item) => item.id === recipe.professionId)?.skill ?? null,
    currentLocationId.value,
    inventoryQuantities.value,
  )
}

function materialGuide(itemId: string): string {
  const process = state.value?.recipes.find((recipe) => recipe.outputItemId === itemId)
  const sourceIds = process ? process.ingredients.map((ingredient) => ingredient.itemId) : [itemId]
  const sources =
    state.value?.materialSources?.filter((source) => sourceIds.includes(source.itemId)) ?? []
  const locations = [...new Set(sources.map((source) => source.locationName))]
  if (locations.length === 0) return ''
  return `${process ? `Выделка: ${process.name}. Сырьё` : 'Добыча'}: ${locations.join(', ')}.`
}

function ingredientOwned(itemId: string): number {
  return inventoryQuantities.value.get(itemId) ?? 0
}

function locationLabel(id: string | null): string {
  if (!id) return 'без ограничения'
  if (id === 'STARTER_TOWN') return 'Городская мастерская'
  return locationPresentation(id).label
}

function itemLabel(id: string | null): string {
  if (!id) return 'Материал'
  const canonical = state.value?.materials?.find((item) => item.id === id)?.name
  if (canonical) return canonical
  return (
    (
      {
        ROUGH_HIDE: 'Грубая шкура',
        LIGHT_HIDE: 'Лёгкая шкура',
        THICK_HIDE: 'Толстая шкура',
        ROUGH_LEATHER: 'Грубая кожа',
        LIGHT_LEATHER: 'Лёгкая кожа',
        THICK_LEATHER: 'Толстая кожа',
        WOLF_HIDE: 'Волчья шкура',
        BOAR_HIDE: 'Кабанья шкура',
        CHITIN_FRAGMENT: 'Фрагмент хитина',
      } as Record<string, string>
    )[id] ?? 'Материал'
  )
}

function errorMessage(caught: unknown): string {
  const code = caught instanceof ApiRequestError ? caught.code : 'network_unavailable'
  return (
    (
      {
        profession_gathering_cooldown: 'Жила ещё восстанавливается. Подождите минуту после добычи.',
        profession_wrong_location: 'Перейдите в локацию с нужной рудой и завершите путешествие.',
        profession_item_not_found: 'Предмет больше недоступен. Выберите другой.',
        profession_item_protected: 'Снимите предмет и уберите блокировку перед действием.',
        profession_invalid_equipment: 'Распыление доступно для обычной и необычной экипировки.',
        profession_enchantment_slot: 'Эти чары подходят для груди, перчаток или обуви.',
        profession_limit_reached: 'Можно изучить не больше двух профессий.',
        profession_already_learned: 'Эта профессия уже изучена.',
        profession_not_learned: 'Сначала изучите нужную профессию.',
        skinning_corpse_not_found: 'Эта туша больше недоступна.',
        skinning_corpse_expired: 'Время для снятия шкуры истекло.',
        skinning_corpse_already_skinned: 'Шкура с этой туши уже снята.',
        profession_skill_too_low: 'Навык профессии пока слишком низкий.',
        profession_wrong_workshop: 'Для изготовления нужна городская мастерская.',
        profession_missing_ingredients: 'Не хватает материалов.',
        inventory_full: 'В инвентаре нет свободного места.',
        profession_idempotency_conflict:
          'Состояние изменилось. Обновите экран и повторите действие.',
        network_unavailable: 'Не удалось связаться с сервером.',
      } as Record<string, string>
    )[code] ?? 'Не удалось выполнить действие.'
  )
}

function selectProfession(id: string): void {
  selectedProfessionId.value = id
  category.value = 'all'
}
function nodeBlockReason(node: ProfessionGatheringNode): string | null {
  if ((activeProfession.value?.skill ?? 0) < node.requiredSkill)
    return `Требуется навык ${node.requiredSkill}.`
  if (currentLocationId.value !== node.locationId)
    return `Добыча: ${locationLabel(node.locationId)}.`
  if (gatheringCooldown.value) return `Жила восстанавливается: ${gatheringCooldown.value} с.`
  return null
}
async function gather(node: ProfessionGatheringNode): Promise<void> {
  await mutate(
    `gather:${node.id}`,
    () => gatherProfessionNode(node.id),
    (result) =>
      result.replayed
        ? 'Добыча уже учтена.'
        : `Добыто: ${itemLabel(node.itemId)} ×${result.quantity}${result.skillIncreased ? ' · навык +1' : ''}`,
    true,
  )
}
async function disenchant(): Promise<void> {
  if (!selectedItem.value) return
  const id = selectedItem.value.id
  confirmDisenchant.value = false
  await mutate(
    `disenchant:${id}`,
    () => disenchantProfessionItem(id),
    (result) =>
      result.replayed
        ? 'Распыление уже выполнено.'
        : `Получено: ${itemLabel(result.itemId)} ×${result.quantity}`,
    true,
  )
  selectedItemId.value = ''
}
async function applyEnchantment(id: string): Promise<void> {
  if (!selectedItem.value) return
  const target = selectedItem.value.id
  await mutate(
    `enchant:${id}`,
    () => enchantProfessionItem(target, id),
    () => 'Чары наложены. Предыдущие чары заменены.',
    true,
  )
}
async function forget(id: string): Promise<void> {
  forgetId.value = null
  await mutate(
    `forget:${id}`,
    () => forgetProfession(id),
    () => 'Профессия забыта. При повторном изучении навык начнётся с 1.',
  )
  selectedProfessionId.value = null
}
onMounted(load)
</script>

<template>
  <section class="professions-view" data-profession-workshop>
    <header class="professions-hero">
      <div>
        <small>РЕМЁСЛА ЭЛИНДОРА</small>
        <h2>Профессии</h2>
        <p>От рудной жилы до зачарованных доспехов. Выберите своё ремесло.</p>
      </div>
      <span>{{ state?.learned.length ?? 0 }} / 2</span>
    </header>

    <UILoadingState v-if="loading && !state" state="loading" title="Загружаем профессии" />

    <template v-else-if="state">
      <p v-if="notice" class="profession-message profession-message--success" role="status">
        {{ notice }}
      </p>
      <p v-if="error" class="profession-message profession-message--error">{{ error }}</p>

      <p class="profession-intro">
        Две основные профессии · навык до 300. Добыча и ремесло дополняют друг друга.
      </p>
      <p
        v-if="selectedProfessionId && !activeProfession"
        class="profession-intro"
        data-workshop-unlearned
      >
        Для этой мастерской изучите профессию «{{
          professionCatalog.find((item) => item.id === selectedProfessionId)?.name
        }}».
      </p>
      <section class="profession-slots" aria-label="Профессии Элиндора">
        <article
          v-for="profession in professionCatalog"
          :key="profession.id"
          class="profession-card"
          :class="{
            'profession-card--learned': profession.learned,
            'profession-card--active':
              selectedProfessionId === profession.id || activeProfession?.id === profession.id,
          }"
          :data-profession="profession.id"
        >
          <div
            class="profession-art"
            :style="{
              backgroundImage: `url(${professionAtlas})`,
              backgroundPosition: `${(profession.art ?? 0) * 25}% center`,
            }"
            aria-hidden="true"
          />
          <div class="profession-card__body">
            <div class="profession-card__heading">
              <div>
                <small>{{ profession.category }}</small
                ><strong>{{ profession.name }}</strong>
              </div>
            </div>
            <p>{{ profession.description }}</p>
            <template v-if="profession.learned">
              <div class="profession-skill">
                <span>Навык {{ profession.learned.skill }} / {{ profession.learned.maxSkill }}</span
                ><i
                  ><b
                    :style="{
                      width: `${Math.min(100, (profession.learned.skill / profession.learned.maxSkill) * 100)}%`,
                    }"
                /></i>
              </div>
              <div class="profession-actions">
                <UIButton
                  variant="secondary"
                  :aria-pressed="activeProfession?.id === profession.id"
                  @click="selectProfession(profession.id)"
                  >{{ activeProfession?.id === profession.id ? 'Открыто' : 'Открыть' }}</UIButton
                ><button
                  type="button"
                  class="forget-link"
                  :disabled="pendingKey !== null"
                  @click="forgetId = profession.id"
                >
                  Забыть
                </button>
              </div>
            </template>
            <UIButton
              v-else
              variant="secondary"
              :disabled="state.learned.length >= 2 || pendingKey !== null"
              :loading="pendingKey === `learn:${profession.id}`"
              @click="learn(profession.id)"
              >Изучить профессию</UIButton
            >
          </div>
        </article>
      </section>
      <div v-if="forgetId" class="profession-message profession-message--error" role="alert">
        <p>
          Забыть {{ professionCatalog.find((item) => item.id === forgetId)?.name }}? Весь навык этой
          профессии будет потерян. Предметы останутся.
        </p>
        <UIButton :disabled="pendingKey !== null" @click="forget(forgetId)"
          >Забыть и сбросить навык</UIButton
        >
        <UIButton variant="secondary" @click="forgetId = null">Отмена</UIButton>
      </div>
      <p v-if="state.learned.length >= 2" class="profession-intro">
        Оба места заняты. Чтобы выбрать другое ремесло, сначала забудьте одну из изученных
        профессий.
      </p>
      <section v-if="activeProfession?.id === 'MINING'" class="profession-panel">
        <header>
          <div>
            <small>ГОРНОЕ ДЕЛО</small>
            <h3>Рудные жилы</h3>
          </div>
        </header>
        <p class="profession-empty">
          Добыча доступна в указанной локации. Между добычами — минута восстановления.
        </p>
        <article
          v-for="node in state.gatheringNodes ?? []"
          :key="node.id"
          class="recipe-row"
          :data-gathering-node="node.id"
        >
          <div>
            <strong>{{ node.name }}</strong
            ><span
              >{{ locationLabel(node.locationId) }} · навык {{ node.requiredSkill }}–{{
                node.skillUpUntil - 1
              }}</span
            ><span>{{ itemLabel(node.itemId) }} ×{{ node.quantity }}</span
            ><span v-if="nodeBlockReason(node)" class="recipe-blocked">{{
              nodeBlockReason(node)
            }}</span>
          </div>
          <UIButton
            :disabled="pendingKey !== null || nodeBlockReason(node) !== null"
            @click="gather(node)"
            >Добыть руду</UIButton
          >
        </article>
      </section>
      <section
        v-if="activeProfession?.id === 'ENCHANTING'"
        class="profession-panel enchanting-panel"
      >
        <header>
          <div>
            <small>СТОЛ ЗАЧАРОВАТЕЛЯ</small>
            <h3>Распыление и чары</h3>
          </div>
        </header>
        <p class="profession-empty">
          Распыление уничтожает выбранную вещь: обычная → странная пыль, необычная → магическая
          субстанция. Чары заменяют предыдущий бонус.
        </p>
        <label class="enchant-target"
          >Снятый предмет<select v-model="selectedItemId" :disabled="pendingKey !== null">
            <option value="">Выберите снаряжение</option>
            <option v-for="item in enchantingItems" :key="item.id" :value="item.id">
              {{ item.name }}
            </option>
          </select></label
        >
        <div class="enchant-actions">
          <UIButton
            variant="secondary"
            :disabled="
              !selectedItem ||
              !['Common', 'Uncommon'].includes(selectedItem.rarity) ||
              pendingKey !== null ||
              currentLocationId !== 'STARTER_TOWN'
            "
            @click="confirmDisenchant = true"
            >Распылить предмет</UIButton
          >
        </div>
        <div
          v-if="confirmDisenchant && selectedItem"
          class="profession-message profession-message--error"
          role="alert"
        >
          <p>Уничтожить «{{ selectedItem.name }}» ради реагента? Вернуть предмет нельзя.</p>
          <UIButton :disabled="pendingKey !== null" @click="disenchant"
            >Подтвердить распыление</UIButton
          ><UIButton variant="secondary" @click="confirmDisenchant = false">Отмена</UIButton>
        </div>
        <article
          v-for="enchantment in state.enchantments ?? []"
          :key="enchantment.id"
          class="recipe-row"
        >
          <div>
            <strong>{{ enchantment.name }}</strong
            ><span
              >+{{ enchantment.value }}
              {{
                {
                  STAMINA: 'выносливость',
                  STRENGTH: 'сила',
                  INTELLECT: 'интеллект',
                  AGILITY: 'ловкость',
                }[enchantment.statId]
              }}
              · грудь, перчатки, обувь</span
            ><span
              >{{ itemLabel(enchantment.scrollItemId) }}:
              {{ ingredientOwned(enchantment.scrollItemId) }} / 1</span
            >
          </div>
          <UIButton
            :disabled="
              !selectedItem ||
              !enchantment.slots.includes(selectedItem.slot ?? '') ||
              ingredientOwned(enchantment.scrollItemId) < 1 ||
              pendingKey !== null ||
              currentLocationId !== 'STARTER_TOWN'
            "
            @click="applyEnchantment(enchantment.id)"
            >Наложить чары</UIButton
          >
        </article>
      </section>

      <details
        v-if="activeProfession?.id === 'SKINNING' && skinning && gatheringRoutes.length"
        class="profession-panel gathering-guide"
      >
        <summary>Где добывать сырьё</summary>
        <div
          v-for="route in gatheringRoutes"
          :key="`${route.itemName}:${route.locationName}`"
          class="gathering-route"
        >
          <strong>{{ route.itemName }}</strong>
          <span
            >{{ route.locationName }} · навык {{ route.requiredSkill
            }}{{ route.skillUpUntil ? `–${route.skillUpUntil - 1}` : '+' }}</span
          >
          <small v-if="route.skillUpUntil && skinning.skill >= route.skillUpUntil">{{
            route.skillUpUntil < maximumGatheringStep
              ? 'Эта ступень пройдена — дальнейшая прокачка в следующей локации.'
              : 'Все доступные ступени пройдены. Продолжение пока не открыто.'
          }}</small>
          <small v-if="skinning.skill < route.requiredSkill"
            >Сначала повысьте навык снятия шкур.</small
          >
        </div>
      </details>

      <section
        v-if="activeProfession && activeProfession.id !== 'SKINNING'"
        class="profession-panel"
      >
        <header>
          <div>
            <small>{{ activeProfession.name }}</small>
            <h3>Рецепты</h3>
          </div>
          <span>{{
            state.recipes.filter((recipe) => recipe.professionId === activeProfession?.id).length
          }}</span>
        </header>
        <p
          class="profession-workshop"
          :class="{ 'profession-workshop--ready': currentLocationId === 'STARTER_TOWN' }"
        >
          {{
            currentLocationId === 'STARTER_TOWN'
              ? 'Мастерская доступна — можно создавать предметы.'
              : 'Для создания предметов нужна городская мастерская.'
          }}
        </p>
        <div class="workshop-filters" aria-label="Тип рецептов">
          <button type="button" :aria-pressed="category === 'all'" @click="category = 'all'">
            Все рецепты
          </button>
          <button
            type="button"
            :aria-pressed="category === 'materials'"
            @click="category = 'materials'"
          >
            Материалы
          </button>
          <button
            type="button"
            :aria-pressed="category === 'equipment'"
            @click="category = 'equipment'"
          >
            Экипировка
          </button>
        </div>
        <article
          v-for="recipe in recipes"
          :key="recipe.id"
          class="recipe-row"
          :data-profession-recipe="recipe.id"
        >
          <div class="recipe-row__main">
            <ItemIcon
              v-if="recipe.outputIconId"
              class="recipe-icon"
              :icon-id="recipe.outputIconId"
              :item-id="recipe.outputItemId"
              :name="recipe.outputName ?? recipe.name"
              :type="materialIds.has(recipe.outputItemId) ? 'Material' : 'Equipment'"
              decorative
            />
            <strong
              >{{ recipe.name }}
              <small v-if="recipe.outputQuantity > 1">×{{ recipe.outputQuantity }}</small></strong
            >
            <span
              >Навык {{ recipe.requiredSkill }} ·
              {{ locationLabel(recipe.requiredLocationId) }}</span
            >
            <span v-if="recipe.outputRequiredLevel && !materialIds.has(recipe.outputItemId)"
              >Экипировка для уровня {{ recipe.outputRequiredLevel }}</span
            >
            <span v-if="recipe.skillUpUntil" class="recipe-growth">{{
              (activeProfession?.skill ?? 0) < recipe.skillUpUntil
                ? `Может повышать навык до ${recipe.skillUpUntil}`
                : 'Больше не повышает навык'
            }}</span>
            <ul>
              <li
                v-for="ingredient in recipe.ingredients"
                :key="ingredient.itemId"
                :class="{
                  'recipe-missing': ingredientOwned(ingredient.itemId) < ingredient.quantity,
                }"
              >
                {{ itemLabel(ingredient.itemId) }}: {{ ingredientOwned(ingredient.itemId) }} /
                {{ ingredient.quantity }}
                <small v-if="materialGuide(ingredient.itemId)" class="recipe-source">{{
                  materialGuide(ingredient.itemId)
                }}</small>
              </li>
            </ul>
            <span v-if="blockReason(recipe)" class="recipe-blocked">{{ blockReason(recipe) }}</span>
          </div>
          <UIButton
            :disabled="!canCraft(recipe) || pendingKey !== null"
            :loading="pendingKey === `craft:${recipe.id}`"
            @click="craft(recipe)"
            >Создать</UIButton
          >
        </article>
        <p v-if="recipes.length === 0" class="profession-empty">
          В этой категории пока нет рецептов.
        </p>
        <p class="profession-empty">
          Для изготовления учитываются только незапертые материалы в инвентаре. Новые рецепты
          открываются навыком автоматически.
        </p>
      </section>

      <p v-if="state.learned.length === 0" class="profession-empty profession-empty--large">
        Выберите первую профессию. Одновременно доступны не более двух профессий.
      </p>
    </template>

    <p v-else class="profession-message profession-message--error">
      {{ error ?? 'Профессии недоступны.' }}
    </p>
  </section>
</template>

<style scoped>
.workshop-filters {
  display: flex;
  gap: 6px;
  padding: 10px;
  flex-wrap: wrap;
  border-bottom: 1px solid var(--ui-color-border);
}
.workshop-filters button {
  min-height: 44px;
  padding: 6px 12px;
  border: 1px solid var(--ui-color-border);
  color: var(--ui-color-text-muted);
  background: #12131a;
  font: inherit;
  cursor: pointer;
}
.workshop-filters button[aria-pressed='true'] {
  border-color: var(--ui-color-gold);
  color: var(--ui-color-gold);
  background: #292015;
}
.workshop-filters button:focus-visible {
  outline: 2px solid var(--ui-color-gold);
  outline-offset: 2px;
}
.recipe-source {
  display: block;
  margin-top: 3px;
  color: var(--ui-color-text-muted);
  line-height: 1.5;
}
.recipe-row .recipe-growth {
  color: #a9d395;
}
.recipe-row .recipe-blocked {
  color: #d8a96d;
}
.recipe-icon {
  width: 42px;
  height: 42px;
}
.gathering-guide summary {
  padding: 12px;
  min-height: 44px;
  box-sizing: border-box;
  color: var(--ui-color-gold);
  cursor: pointer;
}
.gathering-route {
  display: grid;
  gap: 4px;
  padding: 10px 12px;
  border-top: 1px solid var(--ui-color-border);
  font-size: 0.75rem;
}
.gathering-route span,
.gathering-route small {
  color: var(--ui-color-text-muted);
}
.professions-hero > span {
  white-space: nowrap;
  font-size: 0.9rem;
  align-self: start;
}
.profession-card--learned > p {
  display: none;
}
.professions-view {
  display: grid;
  gap: 12px;
}
.professions-hero {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 14px;
  padding: 14px;
  border: 1px solid rgb(205 177 113 / 32%);
  background:
    linear-gradient(115deg, rgb(205 177 113 / 13%), transparent 58%), var(--ui-gradient-panel);
}
.professions-hero small,
.profession-panel header small,
.profession-card__heading small {
  color: var(--ui-color-gold);
  font-size: 0.54rem;
  font-weight: 800;
  letter-spacing: 0.12em;
}
.professions-hero h2,
.profession-panel h3 {
  margin: 2px 0 0;
  font-family: var(--ui-font-display);
}
.professions-hero h2 {
  font-size: 1.05rem;
}
.professions-hero p {
  max-width: 28rem;
  margin: 5px 0 0;
  color: var(--ui-color-text-muted);
  font-size: 0.68rem;
  line-height: 1.4;
}
.professions-hero > span {
  min-width: 3.25rem;
  padding: 6px 8px;
  border: 1px solid rgb(205 177 113 / 35%);
  color: var(--ui-color-gold);
  font-weight: 800;
  text-align: center;
}
.profession-slots {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}
.profession-card {
  display: grid;
  align-content: start;
  gap: 9px;
  min-height: 160px;
  padding: 11px;
  border: 1px solid var(--ui-color-border);
  background: rgb(8 11 17 / 74%);
}
.profession-card--learned {
  min-height: 100px;
  border-color: rgb(205 177 113 / 44%);
  background: linear-gradient(145deg, rgb(205 177 113 / 8%), transparent 65%), rgb(8 11 17 / 78%);
}
.profession-card__heading {
  display: flex;
  align-items: center;
  gap: 8px;
}
.profession-card__heading > div {
  display: grid;
  gap: 1px;
}
.profession-card__heading strong {
  font-family: var(--ui-font-display);
  font-size: 0.87rem;
}
.profession-emblem {
  display: grid;
  width: 2rem;
  height: 2rem;
  place-items: center;
  border: 1px solid rgb(205 177 113 / 38%);
  border-radius: 50%;
  color: var(--ui-color-gold);
}
.profession-card > p {
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: 0.64rem;
  line-height: 1.4;
}
.profession-card :deep(.ui-button) {
  margin-top: auto;
  min-height: var(--ui-touch-target);
  font-size: var(--ui-font-size-xs);
}
.profession-skill {
  display: grid;
  gap: 4px;
  margin-top: auto;
}
.profession-skill > span {
  font-size: 0.62rem;
  font-weight: 700;
}
.profession-skill > i {
  display: block;
  height: 5px;
  overflow: hidden;
  border: 1px solid rgb(205 177 113 / 24%);
  background: #080b11;
}
.profession-skill b {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, #7b5e2e, #d6b76f);
}
.profession-panel {
  display: grid;
  border: 1px solid var(--ui-color-border);
  background: rgb(8 11 17 / 62%);
}
.profession-panel > header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 11px;
  border-bottom: 1px solid var(--ui-color-border);
}
.profession-panel h3 {
  font-size: 0.95rem;
}
.profession-panel > header > span {
  color: var(--ui-color-text-muted);
  font-size: 0.65rem;
}
.corpse-row,
.recipe-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  padding: 10px 11px;
  border-bottom: 1px solid rgb(255 255 255 / 6%);
}
.corpse-row:last-child,
.recipe-row:last-child {
  border-bottom: 0;
}
.corpse-row > div,
.recipe-row__main {
  display: grid;
  min-width: 0;
  gap: 3px;
}
.corpse-row strong,
.recipe-row strong {
  font-size: 0.76rem;
}
.corpse-row small,
.recipe-row span {
  color: var(--ui-color-text-muted);
  font-size: 0.6rem;
}
.corpse-row :deep(.ui-button),
.recipe-row :deep(.ui-button) {
  min-height: var(--ui-touch-target);
  font-size: var(--ui-font-size-xs);
}
.recipe-row ul {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 8px;
  margin: 2px 0 0;
  padding: 0;
  list-style: none;
}
.recipe-row li {
  color: #c7d3bd;
  font-size: 0.58rem;
}
.recipe-row li.recipe-missing {
  color: var(--ui-color-danger);
}
.profession-workshop {
  margin: 0;
  padding: 8px 11px;
  border-bottom: 1px solid var(--ui-color-border);
  color: #d8a96d;
  background: rgb(174 103 42 / 8%);
  font-size: 0.61rem;
}
.profession-workshop--ready {
  color: #a9d395;
  background: rgb(89 133 73 / 8%);
}
.profession-empty {
  margin: 0;
  padding: 13px;
  color: var(--ui-color-text-muted);
  font-size: 0.65rem;
  line-height: 1.45;
}
.profession-empty--large {
  text-align: center;
  border: 1px dashed var(--ui-color-border);
}
.profession-message {
  margin: 0;
  padding: 9px 11px;
  border: 1px solid;
  font-size: 0.65rem;
}
.profession-message--success {
  border-color: rgb(103 157 87 / 45%);
  background: rgb(72 113 60 / 10%);
  color: #b9dda9;
}
.profession-message--error {
  border-color: rgb(184 85 77 / 45%);
  background: rgb(138 54 48 / 10%);
  color: #efaaa4;
}
@media (max-width: 520px) {
  .profession-slots {
    grid-template-columns: 1fr;
  }
  .professions-hero {
    padding: 12px;
  }
  .recipe-row,
  .corpse-row {
    grid-template-columns: minmax(0, 1fr);
    align-items: stretch;
  }
  .recipe-row :deep(.ui-button),
  .corpse-row :deep(.ui-button) {
    width: 100%;
  }
}
@media (max-width: 520px) {
  .professions-view .profession-slots {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .profession-card {
    padding: 9px;
  }
  .profession-card__heading {
    display: block;
  }
  .profession-card__heading strong {
    font-size: 0.72rem;
    overflow-wrap: anywhere;
  }
  .profession-emblem {
    display: none;
  }
}
</style>

<style scoped>
.professions-hero {
  background:
    linear-gradient(100deg, #15120ff2, #15120fa8),
    url('@/assets/professions/workshop-atlas.png') center 57% / cover;
  min-height: 105px;
  border-color: #796244;
}
.professions-hero h2 {
  font-family: var(--ui-font-display);
  font-size: 1.55rem;
  color: #e9cf98;
}
.profession-intro {
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: 0.72rem;
  line-height: 1.6;
}
.professions-view .profession-slots {
  display: grid;
  grid-template-columns: 1fr;
  gap: 8px;
}
.profession-card {
  display: grid;
  grid-template-columns: 108px minmax(0, 1fr);
  gap: 0;
  padding: 0;
  overflow: hidden;
  min-height: 150px;
  border: 1px solid #514735;
  border-radius: 2px;
  background: linear-gradient(100deg, #211c15, #111316);
  box-shadow: inset 0 0 0 1px #ffffff05;
}
.profession-card--active {
  border-color: #b79558;
  box-shadow: inset 0 0 0 1px #d6b76f26;
}
.profession-art {
  background-size: 500% 100%;
  background-repeat: no-repeat;
  border-right: 1px solid #514735;
}
.profession-card__body {
  display: grid;
  gap: 7px;
  padding: 11px 13px;
  min-width: 0;
}
.profession-card__body > p {
  margin: 0;
  color: #ada99d;
  font-size: 0.7rem;
  line-height: 1.45;
}
.profession-card__heading strong {
  color: #e5cf9f;
  font-size: 1rem;
}
.profession-card__heading small {
  color: #9d917b;
  font-size: 0.57rem;
  letter-spacing: 0.09em;
}
.profession-skill > span {
  font-size: 0.7rem;
  color: #b4c79c;
}
.profession-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}
.forget-link {
  margin-left: auto;
  border: 0;
  background: transparent;
  color: #a99d8e;
  text-decoration: underline;
  min-height: 44px;
  padding: 0 8px;
  cursor: pointer;
}
.enchant-target {
  padding: 10px 12px;
  display: grid;
  gap: 6px;
  font-size: 0.75rem;
}
.enchant-target select {
  min-height: 44px;
  width: 100%;
  color: #dfd4bf;
  background: #121317;
  border: 1px solid #635640;
  padding: 8px;
}
.enchant-actions {
  padding: 0 12px 12px;
}
.profession-message > p {
  margin-top: 0;
}
.profession-message :deep(.ui-button) {
  margin: 4px 6px 0 0;
}
@media (min-width: 700px) {
  .professions-view .profession-slots {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@media (max-width: 520px) {
  .profession-card {
    padding: 0;
    grid-template-columns: 100px minmax(0, 1fr);
  }
  .profession-card__heading strong {
    font-size: 0.9rem;
  }
  .profession-card__heading {
    display: flex;
  }
}
</style>
