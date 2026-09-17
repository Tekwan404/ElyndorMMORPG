<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { ApiRequestError } from '@/api/apiClient'
import { locationPresentation } from '@/game/world/locationPresentation'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UILoadingState } from '@/ui/components'
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

const skinning = computed(() => state.value?.learned.find(item => item.id === 'SKINNING') ?? null)
const leatherworking = computed(() => state.value?.learned.find(item => item.id === 'LEATHERWORKING') ?? null)
const currentLocationId = computed(() => session.snapshot?.world?.currentLocation.id ?? null)
const inventoryQuantities = computed(() => {
  const quantities = new Map<string, number>()
  for (const item of session.snapshot?.character?.inventory.items ?? []) {
    quantities.set(item.definitionId, (quantities.get(item.definitionId) ?? 0) + item.quantity)
  }
  return quantities
})

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
  await mutate(`learn:${id}`, () => learnProfession(id), () => {
    const name = id === 'SKINNING' ? 'Снятие шкур' : 'Кожевничество'
    return `${name} изучено.`
  })
}

async function craft(recipe: ProfessionRecipeState): Promise<void> {
  await mutate(
    `craft:${recipe.id}`,
    () => craftProfessionRecipe(recipe.id),
    result => `Создано: ${recipe.name} ×${result.quantity}${result.skillIncreased ? ' · навык +1' : ''}`,
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
    await load()
    if (refreshCharacter) await session.refreshSnapshot()
    notice.value = successMessage(result)
  } catch (caught) {
    error.value = errorMessage(caught)
    await load().catch(() => undefined)
  } finally {
    pendingKey.value = null
  }
}

function canCraft(recipe: ProfessionRecipeState): boolean {
  const profession = state.value?.learned.find(item => item.id === recipe.professionId)
  if (!profession || profession.skill < recipe.requiredSkill) return false
  if (recipe.requiredLocationId && recipe.requiredLocationId !== currentLocationId.value) return false
  return recipe.ingredients.every(ingredient =>
    (inventoryQuantities.value.get(ingredient.itemId) ?? 0) >= ingredient.quantity)
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
  return ({
    ROUGH_LEATHER: 'Грубая кожа',
    LIGHT_LEATHER: 'Лёгкая кожа',
    CHITIN_FRAGMENT: 'Фрагмент хитина',
  } as Record<string, string>)[id] ?? 'Материал'
}

function errorMessage(caught: unknown): string {
  const code = caught instanceof ApiRequestError ? caught.code : 'network_unavailable'
  return ({
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
    profession_idempotency_conflict: 'Состояние изменилось. Обновите экран и повторите действие.',
    network_unavailable: 'Не удалось связаться с сервером.',
  } as Record<string, string>)[code] ?? 'Не удалось выполнить действие.'
}

onMounted(load)
</script>

<template>
  <section class="professions-view">
    <header class="professions-hero">
      <div>
        <small>РЕМЁСЛА ЭЛИНДОРА</small>
        <h2>Профессии</h2>
        <p>Добывайте материалы и создавайте полезное снаряжение.</p>
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
              <i><b :style="{ width: `${Math.min(100, skinning.skill / skinning.maxSkill * 100)}%` }" /></i>
            </div>
          </template>
          <UIButton
            v-else
            variant="secondary"
            :disabled="state.learned.length >= 2 || pendingKey !== null"
            :loading="pendingKey === 'learn:SKINNING'"
            @click="learn('SKINNING')"
          >Изучить профессию</UIButton>
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
              <i><b :style="{ width: `${Math.min(100, leatherworking.skill / leatherworking.maxSkill * 100)}%` }" /></i>
            </div>
          </template>
          <UIButton
            v-else
            variant="secondary"
            :disabled="state.learned.length >= 2 || pendingKey !== null"
            :loading="pendingKey === 'learn:LEATHERWORKING'"
            @click="learn('LEATHERWORKING')"
          >Изучить профессию</UIButton>
        </article>
      </section>

      <section v-if="leatherworking" class="profession-panel">
        <header>
          <div><small>КОЖЕВНИЧЕСТВО</small><h3>Рецепты</h3></div>
          <span>{{ state.recipes.length }}</span>
        </header>
        <p class="profession-workshop" :class="{ 'profession-workshop--ready': currentLocationId === 'STARTER_TOWN' }">
          {{ currentLocationId === 'STARTER_TOWN' ? 'Мастерская доступна — можно создавать предметы.' : 'Для создания предметов нужна городская мастерская.' }}
        </p>
        <article v-for="recipe in state.recipes" :key="recipe.id" class="recipe-row">
          <div class="recipe-row__main">
            <strong>{{ recipe.name }} <small v-if="recipe.outputQuantity > 1">×{{ recipe.outputQuantity }}</small></strong>
            <span>Навык {{ recipe.requiredSkill }} · {{ locationLabel(recipe.requiredLocationId) }}</span>
            <ul>
              <li v-for="ingredient in recipe.ingredients" :key="ingredient.itemId" :class="{ 'recipe-missing': ingredientOwned(ingredient.itemId) < ingredient.quantity }">
                {{ itemLabel(ingredient.itemId) }}: {{ ingredientOwned(ingredient.itemId) }} / {{ ingredient.quantity }}
              </li>
            </ul>
          </div>
          <UIButton
            :disabled="!canCraft(recipe) || pendingKey !== null"
            :loading="pendingKey === `craft:${recipe.id}`"
            @click="craft(recipe)"
          >Создать</UIButton>
        </article>
      </section>

      <p v-if="state.learned.length === 0" class="profession-empty profession-empty--large">
        Выберите первую профессию. Одновременно доступны не более двух профессий.
      </p>
    </template>

    <p v-else class="profession-message profession-message--error">{{ error ?? 'Профессии недоступны.' }}</p>
  </section>
