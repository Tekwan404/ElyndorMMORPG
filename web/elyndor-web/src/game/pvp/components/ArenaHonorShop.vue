<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { ArenaHonorShop, ArenaHonorShopItem } from '@/game/pvp/arenaContracts'
import ItemIcon from '@/game/items/components/ItemIcon.vue'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton } from '@/ui/components'

const props = defineProps<{
  shop: ArenaHonorShop | null
  loading: boolean
  pendingItemId: string | null
  errorMessage: string | null
}>()

const emit = defineEmits<{
  refresh: []
  buy: [itemId: string]
}>()

const session = useGameSessionStore()
const selectedSetId = ref<string | null>(null)
const characterLevel = computed(() => session.snapshot?.character?.level ?? 1)

const groups = computed(() => {
  const grouped = new Map<string, ArenaHonorShopItem[]>()
  for (const item of props.shop?.items ?? []) {
    const key = item.setId ?? 'OTHER'
    const current = grouped.get(key) ?? []
    current.push(item)
    grouped.set(key, current)
  }
  return [...grouped.entries()].map(([id, items]) => ({
    id,
    name: setName(items[0]),
    items: [...items].sort((a, b) => slotRank(a.slot) - slotRank(b.slot)),
  }))
})

const selectedGroup = computed(() =>
  groups.value.find(group => group.id === selectedSetId.value) ?? groups.value[0] ?? null,
)

watch(groups, next => {
  if (!next.some(group => group.id === selectedSetId.value)) {
    selectedSetId.value = next[0]?.id ?? null
  }
}, { immediate: true })

function setName(item: ArenaHonorShopItem | undefined): string {
  if (!item) return 'Комплект'
  return item.name.match(/«([^»]+)»/)?.[1] ?? item.name
}

function slotRank(slot: string | null): number {
  return ['Head', 'Shoulders', 'Chest', 'Hands', 'Legs', 'Feet'].indexOf(slot ?? '')
}

function slotLabel(slot: string | null): string {
  if (slot === 'Head') return 'Шлем'
  if (slot === 'Shoulders') return 'Наплечники'
  if (slot === 'Chest') return 'Нагрудник'
  if (slot === 'Hands') return 'Рукавицы'
  if (slot === 'Legs') return 'Поножи'
  if (slot === 'Feet') return 'Сапоги'
  return 'Предмет'
}

function unavailableReason(item: ArenaHonorShopItem): string | null {
  if (characterLevel.value < item.requiredLevel) return `Нужен ур. ${item.requiredLevel}`
  if ((props.shop?.honor ?? 0) < item.honorPrice) return 'Не хватает чести'
  return null
}
</script>

<template>
  <section class="honor-shop" aria-label="Магазин чести">
    <header class="honor-shop__head">
      <div>
        <small>PvP T1 · ур. 60</small>
        <h3>Магазин чести</h3>
      </div>
      <div class="honor-shop__balance">
        <span>Честь</span>
        <strong>{{ shop?.honor ?? 0 }}</strong>
      </div>
    </header>

    <p v-if="errorMessage" class="honor-shop__error" role="alert">{{ errorMessage }}</p>

    <div v-if="loading && !shop" class="honor-shop__empty">Загружаем награды арены…</div>

    <template v-else-if="groups.length > 0">
      <nav class="honor-shop__sets" aria-label="PvP комплекты">
        <button
          v-for="group in groups"
          :key="group.id"
          type="button"
          :class="{ 'is-active': group.id === selectedGroup?.id }"
          @click="selectedSetId = group.id"
        >
          {{ group.name }}
        </button>
      </nav>

      <div class="honor-shop__list">
        <article v-for="item in selectedGroup?.items ?? []" :key="item.itemId" class="honor-shop__item">
          <span class="honor-shop__icon" aria-hidden="true">
            <ItemIcon
              :icon-id="item.iconId"
              :item-id="item.itemId"
              :name="item.name"
              type="Equipment"
              :equipment-slot="item.slot"
              :rarity="item.rarity"
              decorative
            />
          </span>

          <div class="honor-shop__copy">
            <strong>{{ slotLabel(item.slot) }}</strong>
            <small>{{ selectedGroup?.name }}</small>
          </div>

          <div class="honor-shop__price">
            <strong>{{ item.honorPrice }}</strong>
            <small>Honor</small>
          </div>

          <UIButton
            size="sm"
            :disabled="Boolean(pendingItemId) || Boolean(unavailableReason(item))"
            :aria-label="`Купить ${item.name} за ${item.honorPrice} чести`"
            @click="emit('buy', item.itemId)"
          >
            {{ pendingItemId === item.itemId ? 'Покупка…' : unavailableReason(item) ?? 'Купить' }}
          </UIButton>
        </article>
      </div>
    </template>

    <div v-else class="honor-shop__empty">
      <span>Для вашего класса пока нет наград.</span>
      <UIButton variant="ghost" size="sm" :disabled="loading" @click="emit('refresh')">Обновить</UIButton>
    </div>
  </section>
