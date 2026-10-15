export interface NormalizedRect {
  x: number
  y: number
  width: number
  height: number
}

export interface FormationConstraints {
  minUpperBodyClearance: number
  protectedUpperBodyRatio: number
  minimumTouchTargetPx: number
}

export interface BattleFormationInput {
  actorIds: readonly string[]
  localActorId: string
  aggroActorId?: string | null
  selectedActorId?: string | null
  viewportWidthPx: number
  arenaWidthPx: number
  arenaHeightPx: number
  constraints?: Partial<FormationConstraints>
}

export interface BattleFormationSlot {
  actorId: string
  slotId: string
  baseSlotId: string
  x: number
  y: number
  scale: number
  zIndex: number
  visible: boolean
  isFrontline: boolean
  touchTargetPx: number
  bounds: NormalizedRect
  protectedUpperBodyBounds: NormalizedRect
}

export interface BattleFormationResult {
  mode: 'all-visible' | 'frontline-three'
  slots: BattleFormationSlot[]
  overflowActorIds: string[]
  constraints: FormationConstraints
}

interface SlotTemplate {
  id: string
  bounds: NormalizedRect
  scale: number
  zIndex: number
}

export const DEFAULT_FORMATION_CONSTRAINTS: FormationConstraints = {
  minUpperBodyClearance: 0.035,
  protectedUpperBodyRatio: 0.34,
  minimumTouchTargetPx: 44,
}

const FRONTLINE_SLOT: SlotTemplate = {
  id: 'frontline',
  bounds: { x: 0.35, y: 0.5, width: 0.3, height: 0.49 },
  scale: 1,
  zIndex: 50,
}
const SOLO_FRONTLINE_SLOT: SlotTemplate = {
  id: 'solo-frontline',
  bounds: { x: 0.29, y: 0.45, width: 0.42, height: 0.54 },
  scale: 1,
  zIndex: 50,
}
const BASE_SLOTS: readonly SlotTemplate[] = [
  {
    id: 'second-left',
    bounds: { x: 0.03, y: 0.26, width: 0.27, height: 0.44 },
    scale: 0.92,
    zIndex: 20,
  },
  {
    id: 'second-right',
    bounds: { x: 0.7, y: 0.26, width: 0.27, height: 0.44 },
    scale: 0.92,
    zIndex: 21,
  },
  {
    id: 'front-left',
    bounds: { x: 0.02, y: 0.6, width: 0.25, height: 0.39 },
    scale: 1,
    zIndex: 45,
  },
  {
    id: 'front-right',
    bounds: { x: 0.73, y: 0.6, width: 0.25, height: 0.39 },
    scale: 1,
    zIndex: 46,
  },
]

export function protectedBoundsOverlap(left: NormalizedRect, right: NormalizedRect): boolean {
  return (
    left.x < right.x + right.width &&
    left.x + left.width > right.x &&
    left.y < right.y + right.height &&
    left.y + left.height > right.y
  )
}

export function buildBattleFormation(input: BattleFormationInput): BattleFormationResult {
  const constraints = {
    ...DEFAULT_FORMATION_CONSTRAINTS,
    ...input.constraints,
  }
  const actorIds = [...new Set(input.actorIds.filter(Boolean))]
  const frontlineActorId = resolveFrontlineActorId(actorIds, input.aggroActorId, input.localActorId)
  const allVisibleSlots = createSlots(actorIds, frontlineActorId, input, constraints)

  if (allVisibleSlots.length <= 3 || hasValidGeometry(allVisibleSlots)) {
    return {
      mode: 'all-visible',
      slots: allVisibleSlots,
      overflowActorIds: [],
      constraints,
    }
  }

  const visibleActorIds = prioritizedActorIds(input, actorIds).slice(0, 3)
  const visibleSet = new Set(visibleActorIds)
  const fallbackSlots = createSlots(visibleActorIds, frontlineActorId, input, constraints)
  const fallbackByActorId = new Map(fallbackSlots.map((slot) => [slot.actorId, slot]))
  const hiddenSlots = allVisibleSlots
    .filter((slot) => !visibleSet.has(slot.actorId))
    .map((slot) => ({ ...slot, visible: false }))

  return {
    mode: 'frontline-three',
    slots: [...visibleActorIds.map((actorId) => fallbackByActorId.get(actorId)!), ...hiddenSlots],
    overflowActorIds: actorIds.filter((actorId) => !visibleSet.has(actorId)),
    constraints,
  }
}

function createSlots(
  actorIds: readonly string[],
  frontlineActorId: string | undefined,
  input: BattleFormationInput,
  constraints: FormationConstraints,
): BattleFormationSlot[] {
  const otherIds = actorIds.filter((id) => id !== frontlineActorId)
  return actorIds.map((actorId) => {
    const baseSlot =
      actorId === frontlineActorId
        ? FRONTLINE_SLOT
        : (BASE_SLOTS[otherIds.indexOf(actorId)] ?? BASE_SLOTS[BASE_SLOTS.length - 1]!)
    const isFrontline = actorId === frontlineActorId
    const renderedSlot =
      actorIds.length === 1 ? SOLO_FRONTLINE_SLOT : isFrontline ? FRONTLINE_SLOT : baseSlot
    const bounds = renderedSlot.bounds
    const protectedUpperBodyBounds = expandRect(
      {
        x: bounds.x,
        y: bounds.y,
        width: bounds.width,
        height: bounds.height * constraints.protectedUpperBodyRatio,
      },
      constraints.minUpperBodyClearance,
    )

    return {
      actorId,
      slotId: renderedSlot.id,
      baseSlotId: baseSlot.id,
      x: bounds.x + bounds.width / 2,
      y: bounds.y,
      scale: renderedSlot.scale,
      zIndex: renderedSlot.zIndex,
      visible: true,
      isFrontline,
      touchTargetPx: Math.max(
        constraints.minimumTouchTargetPx,
        Math.min(bounds.width * input.arenaWidthPx, bounds.height * input.arenaHeightPx),
      ),
      bounds: { ...bounds },
      protectedUpperBodyBounds,
    }
  })
}

function resolveFrontlineActorId(
  actorIds: readonly string[],
  aggroActorId: string | null | undefined,
  localActorId: string,
): string | undefined {
  if (actorIds.includes(localActorId)) return localActorId
  if (aggroActorId && actorIds.includes(aggroActorId)) return aggroActorId
  return actorIds[0]
}

function prioritizedActorIds(input: BattleFormationInput, actorIds: readonly string[]): string[] {
  const candidates = [input.localActorId, input.aggroActorId, input.selectedActorId, ...actorIds]
  const available = new Set(actorIds)
  const result: string[] = []

  for (const actorId of candidates) {
    if (actorId && available.has(actorId) && !result.includes(actorId)) {
      result.push(actorId)
    }
  }

  return result
}

function hasValidGeometry(slots: readonly BattleFormationSlot[]): boolean {
  for (let left = 0; left < slots.length; left += 1) {
    for (let right = left + 1; right < slots.length; right += 1) {
      if (
        protectedBoundsOverlap(
          slots[left]!.protectedUpperBodyBounds,
          slots[right]!.protectedUpperBodyBounds,
        )
      ) {
        return false
      }
    }
  }

  return true
}

function expandRect(rect: NormalizedRect, clearance: number): NormalizedRect {
  const inset = clearance / 2

  return {
    x: rect.x - inset,
    y: rect.y - inset,
    width: rect.width + clearance,
    height: rect.height + clearance,
  }
}