</template>

<style scoped>
.professions-view{display:grid;gap:12px}.professions-hero{display:flex;align-items:start;justify-content:space-between;gap:14px;padding:14px;border:1px solid rgb(205 177 113 / 32%);background:linear-gradient(115deg,rgb(205 177 113 / 13%),transparent 58%),var(--ui-gradient-panel)}.professions-hero small,.profession-panel header small,.profession-card__heading small{color:var(--ui-color-gold);font-size:.54rem;font-weight:800;letter-spacing:.12em}.professions-hero h2,.profession-panel h3{margin:2px 0 0;font-family:var(--ui-font-display)}.professions-hero h2{font-size:1.35rem}.professions-hero p{max-width:28rem;margin:5px 0 0;color:var(--ui-color-text-muted);font-size:.68rem;line-height:1.4}.professions-hero>span{min-width:3.25rem;padding:6px 8px;border:1px solid rgb(205 177 113 / 35%);color:var(--ui-color-gold);font-weight:800;text-align:center}.profession-slots{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px}.profession-card{display:grid;align-content:start;gap:9px;min-height:160px;padding:11px;border:1px solid var(--ui-color-border);background:rgb(8 11 17 / 74%)}.profession-card--learned{border-color:rgb(205 177 113 / 44%);background:linear-gradient(145deg,rgb(205 177 113 / 8%),transparent 65%),rgb(8 11 17 / 78%)}.profession-card__heading{display:flex;align-items:center;gap:8px}.profession-card__heading>div{display:grid;gap:1px}.profession-card__heading strong{font-family:var(--ui-font-display);font-size:.87rem}.profession-emblem{display:grid;width:2rem;height:2rem;place-items:center;border:1px solid rgb(205 177 113 / 38%);border-radius:50%;color:var(--ui-color-gold)}.profession-card>p{margin:0;color:var(--ui-color-text-muted);font-size:.64rem;line-height:1.4}.profession-card :deep(.ui-button){margin-top:auto;min-height:var(--ui-touch-target);font-size:var(--ui-font-size-xs)}.profession-skill{display:grid;gap:4px;margin-top:auto}.profession-skill>span{font-size:.62rem;font-weight:700}.profession-skill>i{display:block;height:5px;overflow:hidden;border:1px solid rgb(205 177 113 / 24%);background:#080b11}.profession-skill b{display:block;height:100%;background:linear-gradient(90deg,#7b5e2e,#d6b76f)}.profession-panel{display:grid;border:1px solid var(--ui-color-border);background:rgb(8 11 17 / 62%)}.profession-panel>header{display:flex;align-items:center;justify-content:space-between;padding:10px 11px;border-bottom:1px solid var(--ui-color-border)}.profession-panel h3{font-size:.95rem}.profession-panel>header>span{color:var(--ui-color-text-muted);font-size:.65rem}.corpse-row,.recipe-row{display:grid;grid-template-columns:minmax(0,1fr) auto;align-items:center;gap:10px;padding:10px 11px;border-bottom:1px solid rgb(255 255 255 / 6%)}.corpse-row:last-child,.recipe-row:last-child{border-bottom:0}.corpse-row>div,.recipe-row__main{display:grid;min-width:0;gap:3px}.corpse-row strong,.recipe-row strong{font-size:.76rem}.corpse-row small,.recipe-row span{color:var(--ui-color-text-muted);font-size:.6rem}.corpse-row :deep(.ui-button),.recipe-row :deep(.ui-button){min-height:var(--ui-touch-target);font-size:var(--ui-font-size-xs)}.recipe-row ul{display:flex;flex-wrap:wrap;gap:4px 8px;margin:2px 0 0;padding:0;list-style:none}.recipe-row li{color:#c7d3bd;font-size:.58rem}.recipe-row li.recipe-missing{color:var(--ui-color-danger)}.profession-workshop{margin:0;padding:8px 11px;border-bottom:1px solid var(--ui-color-border);color:#d8a96d;background:rgb(174 103 42 / 8%);font-size:.61rem}.profession-workshop--ready{color:#a9d395;background:rgb(89 133 73 / 8%)}.profession-empty{margin:0;padding:13px;color:var(--ui-color-text-muted);font-size:.65rem;line-height:1.45}.profession-empty--large{text-align:center;border:1px dashed var(--ui-color-border)}.profession-message{margin:0;padding:9px 11px;border:1px solid;font-size:.65rem}.profession-message--success{border-color:rgb(103 157 87 / 45%);background:rgb(72 113 60 / 10%);color:#b9dda9}.profession-message--error{border-color:rgb(184 85 77 / 45%);background:rgb(138 54 48 / 10%);color:#efaaa4}@media (max-width:520px){.profession-slots{grid-template-columns:1fr}.professions-hero{padding:12px}.recipe-row,.corpse-row{grid-template-columns:minmax(0,1fr);align-items:stretch}.recipe-row :deep(.ui-button),.corpse-row :deep(.ui-button){width:100%}}
</style>