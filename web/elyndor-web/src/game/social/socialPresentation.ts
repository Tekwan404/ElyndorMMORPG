const messages: Record<string, string> = {
  party_not_leader: 'Это действие выполняет лидер группы.',
  party_full: 'В группе уже пять участников.',
  party_already_in_party: 'Персонаж уже состоит в группе.',
  party_invite_expired: 'Приглашение истекло. Попросите лидера отправить новое.',
  party_target_not_friend: 'Сначала добавьте персонажа в друзья.',
  party_not_found: 'Группа больше не существует.',
  dungeon_party_required: 'Создайте группу перед входом в подземелье.',
  dungeon_not_leader: 'Забег запускает лидер группы.',
  dungeon_invalid_location: 'Всем участникам нужно переместиться ко входу в подземелье.',
  dungeon_member_cannot_enter: 'Участник не готов к входу: проверьте уровень, здоровье и текущую активность.',
  dungeon_encounter_active: 'Сначала завершите текущий бой.',
  dungeon_encounter_not_ready: 'Обновите состояние забега и проверьте готовность участников.',
  dungeon_member_not_in_party: 'Для этого действия нужно состоять в группе забега.',
  dungeon_member_not_in_run: 'Сначала войдите в забег.',
  dungeon_run_not_found: 'Забег больше недоступен.',
  friend_request_already_pending: 'Заявка уже отправлена.',
  friend_already_friends: 'Этот персонаж уже в друзьях.',
  network_unavailable: 'Нет связи с сервером. Попробуйте ещё раз.',
}

export function socialErrorMessage(code: string): string {
  return messages[code] ?? 'Не удалось выполнить действие. Обновите состояние и попробуйте ещё раз.'
}
