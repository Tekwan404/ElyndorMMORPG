from pathlib import Path


def rep(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected 1 match, got {count}: {old[:180]!r}")
    p.write_text(text.replace(old, new, 1))


# Persist lazy HP recovery before dungeon relocation for solo and party members.
f = "src/Elyndor.Server/Dungeons/DungeonEndpoints.cs"
rep(f, "using Elyndor.Infrastructure.Persistence;\nusing Microsoft.EntityFrameworkCore;", "using Elyndor.Infrastructure.Persistence;\nusing Elyndor.Infrastructure.World;\nusing Microsoft.EntityFrameworkCore;")
rep(f,
    "PartyService partyService,\n        GameDbContext dbContext,\n        ICombatActivityReader combatActivity,",
    "PartyService partyService,\n        BootstrapService bootstrapService,\n        GameDbContext dbContext,\n        ICombatActivityReader combatActivity,")
rep(f,
    "service,\n                partyService,\n                dbContext,\n                combatActivity,",
    "service,\n                partyService,\n                bootstrapService,\n                dbContext,\n                combatActivity,")
rep(f,
    "DungeonService service,\n        PartyService partyService,\n        GameDbContext dbContext,\n        ICombatActivityReader combatActivity,",
    "DungeonService service,\n        PartyService partyService,\n        BootstrapService bootstrapService,\n        GameDbContext dbContext,\n        ICombatActivityReader combatActivity,")
rep(f,
    "if (party is null)\n            return await service.TeleportToEntryAsync(accountId, dungeonId, requestId, cancellationToken);",
    "if (party is null)\n        {\n            await bootstrapService.GetAsync(accountId, cancellationToken, checkpoint: true);\n            return await service.TeleportToEntryAsync(accountId, dungeonId, requestId, cancellationToken);\n        }")
rep(f,
    "int healthyMemberCount = await dbContext.CharacterVitals",
    "foreach (var member in members)\n        {\n            await bootstrapService.GetAsync(member.AccountId, cancellationToken, checkpoint: true);\n        }\n\n        int healthyMemberCount = await dbContext.CharacterVitals")

# Keep recovery copy aligned with authoritative server values.
rep("web/elyndor-web/src/game/world/views/WorldView.vue",
    "? 'Отдых в городе: здоровье восстанавливается со скоростью 5% от максимального здоровья в секунду.'\n      : 'Вне города здоровье восстанавливается со скоростью 1% от максимального здоровья в секунду.'",
    "? 'Отдых в городе: здоровье восстанавливается со скоростью 15% от максимального здоровья в секунду.'\n      : 'Вне города здоровье восстанавливается со скоростью 10% от максимального здоровья в секунду.'")

# Client-side ability queue. Quick taps are accepted while a request/cast is in flight.
s = "web/elyndor-web/src/stores/combatSession.ts"
rep(s, "const pending = ref(false)\n  const trainingStats", "const pending = ref(false)\n  const abilityQueue = ref<string[]>([])\n  const trainingStats")
rep(s, "let lootRefreshTimer: number | null = null\n  const retryCommandIds", "let lootRefreshTimer: number | null = null\n  let abilityQueueTimer: number | null = null\n  let abilitySending = false\n  const retryCommandIds")
rep(s, """  async function useAbility(abilityId: string): Promise<void> {
    if (!snapshot.value) return
    const sessionId = snapshot.value.sessionId
    await invokeRetryableCommand(
      `UseAbility:${sessionId}:${abilityId}`,
      commandId => invokeWithOutcome('UseAbility', sessionId, abilityId, commandId),
    )
  }
""", """  async function useAbility(abilityId: string): Promise<void> {
    const current = snapshot.value
    if (!current || current.status !== 'Active') return
    if (!current.player.abilities.some((ability) => ability.id === abilityId)) return
    if (abilityQueue.value.length >= 3) return
    abilityQueue.value = [...abilityQueue.value, abilityId]
    scheduleAbilityQueueDrain()
  }

  function scheduleAbilityQueueDrain(delayMs = 0): void {
    if (abilityQueueTimer !== null) window.clearTimeout(abilityQueueTimer)
    abilityQueueTimer = window.setTimeout(() => {
      abilityQueueTimer = null
      void drainAbilityQueue()
    }, Math.max(0, delayMs))
  }

  async function drainAbilityQueue(): Promise<void> {
    if (abilitySending || pending.value || abilityQueue.value.length === 0) {
      if (abilityQueue.value.length > 0) scheduleAbilityQueueDrain(20)
      return
    }

    const current = snapshot.value
    if (!current || current.status !== 'Active') {
      clearAbilityQueue()
      return
    }

    if (current.player.activeCast) {
      scheduleAbilityQueueDrain(Math.max(15, Date.parse(current.player.activeCast.resolvesAtUtc) - Date.now() + 25))
      return
    }

    const abilityId = abilityQueue.value[0]!
    const ability = current.player.abilities.find((candidate) => candidate.id === abilityId)
    if (!ability) {
      abilityQueue.value = abilityQueue.value.slice(1)
      scheduleAbilityQueueDrain()
      return
    }

    const readyAt = current.player.cooldowns[abilityId]
    if (readyAt && Date.parse(readyAt) > Date.now()) {
      scheduleAbilityQueueDrain(Math.max(15, Date.parse(readyAt) - Date.now() + 25))
      return
    }

    abilitySending = true
    const sessionId = current.sessionId
    try {
      const succeeded = await invokeRetryableCommand(
        `UseAbility:${sessionId}:${abilityId}`,
        commandId => invokeWithOutcome('UseAbility', sessionId, abilityId, commandId),
      )
      if (succeeded || errorCode.value !== null) {
        abilityQueue.value = abilityQueue.value.slice(1)
      }
    } finally {
      abilitySending = false
      if (abilityQueue.value.length > 0) scheduleAbilityQueueDrain()
    }
  }

  function clearAbilityQueue(): void {
    abilityQueue.value = []
    if (abilityQueueTimer !== null) {
      window.clearTimeout(abilityQueueTimer)
      abilityQueueTimer = null
    }
  }
""")
rep(s, "retryCommandIds.clear()\n        clearLootRolls()\n        errorCode.value = null", "retryCommandIds.clear()\n        clearAbilityQueue()\n        clearLootRolls()\n        errorCode.value = null")
rep(s, "retryCommandIds.clear()\n      clearLootRolls()\n    }\n    return succeeded", "retryCommandIds.clear()\n      clearAbilityQueue()\n      clearLootRolls()\n    }\n    return succeeded")
rep(s, "if (newSession && incomingSnapshot) {\n      retryCommandIds.clear()", "if (newSession && incomingSnapshot) {\n      retryCommandIds.clear()\n      clearAbilityQueue()")
rep(s, "if (incomingSnapshot && incomingSnapshot.status !== 'Active') {\n      retryCommandIds.clear()\n    }", "if (incomingSnapshot && incomingSnapshot.status !== 'Active') {\n      retryCommandIds.clear()\n      clearAbilityQueue()\n    }")
rep(s, "if (update.reward) {\n      reward.value = update.reward\n      if (update.reward.lootRolls?.length) mergeLootRolls(update.reward.lootRolls)\n    }\n  }", "if (update.reward) {\n      reward.value = update.reward\n      if (update.reward.lootRolls?.length) mergeLootRolls(update.reward.lootRolls)\n    }\n    if (abilityQueue.value.length > 0) scheduleAbilityQueueDrain()\n  }")
rep(s, "pending,\n    isActive,", "pending,\n    abilityQueue,\n    isActive,")

# Combat presentation: remove duplicate self from group roster, show companion state, expire floating feedback.
v = "web/elyndor-web/src/game/combat/views/CombatView.vue"
rep(v, "const combatPlayers = computed(() => snapshot.value?.players ?? (snapshot.value ? [snapshot.value.player] : []))\nconst isParticipantActive", "const combatPlayers = computed(() => snapshot.value?.players ?? (snapshot.value ? [snapshot.value.player] : []))\nconst combatAllies = computed(() => combatPlayers.value.filter((player) => player.actorId !== snapshot.value?.player.actorId))\nconst companion = computed(() => snapshot.value?.companion ?? null)\nconst companionArt = computed(() => monsterArtUrl(companion.value?.artId))\nconst isParticipantActive")
rep(v, "type LogSide = 'player' | 'enemy' | 'system'", "type LogSide = 'player' | 'ally' | 'enemy' | 'system'")
rep(v, "  detail?: string\n}", "  detail?: string\n  occurredAtUtc: string\n}")
rep(v, "        : undefined,\n    })\n  }\n  return entries.slice(-12).reverse()", "        : undefined,\n      occurredAtUtc: event.serverTimeUtc,\n    })\n  }\n  return entries.slice(-12).reverse()")
rep(v, "const recentFeedback = computed(() =>\n  logEntries.value.find((entry) => entry.side !== 'system') ?? logEntries.value[0] ?? null,\n)", "const recentFeedback = computed(() => {\n  const entry = logEntries.value.find((candidate) => candidate.side !== 'system') ?? logEntries.value[0] ?? null\n  if (!entry) return null\n  return now.value - Date.parse(entry.occurredAtUtc) <= 4_500 ? entry : null\n})")
rep(v, "if (source === current.player.actorId) return 'player'\n  if (combatEnemies.value.some((enemy) => enemy.actorId === source)) return 'enemy'", "if (source === current.player.actorId) return 'player'\n  if (current.companion?.actorId === source) return 'ally'\n  if (combatEnemies.value.some((enemy) => enemy.actorId === source)) return 'enemy'")
rep(v, "if (side === 'player') return 'ВЫ'\n  if (side === 'enemy') {", "if (side === 'player') return 'ВЫ'\n  if (side === 'ally') return companion.value?.name.toUpperCase() ?? 'СПУТНИК'\n  if (side === 'enemy') {")
rep(v, "const deadEnemy = combatEnemies.value.find((enemy) => enemy.actorId === event.actorId)\n      return deadEnemy ? `${deadEnemy.name} повержен` : 'Вы повержены'", "const deadEnemy = combatEnemies.value.find((enemy) => enemy.actorId === event.actorId)\n      if (deadEnemy) return `${deadEnemy.name} повержен`\n      if (companion.value?.actorId === event.actorId) return `${companion.value.name} повержен`\n      return 'Вы повержены'")
rep(v, "v-if=\"combatPlayers.length > 1\"\n        class=\"combat-party-roster\"", "v-if=\"combatAllies.length > 0\"\n        class=\"combat-party-roster\"")
rep(v, "<strong>Слаженный отряд · {{ combatPlayers.length }}</strong>", "<strong>Союзники · {{ combatAllies.length }}</strong>")
rep(v, "v-for=\"player in combatPlayers\"", "v-for=\"player in combatAllies\"")
rep(v, """        <div class="player-figure" aria-hidden="true">
          <img v-if="playerArt" :src="playerArt" alt="" />
          <div v-else class="player-figure__fallback">
            {{ snapshot.player.definitionId.slice(0, 1) }}
          </div>
        </div>
""", """        <div class="player-figure" aria-hidden="true">
          <img v-if="playerArt" :src="playerArt" alt="" />
          <div v-else class="player-figure__fallback">
            {{ snapshot.player.definitionId.slice(0, 1) }}
          </div>
        </div>

        <div v-if="companion" class="companion-figure" :data-dead="companion.hp <= 0">
          <div class="companion-figure__portrait">
            <img v-if="companionArt" :src="companionArt" alt="" />
            <IconGenerator v-else :config="{ id: `companion-${companion.actorId}`, glyph: 'star', category: 'utility' }" />
          </div>
          <div class="companion-figure__state">
            <strong>{{ companion.name }}</strong>
            <i><span :style="{ width: `${combatPlayerHealthRatio(companion)}%` }" /></i>
            <small>{{ Math.ceil(companion.hp) }} / {{ Math.ceil(companion.maxHp) }}</small>
          </div>
        </div>
""")
rep(v, ":disabled=\"!ability || combat.pending || abilityState(ability) !== 'ready'\"", ":disabled=\"!ability || abilityState(ability) !== 'ready'\"")
rep(v, ":class=\"{ 'ability-slot--empty': !ability, 'ability-slot--comet': ability?.id === 'FIRE_COMET' }\"", ":class=\"{ 'ability-slot--empty': !ability, 'ability-slot--comet': ability?.id === 'FIRE_COMET', 'ability-slot--queued': ability && combat.abilityQueue.includes(ability.id) }\"")
rep(v, "<span v-if=\"ability?.id === 'COMBUSTION' && combustion\" class=\"ability-slot__proc\" aria-label=\"Возгорание активно\">АКТ.</span>", "<span v-if=\"ability?.id === 'COMBUSTION' && combustion\" class=\"ability-slot__proc\" aria-label=\"Возгорание активно\">АКТ.</span>\n            <span v-if=\"ability && combat.abilityQueue.includes(ability.id)\" class=\"ability-slot__queue\">{{ combat.abilityQueue.indexOf(ability.id) + 1 }}</span>")

# Small scoped CSS additions.
rep(v, ".player-figure__fallback {", ".companion-figure { position: absolute; bottom: .6rem; left: 7.6rem; z-index: 3; display: grid; grid-template-columns: 2.5rem minmax(5rem, 1fr); align-items: center; gap: 6px; max-width: 10rem; padding: 5px 7px; border: 1px solid rgb(79 185 150 / 34%); border-radius: var(--ui-radius-md); background: rgb(5 12 14 / 84%); backdrop-filter: blur(6px); }\n.companion-figure[data-dead='true'] { opacity: .48; }\n.companion-figure__portrait { display: grid; width: 2.5rem; height: 2.5rem; place-items: center; overflow: hidden; border-radius: 50%; background: rgb(79 185 150 / 12%); color: #9be2c9; }\n.companion-figure__portrait img { width: 100%; height: 100%; object-fit: contain; }\n.companion-figure__state { display: grid; min-width: 0; gap: 2px; }\n.companion-figure__state strong { overflow: hidden; font-size: .55rem; text-overflow: ellipsis; white-space: nowrap; }\n.companion-figure__state small { color: var(--ui-color-text-muted); font-size: .44rem; }\n.companion-figure__state i { display: block; height: 4px; overflow: hidden; border-radius: 999px; background: rgb(0 0 0 / 55%); }\n.companion-figure__state i span { display: block; height: 100%; background: #4fb996; }\n\n.player-figure__fallback {")
rep(v, ".combat-feedback[data-side='player'] {\n  color: #d1ccff;\n}", ".combat-feedback[data-side='player'] {\n  color: #d1ccff;\n}\n\n.combat-feedback[data-side='ally'] { color: #9be2c9; }")
rep(v, ".training-stats {\n  display: grid;", ".ability-slot--queued { border-color: rgb(155 226 201 / 55%); box-shadow: inset 0 0 0 1px rgb(155 226 201 / 18%); }\n.ability-slot__queue { position: absolute; top: 3px; left: 3px; z-index: 5; display: grid; width: 1rem; height: 1rem; place-items: center; border-radius: 50%; background: #9be2c9; color: #07110e; font-size: .48rem; font-weight: 900; }\n\n.training-stats {\n  display: grid;")

print("combat client + teleport patch applied")