</template>

<style scoped>
.honor-shop {
  display: grid;
  gap: 0.55rem;
  padding: 0.65rem;
  border: 1px solid rgb(177 151 91 / 26%);
  border-radius: 10px;
  background: linear-gradient(145deg, rgb(18 14 20 / 98%), rgb(8 11 18 / 98%));
}
.honor-shop__head {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 0.75rem;
}
.honor-shop__head > div:first-child { display: grid; gap: 0.08rem; }
.honor-shop__head small { color: #b39764; font-size: 0.72rem; }
.honor-shop__head h3 { margin: 0; color: #ead9b0; font-family: var(--ui-font-display); font-size: 1.08rem; }
.honor-shop__balance { display: grid; justify-items: end; line-height: 1; }
.honor-shop__balance span { color: #8f877b; font-size: 0.68rem; }
.honor-shop__balance strong { color: #efcf83; font-size: 1.1rem; }
.honor-shop__sets {
  display: flex;
  gap: 0.35rem;
  overflow-x: auto;
  padding-bottom: 0.1rem;
  scrollbar-width: none;
}
.honor-shop__sets::-webkit-scrollbar { display: none; }
.honor-shop__sets button {
  flex: 0 0 auto;
  max-width: 12rem;
  padding: 0.42rem 0.55rem;
  overflow: hidden;
  border: 1px solid rgb(177 151 91 / 22%);
  border-radius: 999px;
  background: rgb(255 255 255 / 2.5%);
  color: #9b9388;
  font: inherit;
  font-size: 0.72rem;
  font-weight: 700;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.honor-shop__sets button.is-active {
  border-color: rgb(216 174 91 / 55%);
  background: rgb(159 112 45 / 16%);
  color: #efd391;
}
.honor-shop__list { display: grid; gap: 0.28rem; }
.honor-shop__item {
  display: grid;
  grid-template-columns: 42px minmax(0, 1fr) auto auto;
  align-items: center;
  gap: 0.45rem;
  min-height: 54px;
  padding: 0.35rem;
  border: 1px solid rgb(255 255 255 / 5%);
  border-radius: 8px;
  background: rgb(255 255 255 / 2%);
}
.honor-shop__icon {
  width: 42px;
  height: 42px;
  padding: 3px;
  border: 1px solid rgb(196 161 92 / 28%);
  border-radius: 7px;
  background: #090c13;
}
.honor-shop__copy { display: grid; min-width: 0; gap: 0.08rem; }
.honor-shop__copy strong { color: #e8e0d5; font-size: 0.78rem; }
.honor-shop__copy small {
  overflow: hidden;
  color: #817b73;
  font-size: 0.64rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.honor-shop__price { display: grid; min-width: 2.8rem; justify-items: end; }
.honor-shop__price strong { color: #edcc81; font-size: 0.8rem; }
.honor-shop__price small { color: #81786a; font-size: 0.58rem; }
.honor-shop__item :deep(.ui-button) { min-width: 4.7rem; min-height: 36px; padding-inline: 0.55rem; }
.honor-shop__error {
  margin: 0;
  padding: 0.45rem 0.5rem;
  border-radius: 7px;
  background: rgb(94 27 40 / 25%);
  color: #ff9aa7;
  font-size: 0.7rem;
}
.honor-shop__empty {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  min-height: 54px;
  color: #918b82;
  font-size: 0.72rem;
}
@media (max-width: 430px) {
  .honor-shop__item { grid-template-columns: 40px minmax(0, 1fr) auto; }
  .honor-shop__icon { width: 40px; height: 40px; }
  .honor-shop__price { grid-column: 3; grid-row: 1; }
  .honor-shop__item :deep(.ui-button) { grid-column: 2 / 4; width: 100%; min-height: 34px; }
}
</style>
