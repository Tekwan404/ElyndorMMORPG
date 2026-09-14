import type { TalentBranchId } from '@/api/contracts'

export type TalentVisualPosition = Readonly<{
  row: number
  column: number
}>

export const WARRIOR_TALENT_VISUAL_COLUMN_COUNT = 12

const guardianLayout: Readonly<Record<string, TalentVisualPosition>> = {
  'G-1-1': { row: 1, column: 6 },
  'G-1-2': { row: 1, column: 2 },
  'G-1-3': { row: 1, column: 4 },
  'G-1-4': { row: 1, column: 8 },
  'G-1-5': { row: 1, column: 10 },

  'G-2-1': { row: 2, column: 2 },
  'G-2-2': { row: 2, column: 5 },
  'G-2-4': { row: 2, column: 7 },
  'G-2-5': { row: 2, column: 10 },

  'G-2-3': { row: 3, column: 4 },
  'G-3-1': { row: 3, column: 6 },
  'G-3-3': { row: 3, column: 8 },
  'G-3-5': { row: 3, column: 10 },

  'G-4-1': { row: 4, column: 1 },
  'G-4-2': { row: 4, column: 3 },
  'G-3-2': { row: 4, column: 5 },
  'G-4-3': { row: 4, column: 7 },
  'G-3-4': { row: 4, column: 9 },
  'G-3-6': { row: 4, column: 11 },

  'G-5-1': { row: 5, column: 3 },
  'G-4-4': { row: 5, column: 5 },
  'G-5-3': { row: 5, column: 7 },
  'G-4-5': { row: 5, column: 9 },
  'G-5-5': { row: 5, column: 11 },

  'G-5-4': { row: 6, column: 2 },
  'G-5-2': { row: 6, column: 4 },

  'G-6-3': { row: 7, column: 3 },
  'G-6-1': { row: 7, column: 6 },
  'G-6-4': { row: 7, column: 9 },

  'G-6-2': { row: 8, column: 6 },
  'G-6-5': { row: 9, column: 6 },
}

const branchLayouts: Partial<Record<TalentBranchId, Readonly<Record<string, TalentVisualPosition>>>> = {
  GUARDIAN: guardianLayout,
}

export function resolveWarriorTalentVisualPosition(
  branchId: TalentBranchId,
  talentId: string,
): TalentVisualPosition | null {
  return branchLayouts[branchId]?.[talentId] ?? null
}
