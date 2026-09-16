import { describe, expect, it } from 'vitest'

import { playerItemDescription } from '@/game/items/itemPresentation'

describe('playerItemDescription', () => {
  it('hides procedural content-pipeline wording and class ids', () => {
    expect(playerItemDescription(
      'Следопыт Шепчущего Леса: процедурно генерируемая экипировка для ARCHER ранней прогрессии.',
    )).toBe('Часть комплекта «Следопыт Шепчущего Леса».')
  })

  it('turns progression notes into a player-facing set description', () => {
    expect(playerItemDescription('Щит прогрессии «Сталь Пограничника».'))
      .toBe('Часть комплекта «Сталь Пограничника».')
  })

  it('keeps ordinary item descriptions unchanged', () => {
    expect(playerItemDescription('Старый клинок пограничной стражи.'))
      .toBe('Старый клинок пограничной стражи.')
  })

  it('uses a friendly fallback for missing descriptions', () => {
    expect(playerItemDescription(null)).toBe('Описание предмета пока не найдено.')
  })
})
