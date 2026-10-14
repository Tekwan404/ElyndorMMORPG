<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { resolveCharacterArt } from '@/assets/characterArt'
import ProfileFrame from '@/game/character/components/ProfileFrame.vue'
import CharacterRenameForm from '@/game/economy/components/CharacterRenameForm.vue'
import { personalizationError, type PersonalizationStore, type ProfileCosmetic } from '@/game/economy/personalizationContracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIModal } from '@/ui/components'

const props = defineProps<{ mode: 'cosmetics' | 'services' }>()
const emit = defineEmits<{ balance: [value: number] }>()
const session = useGameSessionStore()
const catalog = ref<PersonalizationStore | null>(null)
const error = ref<string | null>(null)
const loading = ref(true)
const selected = ref<ProfileCosmetic | null>(null)
const notice = ref<string | null>(null)
const pending = computed(() => session.isMutationDomainPending('personalization:'))
const character = computed(() => session.snapshot?.character)
const art = computed(() => character.value
  ? resolveCharacterArt(character.value.classId, character.value.genderId, 'transparent', character.value.activeSkinId) : null)
const groups = computed(() => (['Frame', 'Title'] as const).map(kind => ({
  kind, name: kind === 'Frame' ? 'Рамки профиля' : 'Титулы',
  items: catalog.value?.cosmetics.filter(item => item.kind === kind) ?? [],
})))
const balance = computed(() => session.crystalBalance ?? catalog.value?.crystalBalance ?? 0)
const active = (item: ProfileCosmetic) => item.id === (item.kind === 'Frame' ? catalog.value?.activeFrameId : catalog.value?.activeTitleId)
async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try { catalog.value = await session.getPersonalizationStore(); emit('balance', catalog.value.crystalBalance) }
  catch { error.value = 'Не удалось загрузить коллекцию. Повторите попытку.' }
  finally { loading.value = false }
}
async function purchase(): Promise<void> {
  if (!selected.value || pending.value) return
  const item = selected.value
  const result = await session.purchaseCosmetic(item.id)
  if (!result) { error.value = personalizationError[session.errorCode ?? ''] ?? 'Покупка не выполнена.'; return }
  selected.value = null
  notice.value = item.name + ' добавлено в коллекцию. Теперь его можно применить.'
  await load()
}
async function equip(item: ProfileCosmetic | null, kind: 'Frame' | 'Title'): Promise<void> {
  if (pending.value) return
  error.value = null
  const result = await session.equipCosmetic(kind, item?.id ?? null)
  if (!result) { error.value = personalizationError[session.errorCode ?? ''] ?? 'Не удалось применить украшение.'; return }
  notice.value = item ? item.name + ' применяется к профилю.' : 'Украшение снято.'
  await load()
}
onMounted(load)
</script>

