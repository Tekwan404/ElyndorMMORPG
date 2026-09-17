const PROCEDURAL_DESCRIPTION = /^(.+?):\s*процедурно генерируемая экипировка(?: для [A-Z_]+)?(?: ранней)? прогрессии\.?$/i
const PROGRESSION_DESCRIPTION = /^.+? прогрессии «(.+?)»\.?$/i

/**
 * Keeps content-pipeline notes out of player-facing item descriptions.
 * Technical content metadata remains unchanged in the source definitions.
 */
export function playerItemDescription(description: string | null | undefined): string {
  const source = description?.trim()
  if (!source) return 'Описание предмета пока не найдено.'

  const procedural = source.match(PROCEDURAL_DESCRIPTION)
  if (procedural?.[1]) return `Часть комплекта «${procedural[1].trim()}».`

  const progression = source.match(PROGRESSION_DESCRIPTION)
  if (progression?.[1]) return `Часть комплекта «${progression[1].trim()}».`

  return source
}
