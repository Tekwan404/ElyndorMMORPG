import { computed, onScopeDispose, shallowRef, watch } from 'vue'
import { apiClient } from '@/api/apiClient'
import type { InventoryItem, ItemReforgePreview, ItemReforgeResponse } from '@/api/contracts'
import type { ItemSalvagePreviewV2 } from '@/api/itemEnhancementContracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { canAffordMoney } from '@/shared/money'
import {
  availableForgeMaterialQuantity,
  forgeableAffixes,
  forgeItemAvailability,
  reforgeResultAffixes,
} from './forgePresentation'
import {
  canSalvage,
  forgeCategory,
  salvageMaterials,
  type ForgeCategory,
} from './forgeWorkbenchPresentation'

export type ForgeMode = 'reforge' | 'upgrade' | 'salvage'
export interface EnhancementPreview {
  itemInstanceId: string
  currentEnhancementLevel: number
  targetEnhancementLevel: number
  isMaximumEnhancement: boolean
  enhancementBonusPercent: number
  intrinsicItemPower: number | null
  finalItemPower: number | null
  cost: {
    gold: number
    enhancementMaterialItemId: string
    enhancementMaterialQuantity: number
    catalystItemId: string | null
    catalystQuantity: number
  }
}

export function useForgeWorkbench() {
  const session = useGameSessionStore()
  const mode = shallowRef<ForgeMode>('reforge')
  const source = shallowRef<'backpack' | 'equipped'>('backpack')
  const category = shallowRef<ForgeCategory | null>(null)
  const selectedId = shallowRef<string | null>(null)
  const slotKey = shallowRef<string | null>(null)
  const selectedIds = shallowRef<string[]>([])
  const preview = shallowRef<ItemReforgePreview | null>(null)
  const pending = shallowRef<ItemReforgeResponse | null>(null)
  const enhancement = shallowRef<EnhancementPreview | null>(null)
  const salvagePreviews = shallowRef<ItemSalvagePreviewV2[]>([])
  const loading = shallowRef(false)
  const acting = shallowRef(false)
  const error = shallowRef<string | null>(null)
  const notice = shallowRef<string | null>(null)
  const refreshKey = shallowRef(0)
  let revision = 0
  onScopeDispose(() => {
    revision++
  })
  const items = computed(() => session.snapshot?.character?.inventory.items ?? [])
  const gold = computed(() => session.snapshot?.character?.gold ?? 0)
  const equipment = computed(() => items.value.filter((item) => item.type === 'Equipment'))
  const displayed = computed(() =>
    equipment.value
      .filter(
        (item) =>
          (source.value === 'equipped' ? item.equippedSlot !== null : item.equippedSlot === null) &&
          (!category.value || forgeCategory(item) === category.value),
      )
      .sort(
        (a, b) =>
          (b.generatedItem?.itemPower ?? 0) - (a.generatedItem?.itemPower ?? 0) ||
          a.id.localeCompare(b.id),
      ),
  )
  const selected = computed(
    () => equipment.value.find((item) => item.id === selectedId.value) ?? null,
  )
  const affixes = computed(() => selected.value?.generatedItem?.affixes ?? [])
  const reforgeAffixes = computed(() => (selected.value ? forgeableAffixes(selected.value) : []))
  const resultAffixes = computed(() =>
    pending.value
      ? reforgeResultAffixes(pending.value.current, pending.value.proposed, pending.value.slotKey)
      : null,
  )
  const busy = computed(() => acting.value || session.mutationPending)
  const stones = computed(() => available('REFORGE_STONE'))
  const rewards = computed(() => salvageMaterials(salvagePreviews.value))
  const salvageReady = computed(
    () =>
      !loading.value &&
      selectedIds.value.length > 0 &&
      salvagePreviews.value.length === selectedIds.value.length,
  )
  const canReforge = computed(
    () =>
      !!preview.value &&
      affordable(
        preview.value.cost.gold,
        preview.value.cost.materialItemId,
        preview.value.cost.materialQuantity,
        preview.value.cost.catalystItemId,
        preview.value.cost.catalystQuantity,
      ),
  )
  const canEnhance = computed(
    () =>
      !!enhancement.value &&
      !enhancement.value.isMaximumEnhancement &&
      affordable(
        enhancement.value.cost.gold,
        enhancement.value.cost.enhancementMaterialItemId,
        enhancement.value.cost.enhancementMaterialQuantity,
        enhancement.value.cost.catalystItemId,
        enhancement.value.cost.catalystQuantity,
      ),
  )

  function available(id: string | null) {
    return id ? availableForgeMaterialQuantity(items.value, id) : 0
  }
  function affordable(
    price: number,
    material: string | null,
    quantity: number,
    catalyst: string | null,
    catalystQuantity: number,
  ) {
    return (
      canAffordMoney(gold.value, price) &&
      available(material) >= quantity &&
      available(catalyst) >= catalystQuantity
    )
  }
  function materialLabel(id: string) {
    return (
      items.value.find((item) => item.definitionId === id)?.name ??
      (
        {
          REFORGE_STONE: 'Камни перековки',
          FORGE_SCRAP: 'Кузнечный лом',
          ENHANCEMENT_ORE: 'Закалочная руда',
          DUNGEON_CATALYST: 'Ядро подземелья',
        } as Record<string, string>
      )[id] ??
      id
    )
  }
  function setMode(next: ForgeMode) {
    if (busy.value || pending.value) return
    mode.value = next
    error.value = null
    notice.value = null
    if (next === 'salvage') source.value = 'backpack'
  }
  function select(item: InventoryItem) {
    if (busy.value || pending.value) return
    error.value = null
    notice.value = null
    if (mode.value === 'salvage') {
      if (!canSalvage(item)) return
      selectedIds.value = selectedIds.value.includes(item.id)
        ? selectedIds.value.filter((id) => id !== item.id)
        : [...selectedIds.value, item.id]
    } else {
      selectedId.value = item.id
      slotKey.value = item.reforgeSlotKey ?? forgeableAffixes(item)[0]?.slotKey ?? null
    }
  }
  function clearBatch() {
    if (!busy.value) {
      error.value = null
      selectedIds.value = []
    }
  }
  function selectAll() {
    if (!busy.value) {
      error.value = null
      selectedIds.value = equipment.value.filter(canSalvage).map((item) => item.id)
    }
  }
  function retry() {
    if (!busy.value) {
      error.value = null
      refreshKey.value++
    }
  }
  function selectAffix(key: string) {
    if (
      !busy.value &&
      !pending.value &&
      reforgeAffixes.value.some((affix) => affix.slotKey === key)
    ) {
      error.value = null
      slotKey.value = key
    }
  }

  watch(
    [mode, selectedId, slotKey, selectedIds, refreshKey],
    async () => {
      const request = ++revision
      preview.value = null
      enhancement.value = null
      salvagePreviews.value = []
      loading.value = true
      try {
        if (mode.value === 'salvage') {
          const prepared: ItemSalvagePreviewV2[] = []
          const requestedIds = selectedIds.value
          for (const id of requestedIds) {
            const item = equipment.value.find((candidate) => candidate.id === id)
            if (!item || !canSalvage(item))
              throw new Error('Список изменился. Выберите предметы заново.')
            const result = await session.getSalvagePreview(id)
            if (request !== revision) return
            if (!result)
              throw new Error(
                `Не удалось рассчитать разбор «${item.name}». Измените выбор, чтобы повторить.`,
              )
            prepared.push(result)
          }
          salvagePreviews.value = prepared
        } else if (selected.value) {
          const item = selected.value
          if (item.transactionLocked) {
            const restored = await session.getPendingReforge(item.id)
            if (request !== revision) return
            if (restored) {
              pending.value = restored
              mode.value = 'reforge'
              return
            }
          }
          if (mode.value === 'reforge' && forgeItemAvailability(item).available && slotKey.value) {
            const result = await session.getReforgePreview(item.id, slotKey.value)
            if (request !== revision) return
            if (!result) throw new Error('Не удалось получить стоимость. Повторите выбор предмета.')
            preview.value = result
          } else if (
            mode.value === 'upgrade' &&
            item.generatedItem &&
            !item.isLocked &&
            !item.transactionLocked
          ) {
            const result = await apiClient.request<EnhancementPreview>(
              `/api/v1/inventory/enhancement/preview/${encodeURIComponent(item.id)}`,
            )
            if (request !== revision) return
            enhancement.value = result
          }
        }
      } catch (failure) {
        if (request === revision)
          error.value =
            failure instanceof Error ? failure.message : 'Не удалось получить данные кузницы.'
      } finally {
        if (request === revision) loading.value = false
      }
    },
    { immediate: true },
  )

  watch(equipment, () => {
    if (acting.value) return
    const valid = selectedIds.value.filter((id) =>
      equipment.value.some((item) => item.id === id && canSalvage(item)),
    )
    if (valid.length !== selectedIds.value.length) selectedIds.value = valid
    if (selectedId.value && !selected.value) {
      selectedId.value = null
      pending.value = null
    }
  })

  async function run(action: () => Promise<boolean>, success: string) {
    if (busy.value) return
    acting.value = true
    error.value = null
    notice.value = null
    try {
      if (await action()) notice.value = success
      else if (!error.value)
        error.value =
          'Действие не выполнено. Проверьте ресурсы и состояние предмета, затем повторите.'
    } catch {
      error.value =
        'Не удалось обновить состояние кузницы. Повторите загрузку перед следующим действием.'
      preview.value = null
      enhancement.value = null
    } finally {
      acting.value = false
    }
  }
  async function roll() {
    if (!selected.value || !slotKey.value || !canReforge.value || loading.value) return
    const id = selected.value.id,
      key = slotKey.value
    await run(async () => {
      pending.value = await session.rollReforge(id, key)
      if (pending.value) preview.value = null
      return !!pending.value
    }, 'Выберите текущую или новую характеристику.')
  }
  async function decide(accept: boolean) {
    if (!pending.value) return
    const operation = pending.value.operationId
    await run(
      async () => {
        const result = await session.decideReforge(operation, accept)
        if (!result) return false
        pending.value = null
        selectedId.value = null
        return true
      },
      accept ? 'Новая характеристика применена.' : 'Текущая характеристика сохранена.',
    )
  }
  async function enhance() {
    if (!selected.value || !canEnhance.value || loading.value) return
    const id = selected.value.id
    await run(async () => {
      const result = await session.enhanceItem(id)
      if (!result) return false
      enhancement.value = await apiClient.request<EnhancementPreview>(
        `/api/v1/inventory/enhancement/preview/${encodeURIComponent(id)}`,
      )
      return true
    }, 'Предмет усилен. Звёзды и качество сохранены.')
  }
  async function salvage() {
    if (!salvageReady.value) return
    const batch = [...salvagePreviews.value]
    if (
      !batch.every((entry) =>
        equipment.value.some((item) => item.id === entry.characterItemId && canSalvage(item)),
      )
    ) {
      error.value = 'Список изменился. Выберите предметы заново.'
      return
    }
    await run(async () => {
      let completed = 0
      for (const entry of batch) {
        const result = await session.salvageItemDetailed(
          entry.characterItemId,
          entry.requiresConfirmation,
        )
        if (!result) {
          selectedIds.value = batch.slice(completed).map((item) => item.characterItemId)
          error.value = `Разобрано ${completed} из ${batch.length}. Следующее действие не подтверждено. Проверьте инвентарь перед повтором.`
          return false
        }
        completed++
      }
      selectedIds.value = []
      return true
    }, `Разобрано предметов: ${batch.length}. Материалы получены.`)
    if (error.value) notice.value = null
  }
  return {
    session,
    mode,
    source,
    category,
    selectedId,
    slotKey,
    selectedIds,
    displayed,
    equipment,
    selected,
    affixes,
    reforgeAffixes,
    preview,
    pending,
    resultAffixes,
    enhancement,
    loading,
    busy,
    error,
    notice,
    gold,
    stones,
    rewards,
    salvageReady,
    canReforge,
    canEnhance,
    available,
    materialLabel,
    setMode,
    select,
    clearBatch,
    selectAll,
    roll,
    decide,
    enhance,
    salvage,
    retry,
    selectAffix,
  }
}
