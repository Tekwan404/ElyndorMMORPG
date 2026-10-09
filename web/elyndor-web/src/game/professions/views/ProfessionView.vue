<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { ApiRequestError } from '@/api/apiClient'
import { locationPresentation } from '@/game/world/locationPresentation'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UILoadingState } from '@/ui/components'
import ItemIcon from '@/game/items/components/ItemIcon.vue'
import { recipeBlockReason, usableMaterialQuantities } from '../professionWorkshop'
import {
  craftProfessionRecipe,
  getProfessionState,
  learnProfession,
  type ProfessionMutationResult,
  type ProfessionRecipeState,
  type ProfessionStateSnapshot,
} from '@/game/professions/professionApi'

const session = useGameSessionStore()
const state = ref<ProfessionStateSnapshot | null>(null)
const loading = ref(true)
const pendingKey = ref<string | null>(null)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const category = ref<'all' | 'materials' | 'equipment'>('all')

const skinning = computed(() => state.value?.learned.find((item) => item.id === 'SKINNING') ?? null)
const leatherworking = computed(
  () => state.value?.learned.find((item) => item.id === 'LEATHERWORKING') ?? null,
)
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
      category.value === 'all' ||
      (category.value === 'materials') === materialIds.value.has(recipe.outputItemId),
  ),
)
const gatheringRoutes = computed(() => {
  const routes = new Map<
    string,
    { itemName: string; locationName: string; requiredSkill: number; skillUpUntil?: number }
  >()
  for (const source of state.value?.materialSources ?? []) {
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

async function learn(id: 'SKINNING' | 'LEATHERWORKING'): Promise<void> {
  await mutate(
    `learn:${id}`,
    () => learnProfession(id),
    () => {
      const name = id === 'SKINNING' ? 'Снятие шкур' : 'Кожевничество'
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

onMounted(load)
</script>

<template>
  <section class="professions-view" data-profession-workshop>
    <header class="professions-hero">
      <div>
        <small>РЕМЁСЛА ЭЛИНДОРА</small>
        <h2>Кожевенная мастерская</h2>
        <p>Добудьте сырьё с туши, выделайте кожу и создайте экипировку.</p>
      </div>
      <span>{{ state?.learned.length ?? 0 }} / 2</span>
    </header>

    <UILoadingState v-if="loading && !state" state="loading" title="Загружаем профессии" />

    <template v-else-if="state">
      <p v-if="notice" class="profession-message profession-message--success">{{ notice }}</p>
      <p v-if="error" class="profession-message profession-message--error">{{ error }}</p>

      <section class="profession-slots" aria-label="Изученные профессии">
        <article class="profession-card" :class="{ 'profession-card--learned': skinning }">
          <div class="profession-card__heading">
            <span class="profession-emblem" aria-hidden="true">✦</span>
            <div><small>СБОР</small><strong>Снятие шкур</strong></div>
          </div>
          <p>После победы над подходящим зверем с его туши можно снять шкуру.</p>
          <template v-if="skinning">
            <div class="profession-skill">
              <span>Навык {{ skinning.skill }} / {{ skinning.maxSkill }}</span>
              <i
                ><b
                  :style="{
                    width: `${Math.min(100, (skinning.skill / skinning.maxSkill) * 100)}%`,
                  }"
              /></i>
            </div>
          </template>
          <UIButton
            v-else
            variant="secondary"
            :disabled="state.learned.length >= 2 || pendingKey !== null"
            :loading="pendingKey === 'learn:SKINNING'"
            @click="learn('SKINNING')"
            >Изучить профессию</UIButton
          >
        </article>

        <article class="profession-card" :class="{ 'profession-card--learned': leatherworking }">
          <div class="profession-card__heading">
            <span class="profession-emblem" aria-hidden="true">◆</span>
            <div><small>РЕМЕСЛО</small><strong>Кожевничество</strong></div>
          </div>
          <p>Создавайте кожаное снаряжение из шкур и хитина в городской мастерской.</p>
          <template v-if="leatherworking">
            <div class="profession-skill">
              <span>Навык {{ leatherworking.skill }} / {{ leatherworking.maxSkill }}</span>
              <i
                ><b
                  :style="{
                    width: `${Math.min(100, (leatherworking.skill / leatherworking.maxSkill) * 100)}%`,
                  }"
              /></i>
            </div>
          </template>
          <UIButton
            v-else
            variant="secondary"
            :disabled="state.learned.length >= 2 || pendingKey !== null"
            :loading="pendingKey === 'learn:LEATHERWORKING'"
            @click="learn('LEATHERWORKING')"
            >Изучить профессию</UIButton
          >
        </article>
      </section>

      <details v-if="skinning && gatheringRoutes.length" class="profession-panel gathering-guide">
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

      <section v-if="leatherworking" class="profession-panel">
        <header>
          <div>
            <small>КОЖЕВНИЧЕСТВО</small>
            <h3>Рецепты</h3>
          </div>
          <span>{{ state.recipes.length }}</span>
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
            Выделка
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
              (leatherworking?.skill ?? 0) < recipe.skillUpUntil
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