<template>
  <section class="personalization" :aria-label="mode === 'services' ? 'Услуги персонажа' : 'Коллекция украшений'">
    <p v-if="loading" role="status">Открываем коллекцию…</p>
    <p v-if="error" class="personalization__error" role="alert">{{ error }} <UIButton variant="ghost" @click="load">Обновить</UIButton></p>
    <p v-if="notice" role="status">{{ notice }}</p>
    <template v-if="catalog">
      <CharacterRenameForm v-if="props.mode === 'services' && catalog.renamePrice !== null"
        :price="catalog.renamePrice" :balance="balance" @changed="load" />
      <p v-else-if="props.mode === 'services'">Смена имени временно недоступна.</p>
      <template v-else>
        <p class="personalization__lead">Покупка навсегда. Примеряйте перед покупкой, меняйте и снимайте бесплатно. Характеристики героя остаются прежними.</p>
        <section v-for="group in groups" :key="group.kind" class="cosmetic-group">
          <header><h2>{{ group.name }}</h2><UIButton v-if="group.kind === 'Frame' ? catalog.activeFrameId : catalog.activeTitleId"
            variant="ghost" :disabled="pending" @click="equip(null, group.kind)">Снять</UIButton></header>
          <div class="cosmetic-grid">
            <article v-for="item in group.items" :key="item.id" class="cosmetic-card" :data-cosmetic-id="item.id">
              <div class="cosmetic-card__stage">
                <ProfileFrame :style-id="item.kind === 'Frame' ? item.styleId : character?.profileFrameStyle" class="cosmetic-card__portrait">
                  <img v-if="art" :src="art" alt="" /><span v-else>{{ character?.name[0] }}</span>
                </ProfileFrame>
                <strong>{{ character?.name }}</strong>
                <span v-if="item.kind === 'Title'" class="cosmetic-card__title">{{ item.name }}</span>
              </div>
              <h3>{{ item.name }}</h3><p>{{ item.description }}</p>
              <small v-if="active(item)">Используется</small><small v-else-if="item.owned">В коллекции</small>
              <UIButton v-if="item.owned" :disabled="pending || active(item)" :data-equip-cosmetic="item.id" @click="equip(item, item.kind)">
                {{ active(item) ? 'Используется' : 'Применить бесплатно' }}</UIButton>
              <UIButton v-else :disabled="pending || !item.enabled || balance < item.crystalPrice" :data-buy-cosmetic="item.id" @click="error = null; selected = item">
                {{ balance < item.crystalPrice ? 'Не хватает кристаллов' : 'Купить · ' + item.crystalPrice }}</UIButton>
            </article>
          </div>
        </section>
      </template>
    </template>
    <UIModal :open="Boolean(selected)" :title="selected?.name ?? 'Покупка'" :busy="pending" @close="selected = null">
      <p>Украшение останется в коллекции навсегда. Вы сможете применять и снимать его бесплатно.</p>
      <p>Стоимость: {{ selected?.crystalPrice }} кристаллов. Остаток: {{ balance - (selected?.crystalPrice ?? 0) }}.</p>
      <p v-if="error" role="alert">{{ error }}</p>
      <template #actions><UIButton variant="ghost" :disabled="pending" @click="selected = null">Отмена</UIButton>
        <UIButton data-confirm-cosmetic :loading="pending" :disabled="pending || balance < (selected?.crystalPrice ?? 0)" @click="purchase">Подтвердить покупку</UIButton></template>
    </UIModal>
  </section>
</template>

<style scoped>
.personalization { display: grid; gap: 16px; min-width: 0; padding-block: 10px; color: #c5baa7; }
.personalization__lead { margin: 0; font-size: 14px; line-height: 1.6; }
.personalization__error { color: #efad9d; }
.cosmetic-group { display: grid; gap: 12px; }
.cosmetic-group header { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.cosmetic-group h2 { margin: 0; color: #ebd5aa; font: 700 25px Georgia, serif; }
.cosmetic-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.cosmetic-card { display: grid; gap: 10px; min-width: 0; padding: 12px; border: 1px solid #68533b; border-radius: 8px; background: #101722; }
.cosmetic-card h3 { margin: 0; color: #f0ddbd; font: 700 18px Georgia, serif; }
.cosmetic-card p { margin: 0; font-size: 12px; line-height: 1.5; }
.cosmetic-card small { color: #a8c799; }
.cosmetic-card__stage { display: grid; justify-items: center; gap: 10px; padding: 12px 0; background: radial-gradient(ellipse at center, #312b3a, #0d1420 70%); text-align: center; }
.cosmetic-card__portrait { width: 84px; height: 100px; border-radius: 8px; }
.cosmetic-card__portrait img { width: 100%; height: 100%; object-fit: contain; }
.cosmetic-card__title { color: #d9b9f5; font-size: 12px; }
.cosmetic-card :deep(button) { align-self: end; min-height: 48px; font-size: 12px; }
@media (max-width: 360px) { .cosmetic-grid { grid-template-columns: 1fr; } .cosmetic-card__portrait { width: 96px; height: 112px; } }
@media (min-width: 700px) { .cosmetic-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
</style>
