import type { WorldLocation } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'

export type LocationKind = 'city' | 'region' | 'dungeon'

export interface LocationPresentation {
  label: string
  art: string
  kind: LocationKind
  dangerLabel: string
}

const PRESENTATIONS: Record<string, LocationPresentation> = {
  STARTER_TOWN: {
    label: 'Стартовый город',
    art: gameArt.locations.STARTER_TOWN,
    kind: 'city',
    dangerLabel: 'Безопасная зона',
  },
  WHISPERING_FOREST: {
    label: 'Шепчущий лес',
    art: gameArt.locations.WHISPERING_FOREST,
    kind: 'region',
    dangerLabel: 'Опасная область',
  },
  DEEP_FOREST: {
    label: 'Глубокий лес',
    art: gameArt.locations.DEEP_FOREST,
    kind: 'region',
    dangerLabel: 'Высокий риск',
  },
  ANCIENT_MINE: {
    label: 'Древняя шахта',
    art: gameArt.locations.ANCIENT_MINE,
    kind: 'dungeon',
    dangerLabel: 'Подземелье',
  },
  ECLIPSED_CITADEL: {
    label: 'Цитадель Затмения',
    art: gameArt.locations.ECLIPSED_CITADEL,
    kind: 'dungeon',
    dangerLabel: 'Подземелье',
  },
  SHATTERED_ORDER_CITADEL_TEST: {
    label: 'Цитадель Расколотого Ордена',
    art: gameArt.locations.SHATTERED_ORDER_CITADEL_TEST,
    kind: 'dungeon',
    dangerLabel: 'Тестовое подземелье',
  },
  BROODMOTHER_LAIR: {
    label: 'Логово Прародительницы',
    art: gameArt.locations.BROODMOTHER_LAIR,
    kind: 'region',
    dangerLabel: 'Высокий риск',
  },
  BLIGHTED_GROVE: {
    label: 'Осквернённая чаща',
    art: gameArt.locations.BLIGHTED_GROVE,
    kind: 'region',
    dangerLabel: 'Высокий риск',
  },
}

const FALLBACK_PRESENTATION: LocationPresentation = {
  label: 'Неизвестная область',
  art: gameArt.world.ancientRuins,
  kind: 'region',
  dangerLabel: 'Неизвестная угроза',
}

function safeFallbackLabel(label: string | null | undefined): string {
  if (!label) return FALLBACK_PRESENTATION.label
  return /[А-Яа-яЁё]/.test(label) ? label : FALLBACK_PRESENTATION.label
}

export function locationPresentation(
  locationId: string | null | undefined,
  fallbackLabel?: string | null,
): LocationPresentation {
  const presentation = locationId ? PRESENTATIONS[locationId] : undefined
  if (presentation) return presentation
  return {
    ...FALLBACK_PRESENTATION,
    label: safeFallbackLabel(fallbackLabel),
  }
}

export function locationLabel(
  location: Pick<WorldLocation, 'id' | 'displayName'> | { id: string; displayName?: string | null },
): string {
  return locationPresentation(location.id, location.displayName).label
}

export function locationKind(locationId: string | null | undefined): LocationKind {
  return locationPresentation(locationId).kind
}
