export interface ProfileCosmetic {
  id: string
  name: string
  description: string
  kind: 'Frame' | 'Title'
  styleId: 'ash' | 'ether' | 'iron'
  crystalPrice: number
  owned: boolean
  enabled: boolean
}
export interface PersonalizationStore {
  crystalBalance: number
  characterName: string
  activeFrameId: string | null
  activeTitleId: string | null
  renamePrice: number | null
  cosmetics: ProfileCosmetic[]
}
export interface PersonalizationMutation {
  crystalBalance: number
  characterName: string
  activeFrameId: string | null
  activeTitleId: string | null
}

export const personalizationError: Record<string, string> = {
  cosmetic_insufficient_crystals: 'Не хватает кристаллов.',
  cosmetic_already_owned: 'Эта покупка уже есть в вашей коллекции.',
  cosmetic_not_owned: 'Сначала приобретите это украшение.',
  cosmetic_invalid_slot: 'Украшение больше недоступно для этого слота.',
  cosmetic_not_for_sale: 'Украшение больше не продаётся.',
  personalization_in_combat: 'Имя можно изменить после завершения боя.',
  personalization_operation_conflict: 'Этот запрос уже использован для другой покупки. Обновите магазин.',
  rename_name_taken: 'Это имя уже занято. Выберите другое.',
  rename_same_name: 'У героя уже это имя.',
  rename_unavailable: 'Смена имени временно недоступна.',
  character_name_required: 'Введите новое имя.',
  character_name_invalid_length: 'Имя должно содержать от 3 до 16 символов.',
  character_name_invalid_display_form: 'Уберите пробелы по краям и необычные символы.',
  character_name_invalid_separator: 'Пробел или дефис допустимы только между словами.',
  character_name_invalid_character: 'В имени допустимы буквы, цифры 0–9, пробел и дефис.',
  character_name_invalid_script: 'Используйте кириллицу или латиницу.',
  character_name_mixed_scripts: 'Нельзя смешивать кириллицу и латиницу в одном имени.',
}
