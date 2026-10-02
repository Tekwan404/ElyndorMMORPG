const ERRORS: Record<string, string> = {
  arena_disabled: 'Арена пока недоступна.',
  arena_match_active: 'Вы уже участвуете в бою на арене.',
  arena_pve_combat_active: 'Завершите текущий бой, чтобы выйти на арену.',
  arena_dungeon_active: 'Нельзя встать в очередь во время подземелья.',
  arena_character_unavailable: 'Герой сейчас недоступен: он мёртв, в пути или в AFK-ферме.',
  arena_unsupported_build: 'Эта сборка героя пока не поддерживается на арене.',
  arena_already_queued: 'Вы уже стоите в другой очереди.',
  arena_queue_mode_invalid: 'Неизвестный режим очереди.',
  arena_match_not_found: 'Бой не найден или уже завершён.',
  arena_ended: 'Бой уже завершён.',
  arena_duplicate_command: 'Команда уже была выполнена.',
  arena_invalid_target: 'Недопустимая цель.',
  rate_limited: 'Слишком часто. Подождите секунду.',
  character_not_found: 'Герой не найден.',
  arena_invite_invalid: 'Проверьте имя персонажа.',
  arena_invite_player_not_found: 'Персонаж с таким именем не найден.',
  arena_invite_self: 'Нельзя пригласить самого себя.',
  arena_invite_pending: 'У вас уже есть приглашение. Отмените его или дождитесь ответа; между отправками — 30 секунд.',
  arena_invite_queued: 'Перед дружеским боем обоим нужно выйти из очереди.',
  arena_invite_not_found: 'Приглашение не найдено или адресовано другому герою.',
  arena_invite_expired: 'Приглашение истекло или уже закрыто.',
  arena_invite_player_offline: 'Оба игрока должны открыть раздел «Арена». Друг сейчас не в сети арены.',
  arena_invite_failed: 'Не удалось обновить приглашение. Попробуйте ещё раз.',
  arena_start_failed: 'Не удалось начать бой. Отправьте новое приглашение.',
}

export function arenaErrorMessage(code: string | null | undefined): string {
  if (!code) return ''
  return ERRORS[code] ?? 'Не удалось выполнить действие на арене.'
}

export function arenaResultLabel(result: string | null | undefined, outcome: string | undefined): string {
  if (result === 'Victory') return 'Победа'
  if (result === 'Defeat') return 'Поражение'
  if (outcome === 'Draw') return 'Ничья'
  if (outcome === 'Cancelled' || result === 'Cancelled') return 'Бой отменён'
  return ''
}
