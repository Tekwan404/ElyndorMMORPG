using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;
using Elyndor.Core.Combat.Targeting;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const decimal BaseRageFromDirectDamageTaken = 5;
    private readonly CombatParticipantDefinition _player;
    private readonly CombatParticipantDefinition? _companion;
    private readonly List<CombatParticipantDefinition> _enemies;
    private readonly Dictionary<Guid, CombatParticipantDefinition> _enemiesById;
    private readonly CombatRuntimeState _playerRuntime;
    private readonly CombatRuntimeState? _companionRuntime;
    private readonly Dictionary<Guid, CombatRuntimeState> _enemyRuntimes;
    private readonly Guid _primaryEnemyActorId;
    private Guid _selectedTargetActorId;

    // Transitional compatibility projection: existing class runtimes operate on the
    // currently selected enemy while the authoritative session state stores all enemies.
    private CombatParticipantDefinition _enemy => _enemiesById[_selectedTargetActorId];
    private CombatRuntimeState _enemyRuntime => _enemyRuntimes[_selectedTargetActorId];
    private CombatParticipantDefinition _primaryEnemy => _enemiesById[_primaryEnemyActorId];
    private readonly IReadOnlyDictionary<string, AbilityDefinition> _abilities;
    private readonly Dictionary<Guid, EnemyAiRuntime> _enemyAiRuntimes;
    private readonly Dictionary<Guid, ThreatTable> _enemyThreatTables;
    private readonly Dictionary<Guid, ForcedTargetState> _enemyForcedTargets;
    private readonly ResolvedTalentModifiers _playerTalents;
    private readonly ResolvedTalentModifiers _genericTalentModifiers;
    private readonly TalentRuntimeEngine _talentRuntimeEngine;
    private readonly IGameRandom _random;
    private readonly CombatSummonProfile? _summonProfile;
    private DateTimeOffset? _nextSummonAtUtc;
    private readonly HashSet<string> _processedCommandIds = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _deadActors = [];
    private readonly List<CombatEvent> _events = [];
    private readonly Dictionary<string, DateTimeOffset> _talentInternalCooldowns = new(StringComparer.Ordinal);
    private DateTimeOffset? _nextPlayerMainHandAutoAttackAtUtc;
    private DateTimeOffset? _nextPlayerOffHandAutoAttackAtUtc;
    private DateTimeOffset? _nextCompanionAutoAttackAtUtc;
    private readonly Dictionary<string, DateTimeOffset> _consumableCooldowns = new(StringComparer.Ordinal);
    private DateTimeOffset _lastPlayerResourceRegenAtUtc;
    private bool _playerAutoAttackEnabled;

    public CombatSession(
        Guid sessionId,
        CombatParticipantDefinition player,
        CombatParticipantDefinition enemy,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        MonsterAiProfile enemyAi,
        ResolvedTalentModifiers playerTalents,
        IGameRandom random,
        DateTimeOffset startedAtUtc,
        string contentVersion = "UNVERSIONED",
        string balanceVersion = "UNVERSIONED",
        IReadOnlyDictionary<string, DateTimeOffset>? initialPlayerCooldowns = null,
        CombatSummonProfile? summonProfile = null,
        CombatParticipantDefinition? companion = null)
        : this(
            sessionId,
            player,
            [enemy],
            abilities,
            enemyAi,
            playerTalents,
            random,
            startedAtUtc,
            contentVersion,
            balanceVersion,
            initialPlayerCooldowns,
            summonProfile,
            companion)
    {
    }

    public CombatSession(
        Guid sessionId,
        CombatParticipantDefinition player,
        IReadOnlyList<CombatParticipantDefinition> enemies,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        MonsterAiProfile primaryEnemyAi,
        ResolvedTalentModifiers playerTalents,
        IGameRandom random,
        DateTimeOffset startedAtUtc,
        string contentVersion = "UNVERSIONED",
        string balanceVersion = "UNVERSIONED",
        IReadOnlyDictionary<string, DateTimeOffset>? initialPlayerCooldowns = null,
        CombatSummonProfile? summonProfile = null,
        CombatParticipantDefinition? companion = null)
        : this(
            sessionId,
            player,
            enemies,
            abilities,
            CreateSharedEnemyAiProfiles(enemies, primaryEnemyAi),
            playerTalents,
            random,
            startedAtUtc,
            contentVersion,
            balanceVersion,
            initialPlayerCooldowns,
            summonProfile,
            companion)
    {
    }

    public CombatSession(
        Guid sessionId,
        CombatParticipantDefinition player,
        IReadOnlyList<CombatParticipantDefinition> enemies,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        IReadOnlyDictionary<Guid, MonsterAiProfile> enemyAiProfiles,
        ResolvedTalentModifiers playerTalents,
        IGameRandom random,
        DateTimeOffset startedAtUtc,
        string contentVersion = "UNVERSIONED",
        string balanceVersion = "UNVERSIONED",
        IReadOnlyDictionary<string, DateTimeOffset>? initialPlayerCooldowns = null,
        CombatSummonProfile? summonProfile = null,
        CombatParticipantDefinition? companion = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(enemyAiProfiles);
        ArgumentNullException.ThrowIfNull(playerTalents);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(balanceVersion);
        if (enemies.Count == 0)
            throw new ArgumentException("CombatSession requires at least one enemy.", nameof(enemies));
        if (player.Kind != CombatActorKind.Player
            || enemies.Any(enemy => enemy.Kind != CombatActorKind.Monster)
            || companion is not null && companion.Kind != CombatActorKind.Companion)
        {
            throw new ArgumentException(
                "CombatSession requires one player, optional companion, and monster enemies.");
        }
        if (enemies.Select(enemy => enemy.Actor.ActorId).Distinct().Count() != enemies.Count)
            throw new ArgumentException("Enemy actor identifiers must be unique.", nameof(enemies));
        if (enemies.Any(enemy => enemy.Actor.ActorId == player.Actor.ActorId))
            throw new ArgumentException("Player and enemy actor identifiers must be unique.", nameof(enemies));
        if (companion is not null
            && (companion.Actor.ActorId == player.Actor.ActorId
                || enemies.Any(enemy => enemy.Actor.ActorId == companion.Actor.ActorId)))
        {
            throw new ArgumentException("Companion actor identifier must be unique.", nameof(companion));
        }
        if (enemyAiProfiles.Count != enemies.Count
            || enemies.Any(enemy =>
                !enemyAiProfiles.TryGetValue(
                    enemy.Actor.ActorId,
                    out MonsterAiProfile? profile)
                || profile is null))
        {
            throw new ArgumentException(
                "Every enemy requires exactly one non-null AI profile keyed by actor id.",
                nameof(enemyAiProfiles));
        }

        ValidateAutoAttack(player.AutoAttack);
        if (player.OffHandAutoAttack is not null)
            ValidateAutoAttack(player.OffHandAutoAttack);
        foreach (CombatParticipantDefinition enemy in enemies)
            ValidateAutoAttack(enemy.AutoAttack);
        if (companion is not null)
            ValidateAutoAttack(companion.AutoAttack);
        if (player.ResourceRegenPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(player), "Resource regeneration cannot be negative.");

        SessionId = sessionId;
        ContentVersion = contentVersion;
        BalanceVersion = balanceVersion;
        _player = player;
        _companion = companion;
        _enemies = enemies.ToList();
        _enemiesById = _enemies.ToDictionary(enemy => enemy.Actor.ActorId);
        _primaryEnemyActorId = _enemies[0].Actor.ActorId;
        _selectedTargetActorId = _primaryEnemyActorId;
        _abilities = abilities;
        _playerTalents = playerTalents;
        _genericTalentModifiers = playerTalents with
        {
            EventHooks = playerTalents.EventHooks
                .Where(hook => !IsClassRuntimeTalent(hook.TalentId))
                .ToArray()
        };
        _random = random;
        _talentRuntimeEngine = new(new TalentRuntimeState(player.Actor.ActorId, random));
        _summonProfile = summonProfile;
        if (_summonProfile is not null)
        {
            if (_summonProfile.Interval <= TimeSpan.Zero
                || _summonProfile.Count <= 0
                || _summonProfile.MaxActive <= 0
                || !_enemies.Any(enemy => string.Equals(
                    enemy.DefinitionId,
                    _summonProfile.SourceDefinitionId,
                    StringComparison.Ordinal)))
            {
                throw new ArgumentException("Combat summon profile is invalid.", nameof(summonProfile));
            }

            ValidateAutoAttack(new AutoAttackProfile(
                _summonProfile.Monster.AutoAttackInterval,
                _summonProfile.Monster.AutoAttackBaseDamage,
                _summonProfile.Monster.AutoAttackAttackPowerCoefficient,
                0,
                _summonProfile.Monster.AutoAttackBaseDamageMin,
                _summonProfile.Monster.AutoAttackBaseDamageMax));
        }

        CombatActorState[] enemyActors = _enemies.Select(enemy => enemy.Actor).ToArray();
        CombatActorState[] playerKnownActors = companion is null
            ? enemyActors
            : enemyActors.Append(companion.Actor).ToArray();
        _playerRuntime = CreateRuntime(player.Actor, playerKnownActors);
        _companionRuntime = companion is null
            ? null
            : CreateRuntime(
                companion.Actor,
                new[] { player.Actor }.Concat(enemyActors));
        if (initialPlayerCooldowns is not null)
        {
            foreach ((string abilityId, DateTimeOffset readyAtUtc) in initialPlayerCooldowns)
            {
                if (readyAtUtc > startedAtUtc)
                    _playerRuntime.Cooldowns[abilityId] = readyAtUtc;
            }
        }
        _enemyRuntimes = _enemies.ToDictionary(
            enemy => enemy.Actor.ActorId,
            enemy => CreateRuntime(
                enemy.Actor,
                new[] { player.Actor }
                    .Concat(companion is null ? [] : new[] { companion.Actor })
                    .Concat(enemyActors.Where(actor => actor.ActorId != enemy.Actor.ActorId))));
        _enemyAiRuntimes = _enemies.ToDictionary(
            enemy => enemy.Actor.ActorId,
            enemy => new EnemyAiRuntime(
                enemyAiProfiles[enemy.Actor.ActorId],
                startedAtUtc + enemy.AutoAttack.Interval));
        _enemyThreatTables = _enemies.ToDictionary(
            enemy => enemy.Actor.ActorId,
            _ => new ThreatTable());
        _enemyForcedTargets = _enemies.ToDictionary(
            enemy => enemy.Actor.ActorId,
            _ => new ForcedTargetState());
        foreach (ThreatTable threat in _enemyThreatTables.Values)
        {
            threat.AddThreat(_player.Actor.ActorId, 1);
            if (_companion is not null)
                threat.AddThreat(_companion.Actor.ActorId, 1);
        }

        CurrentTimeUtc = startedAtUtc;
        _lastPlayerResourceRegenAtUtc = startedAtUtc;
        _nextSummonAtUtc = _summonProfile is null
            ? null
            : startedAtUtc + _summonProfile.Interval;
        Status = CombatSessionStatus.Active;
        _playerAutoAttackEnabled = player.CanAutoAttack;
        _nextPlayerMainHandAutoAttackAtUtc = player.CanAutoAttack ? startedAtUtc : null;
        _nextPlayerOffHandAutoAttackAtUtc = player.CanAutoAttack
            && player.OffHandAutoAttack is not null
                ? startedAtUtc + InitialOffHandDelay(player.OffHandAutoAttack)
                : null;
        _nextCompanionAutoAttackAtUtc = companion is not null && companion.CanAutoAttack
            ? startedAtUtc + companion.AutoAttack.Interval
            : null;
        ApplyGuardianStartingEffects(startedAtUtc);
        ApplyWarlordPassiveEffects(startedAtUtc);
        Append(new CombatEvent(
            CombatEventType.CombatStarted,
            startedAtUtc,
            player.Actor.ActorId,
            _primaryEnemy.DefinitionId,
            SourceActorId: player.Actor.ActorId,
            TargetActorId: _selectedTargetActorId));
    }

    public Guid SessionId { get; }
    public string ContentVersion { get; }
    public string BalanceVersion { get; }
    public long Sequence { get; private set; }
    public CombatSessionStatus Status { get; private set; }
    public DateTimeOffset CurrentTimeUtc { get; private set; }
    public Guid PlayerActorId => _player.Actor.ActorId;
    public Guid? CompanionActorId => _companion?.Actor.ActorId;
    public Guid EnemyActorId => _selectedTargetActorId;
    public Guid SelectedTargetActorId => _selectedTargetActorId;
    public IReadOnlyList<Guid> EnemyActorIds => _enemies.Select(enemy => enemy.Actor.ActorId).ToArray();

    public DateTimeOffset? NextDueAtUtc
    {
        get
        {
            if (Status != CombatSessionStatus.Active) return null;
            DateTimeOffset? next = Min(
                _nextPlayerMainHandAutoAttackAtUtc,
                _nextPlayerOffHandAutoAttackAtUtc);
            next = Min(next, _playerRuntime.ActiveCast?.ResolvesAtUtc);
            next = Min(next, NextEffectDue(_player.Actor));
            next = Min(next, _nextCompanionAutoAttackAtUtc);
            next = Min(next, _companionRuntime?.ActiveCast?.ResolvesAtUtc);
            next = Min(next, _companion is null ? null : NextEffectDue(_companion.Actor));
            next = Min(next, _nextSummonAtUtc);
            foreach (CombatParticipantDefinition enemy in _enemies)
            {
                Guid enemyActorId = enemy.Actor.ActorId;
                next = Min(next, _enemyRuntimes[enemyActorId].ActiveCast?.ResolvesAtUtc);
                next = Min(next, _enemyAiRuntimes[enemyActorId].NextActionAtUtc);
                next = Min(next, NextEffectDue(enemy.Actor));
            }
            return next;
        }
    }

    public bool HasProcessedCommand(string commandId) =>
        !string.IsNullOrWhiteSpace(commandId) && _processedCommandIds.Contains(commandId);

    public string? ValidateConsumableUse(
        DateTimeOffset now,
        IReadOnlyList<ResolvedConsumableAction> actions,
        string cooldownCategoryId,
        TimeSpan cooldown)
    {
        if (Status != CombatSessionStatus.Active) return CombatErrorCodes.Ended;
        if (_player.Actor.IsDead) return CombatErrorCodes.ActorDead;
        if (actions is null
            || actions.Count == 0
            || string.IsNullOrWhiteSpace(cooldownCategoryId)
            || cooldown < TimeSpan.Zero)
        {
            return CombatErrorCodes.CommandRejected;
        }
        if (_consumableCooldowns.TryGetValue(cooldownCategoryId, out DateTimeOffset readyAt)
            && readyAt > now)
        {
            return CombatErrorCodes.ConsumableOnCooldown;
        }

        bool changesState = false;
        foreach (ResolvedConsumableAction action in actions)
        {
            switch (action.Type)
            {
                case ConsumableActionType.RestoreHp:
                    if (action.Amount <= 0)
                        return CombatErrorCodes.CommandRejected;
                    changesState |= _player.Actor.CurrentHp < _player.Actor.MaxHp;
                    break;
                case ConsumableActionType.RestoreResource:
                    if (action.Amount <= 0
                        || string.IsNullOrWhiteSpace(action.ResourceType)
                        || !string.Equals(
                            action.ResourceType,
                            _player.ResourceType,
                            StringComparison.Ordinal))
                    {
                        return CombatErrorCodes.CommandRejected;
                    }
                    changesState |= _player.Actor.CurrentResource < _player.Actor.MaxResource;
                    break;
                case ConsumableActionType.ApplyEffect:
                    if (action.Effect is null)
                        return CombatErrorCodes.CommandRejected;
                    changesState = true;
                    break;
                case ConsumableActionType.RemoveEffect:
                    bool byId = !string.IsNullOrWhiteSpace(action.RemoveEffectId);
                    bool byCategory = !string.IsNullOrWhiteSpace(action.DispelCategory);
                    if (byId == byCategory)
                        return CombatErrorCodes.CommandRejected;
                    changesState |= _player.Actor.ActiveEffects.Any(effect =>
                        byId
                            ? string.Equals(
                                effect.Definition.Id,
                                action.RemoveEffectId,
                                StringComparison.Ordinal)
                            : string.Equals(
                                effect.Definition.DispelCategory,
                                action.DispelCategory,
                                StringComparison.Ordinal));
                    break;
                default:
                    return CombatErrorCodes.CommandRejected;
            }
        }

        return changesState ? null : CombatErrorCodes.ConsumableNotNeeded;
    }

    public CombatCommandResult Handle(CombatCommand command, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command);
        long before = Sequence;
        AdvanceCore(now);
        if (Status != CombatSessionStatus.Active)
            return Result(false, CombatErrorCodes.Ended, before);
        if (string.IsNullOrWhiteSpace(command.CommandId)
            || !_processedCommandIds.Add(command.CommandId))
            return Result(false, CombatErrorCodes.DuplicateCommand, before);

        return command switch
        {
            UseAbilityCommand useAbility => UseAbility(useAbility, now, before),
            UseConsumableCommand consumable => UseConsumable(consumable, now, before),
            StartAutoAttackCommand => StartAutoAttack(now, before),
            StopAutoAttackCommand => StopAutoAttack(now, before),
            SelectTargetCommand selectTarget => SelectTarget(selectTarget, now, before),
            _ => Result(false, CombatErrorCodes.CommandRejected, before)
        };
    }

    public CombatCommandResult AdvanceTo(DateTimeOffset now)
    {
        long before = Sequence;
        AdvanceCore(now);
        return Result(true, null, before);
    }

    public IReadOnlyList<CombatEvent> GetEventsAfter(long sequence) =>
        _events.Where(item => item.Sequence > sequence).ToArray();

    public CombatSessionSnapshot Snapshot()
    {
        CombatActorSnapshot[] enemies = _enemies
            .Select(enemy => ActorSnapshot(
                enemy,
                _enemyRuntimes[enemy.Actor.ActorId],
                Status == CombatSessionStatus.Active && !enemy.Actor.IsDead))
            .ToArray();
        CombatActorSnapshot selected = enemies.Single(enemy =>
            enemy.ActorId == _selectedTargetActorId);
        return new CombatSessionSnapshot(
            SessionId,
            Sequence,
            Status,
            CurrentTimeUtc,
            ActorSnapshot(_player, _playerRuntime, _playerAutoAttackEnabled),
            selected,
            ContentVersion,
            BalanceVersion,
            enemies,
            _selectedTargetActorId,
            _companion is null || _companionRuntime is null
                ? null
                : ActorSnapshot(
                    _companion,
                    _companionRuntime,
                    Status == CombatSessionStatus.Active && !_companion.Actor.IsDead));
    }

    public CombatCommandResult Cancel(DateTimeOffset now)
    {
        long before = Sequence;
        if (Status == CombatSessionStatus.Active)
        {
            AdvanceCore(now);
            if (Status == CombatSessionStatus.Active)
            {
                Status = CombatSessionStatus.Cancelled;
                _nextPlayerMainHandAutoAttackAtUtc = null;
                _nextPlayerOffHandAutoAttackAtUtc = null;
                _nextCompanionAutoAttackAtUtc = null;
                StopEnemyAi(MonsterAiState.Resetting);
                Append(new CombatEvent(
                    CombatEventType.CombatEnded,
                    CurrentTimeUtc,
                    _player.Actor.ActorId,
                    Status.ToString(),
                    SourceActorId: _player.Actor.ActorId));
            }
        }

        return Result(true, null, before);
    }

    private CombatCommandResult UseAbility(
        UseAbilityCommand command,
        DateTimeOffset now,
        long before)
    {
        if (!IsPlayerAbilityKnown(command.AbilityId, now)
            || !_abilities.TryGetValue(command.AbilityId, out AbilityDefinition? baseAbility))
            return Result(false, CombatErrorCodes.AbilityNotKnown, before);

        SyncBerserkerConditionalEffects(now);
        SyncArcherConditionalEffects(now);
        AbilityDefinition ability = ResolveArcherAbility(
            ResolveMageAbility(
                ResolvePyromancerAbility(
                    ResolveWarlordAbility(
                        ResolvePlayerAbility(baseAbility, now),
                        now),
                    now),
                now),
            now);
        Guid[] targetActorIds = ResolvePlayerAbilityTargetIds(ability);
        if (targetActorIds.Length == 0)
            return Result(false, CombatErrorCodes.InvalidTarget, before);
        Guid primaryTargetActorId = targetActorIds[0];
        IReadOnlyDictionary<Guid, AbilityTargetModifier> targetModifiers =
            ResolvePlayerAbilityTargetModifiers(ability, targetActorIds, now);

        AbilityExecutionResult execution = AbilityEngine.Execute(
            _playerRuntime,
            ability,
            new AbilityIntent(
                command.CommandId,
                command.AbilityId,
                primaryTargetActorId,
                targetActorIds,
                targetModifiers),
            now,
            _random);
        if (!execution.Succeeded)
            return Result(false, MapAbilityError(execution.ErrorCode), before);

        ApplyKernelEvents(
            execution.Events,
            _player.Actor.ActorId,
            primaryTargetActorId,
            command.AbilityId);
        OnPyromancerAbilityStarted(ability, now);
        OnMageAbilityStarted(ability, now);
        OnArcherAbilityStarted(ability, now);
        if (ability.Type != AbilityType.Casted)
        {
            OnPlayerAbilitySucceeded(
                ability,
                execution,
                now);
            OnPyromancerAbilityResolved(
                ability,
                execution,
                now);
            OnMageAbilityResolved(
                ability,
                execution,
                now);
            OnArcherAbilityResolved(
                ability,
                execution,
                now);
        }
        Append(new CombatEvent(
            CombatEventType.AbilityUsed,
            now,
            _player.Actor.ActorId,
            command.AbilityId,
            SourceActorId: _player.Actor.ActorId,
            TargetActorId: primaryTargetActorId));
        return Result(true, null, before);
    }

    private CombatCommandResult UseConsumable(
        UseConsumableCommand command,
        DateTimeOffset now,
        long before)
    {
        string? validationError = ValidateConsumableUse(
            now,
            command.Actions,
            command.CooldownCategoryId,
            command.Cooldown);
        if (validationError is not null)
            return Result(false, validationError, before);

        foreach (ResolvedConsumableAction action in command.Actions)
        {
            switch (action.Type)
            {
                case ConsumableActionType.RestoreHp:
                {
                    decimal previousHp = _player.Actor.CurrentHp;
                    _player.Actor.ApplyHealing(action.Amount);
                    decimal healed = _player.Actor.CurrentHp - previousHp;
                    if (healed > 0)
                    {
                        Append(new CombatEvent(
                            CombatEventType.HealingApplied,
                            now,
                            _player.Actor.ActorId,
                            command.ItemDefinitionId,
                            healed,
                            SourceActorId: _player.Actor.ActorId,
                            TargetActorId: _player.Actor.ActorId));
                    }
                    break;
                }
                case ConsumableActionType.RestoreResource:
                    AddResource(
                        _player.Actor,
                        action.Amount,
                        now,
                        command.ItemDefinitionId);
                    break;
                case ConsumableActionType.ApplyEffect:
                    ApplyKernelEvents(
                        EffectEngine.Apply(
                            _player.Actor,
                            _player.Actor.ActorId,
                            action.Effect!,
                            now),
                        _player.Actor.ActorId,
                        _player.Actor.ActorId,
                        command.ItemDefinitionId);
                    break;
                case ConsumableActionType.RemoveEffect:
                    IReadOnlyList<CombatEvent> removed = action.RemoveEffectId is not null
                        ? EffectEngine.Remove(
                            _player.Actor,
                            action.RemoveEffectId,
                            now)
                        : EffectEngine.Dispel(
                            _player.Actor,
                            action.DispelCategory!,
                            now);
                    ApplyKernelEvents(
                        removed,
                        _player.Actor.ActorId,
                        _player.Actor.ActorId,
                        command.ItemDefinitionId);
                    break;
            }
        }

        _consumableCooldowns[command.CooldownCategoryId] = now + command.Cooldown;
        Append(new CombatEvent(
            CombatEventType.ConsumableUsed,
            now,
            _player.Actor.ActorId,
            command.ItemDefinitionId,
            SourceActorId: _player.Actor.ActorId,
            TargetActorId: _player.Actor.ActorId));
        SyncBerserkerConditionalEffects(now);
        SyncArcherConditionalEffects(now);
        return Result(true, null, before);
    }

    private Dictionary<Guid, AbilityTargetModifier>
        ResolvePlayerAbilityTargetModifiers(
            AbilityDefinition ability,
            IReadOnlyList<Guid> targetActorIds,
            DateTimeOffset now)
    {
        Dictionary<Guid, AbilityTargetModifier> modifiers = [];
        foreach (Guid targetActorId in targetActorIds)
        {
            if (!_enemiesById.TryGetValue(
                    targetActorId,
                    out CombatParticipantDefinition? target))
            {
                continue;
            }

            AbilityTargetModifier modifier = new();
            modifier = ResolveBerserkerTargetAbilityModifier(
                ability,
                target.Actor,
                modifier);
            modifier = ResolvePyromancerTargetAbilityModifier(
                ability,
                target.Actor,
                modifier,
                now);
            modifier = ResolveMageTargetAbilityModifier(
                ability,
                target.Actor,
                modifier,
                now);
            modifier = ResolveArcherTargetAbilityModifier(
                ability,
                target.Actor,
                modifier,
                now);
            if (modifier != new AbilityTargetModifier())
                modifiers[targetActorId] = modifier;
        }

        return modifiers;
    }

    private Guid[] ResolvePlayerAbilityTargetIds(
        AbilityDefinition ability)
    {
        if (ability.TargetType == AbilityTargetType.Self)
            return [_player.Actor.ActorId];

        if (ability.TargetType == AbilityTargetType.ActiveCompanion)
            return _companion is not null && !_companion.Actor.IsDead
                ? [_companion.Actor.ActorId]
                : [];

        if (ability.TargetType == AbilityTargetType.SelfAndPartyMembersInCombat)
        {
            Guid[] partyMembers = _companion is not null && !_companion.Actor.IsDead
                ? [_player.Actor.ActorId, _companion.Actor.ActorId]
                : [_player.Actor.ActorId];
            return ability.TargetCount > 0
                ? partyMembers.Take(ability.TargetCount).ToArray()
                : partyMembers;
        }

        if (ability.TargetType == AbilityTargetType.SingleEnemy)
        {
            return _enemiesById.TryGetValue(
                    _selectedTargetActorId,
                    out CombatParticipantDefinition? selected)
                && !selected.Actor.IsDead
                    ? [_selectedTargetActorId]
                    : [];
        }

        if (ability.TargetType is not (AbilityTargetType.AllEnemiesInCombat
            or AbilityTargetType.NEnemiesInCombat))
        {
            return [];
        }

        if (ability.TargetSelectorProfile != AbilityTargetSelectorProfile.EncounterOrder)
            return [];

        int targetLimit = ability.TargetType == AbilityTargetType.NEnemiesInCombat
            ? ability.TargetCount
            : ability.TargetCount > 0
                ? ability.TargetCount
                : int.MaxValue;
        if (targetLimit <= 0)
            return [];

        return _enemies
            .Where(enemy => !enemy.Actor.IsDead)
            .Take(targetLimit)
            .Select(enemy => enemy.Actor.ActorId)
            .ToArray();
    }

    private CombatCommandResult SelectTarget(
        SelectTargetCommand command,
        DateTimeOffset now,
        long before)
    {
        if (!_enemiesById.TryGetValue(
                command.TargetActorId,
                out CombatParticipantDefinition? target)
            || target.Actor.IsDead)
        {
            return Result(false, CombatErrorCodes.InvalidTarget, before);
        }

        if (_selectedTargetActorId == command.TargetActorId)
            return Result(true, null, before);

        _selectedTargetActorId = command.TargetActorId;
        Append(new CombatEvent(
            CombatEventType.TargetChanged,
            now,
            _player.Actor.ActorId,
            target.DefinitionId,
            SourceActorId: _player.Actor.ActorId,
            TargetActorId: target.Actor.ActorId));
        SyncBerserkerConditionalEffects(now);
        return Result(true, null, before);
    }

    private CombatCommandResult StartAutoAttack(DateTimeOffset now, long before)
    {
        if (!_player.CanAutoAttack)
        {
            return Result(false, CombatErrorCodes.AutoAttackUnavailable, before);
        }

        if (!_playerAutoAttackEnabled)
        {
            _playerAutoAttackEnabled = true;
            _nextPlayerMainHandAutoAttackAtUtc = now;
            _nextPlayerOffHandAutoAttackAtUtc = _player.OffHandAutoAttack is null
                ? null
                : now + InitialOffHandDelay(_player.OffHandAutoAttack);
            CombatEvent autoAttackStarted = new(
                CombatEventType.AutoAttackStarted,
                now,
                _player.Actor.ActorId,
                SourceActorId: _player.Actor.ActorId,
                TargetActorId: _enemy.Actor.ActorId);
            Append(autoAttackStarted);
            TriggerTalent(
                TalentModifierKeys.OnAutoAttack,
                now,
                ToRuntimeEvent(autoAttackStarted, CombatRuntimeEventKind.AutoAttackStarted));
            AdvanceCore(now);
        }

        return Result(true, null, before);
    }

    private CombatCommandResult StopAutoAttack(DateTimeOffset now, long before)
    {
        if (_playerAutoAttackEnabled)
        {
            _playerAutoAttackEnabled = false;
            _nextPlayerMainHandAutoAttackAtUtc = null;
            _nextPlayerOffHandAutoAttackAtUtc = null;
            Append(new CombatEvent(
                CombatEventType.AutoAttackStopped,
                now,
                _player.Actor.ActorId,
                SourceActorId: _player.Actor.ActorId,
                TargetActorId: _enemy.Actor.ActorId));
        }

        return Result(true, null, before);
    }

    private void AdvanceCore(DateTimeOffset now)
    {
        if (now < CurrentTimeUtc)
            throw new ArgumentOutOfRangeException(
                nameof(now),
                "Combat time cannot move backwards.");
        if (Status != CombatSessionStatus.Active)
        {
            CurrentTimeUtc = now;
            return;
        }

        while (Status == CombatSessionStatus.Active
               && NextDueAtUtc is { } due
               && due <= now)
        {
            ApplyPlayerResourceRegen(due);
            CurrentTimeUtc = due;
            ProcessEffects(due);
            if (Status != CombatSessionStatus.Active) break;

            SyncBerserkerConditionalEffects(due);
            SyncArcherConditionalEffects(due);
            CompleteReadyCast(
                _playerRuntime,
                _player.Actor.ActorId,
                due);
            if (_companionRuntime is not null && _companion is not null)
            {
                CompleteReadyCast(
                    _companionRuntime,
                    _companion.Actor.ActorId,
                    due);
            }
            foreach (CombatParticipantDefinition enemy in _enemies)
            {
                CompleteReadyCast(
                    _enemyRuntimes[enemy.Actor.ActorId],
                    enemy.Actor.ActorId,
                    due);
                if (Status != CombatSessionStatus.Active) break;
            }

            if (Status == CombatSessionStatus.Active)
                SyncArcherConditionalEffects(due);

            if (Status == CombatSessionStatus.Active
                && _nextPlayerMainHandAutoAttackAtUtc <= due)
            {
                ResolveReadyPlayerAutoAttack(
                    _player.AutoAttack,
                    due,
                    ref _nextPlayerMainHandAutoAttackAtUtc);
            }

            if (Status == CombatSessionStatus.Active
                && _player.OffHandAutoAttack is not null
                && _nextPlayerOffHandAutoAttackAtUtc <= due)
            {
                ResolveReadyPlayerAutoAttack(
                    _player.OffHandAutoAttack,
                    due,
                    ref _nextPlayerOffHandAutoAttackAtUtc);
            }

            if (Status == CombatSessionStatus.Active
                && _companion is not null
                && !_companion.Actor.IsDead
                && _nextCompanionAutoAttackAtUtc <= due)
            {
                CombatParticipantDefinition? companionTarget = _enemiesById
                    .GetValueOrDefault(_selectedTargetActorId);
                if (companionTarget is null || companionTarget.Actor.IsDead)
                    companionTarget = _enemies.FirstOrDefault(enemy => !enemy.Actor.IsDead);

                if (companionTarget is not null)
                    ResolveAutoAttack(_companion, companionTarget, due);

                _nextCompanionAutoAttackAtUtc =
                    Status == CombatSessionStatus.Active && !_companion.Actor.IsDead
                        ? NextCompanionActionAfter(_companion, due)
                        : null;
            }

            if (Status == CombatSessionStatus.Active
                && _nextSummonAtUtc <= due)
            {
                ResolveSummon(due);
            }

            foreach (CombatParticipantDefinition enemy in _enemies)
            {
                if (Status != CombatSessionStatus.Active)
                    break;

                EnemyAiRuntime aiRuntime = _enemyAiRuntimes[enemy.Actor.ActorId];
                if (aiRuntime.NextActionAtUtc <= due)
                    ResolveEnemyAction(enemy, due);
            }
        }

        ApplyPlayerResourceRegen(now);
        CurrentTimeUtc = now;
        if (Status == CombatSessionStatus.Active)
        {
            ProcessEffects(now);
            if (Status == CombatSessionStatus.Active)
            {
                SyncBerserkerConditionalEffects(now);
                SyncArcherConditionalEffects(now);
            }
        }
    }

    private void ResolveReadyPlayerAutoAttack(
        AutoAttackProfile profile,
        DateTimeOffset due,
        ref DateTimeOffset? nextAtUtc)
    {
        if (_playerRuntime.ActiveCast is null)
        {
            ResolveAutoAttack(_player, _enemy, due, profile);
            nextAtUtc = Status == CombatSessionStatus.Active && _playerAutoAttackEnabled
                ? due + EffectivePlayerAutoAttackInterval(profile, due)
                : null;
            return;
        }

        nextAtUtc = _playerRuntime.ActiveCast.ResolvesAtUtc;
    }

    private void ResolveSummon(DateTimeOffset now)
    {
        if (_summonProfile is null)
        {
            _nextSummonAtUtc = null;
            return;
        }

        CombatParticipantDefinition? source = _enemies.FirstOrDefault(enemy =>
            string.Equals(
                enemy.DefinitionId,
                _summonProfile.SourceDefinitionId,
                StringComparison.Ordinal)
            && !enemy.Actor.IsDead);
        if (source is null)
        {
            _nextSummonAtUtc = null;
            return;
        }

        int activeSummons = _enemies.Count(enemy =>
            string.Equals(
                enemy.DefinitionId,
                _summonProfile.Monster.Id,
                StringComparison.Ordinal)
            && !enemy.Actor.IsDead);
        int available = Math.Max(0, _summonProfile.MaxActive - activeSummons);
        int toSummon = Math.Min(_summonProfile.Count, available);

        for (var index = 0; index < toSummon; index++)
        {
            CombatParticipantDefinition summoned = CreateSummonedParticipant(
                _summonProfile.Monster);
            CombatActorState[] existingEnemyActors =
                _enemies.Select(enemy => enemy.Actor).ToArray();

            _playerRuntime.AddActor(summoned.Actor);
            foreach (CombatRuntimeState runtime in _enemyRuntimes.Values)
                runtime.AddActor(summoned.Actor);

            _enemies.Add(summoned);
            _enemiesById.Add(summoned.Actor.ActorId, summoned);
            _enemyRuntimes.Add(
                summoned.Actor.ActorId,
                CreateRuntime(
                    summoned.Actor,
                    new[] { _player.Actor }.Concat(existingEnemyActors)));
            _enemyAiRuntimes.Add(
                summoned.Actor.ActorId,
                new EnemyAiRuntime(
                    _summonProfile.AiProfile,
                    now + summoned.AutoAttack.Interval));
            ThreatTable summonedThreat = new();
            summonedThreat.AddThreat(_player.Actor.ActorId, 1);
            if (_companion is not null)
                summonedThreat.AddThreat(_companion.Actor.ActorId, 1);
            _enemyThreatTables.Add(summoned.Actor.ActorId, summonedThreat);
            _enemyForcedTargets.Add(summoned.Actor.ActorId, new ForcedTargetState());

            Append(new CombatEvent(
                CombatEventType.ActorSummoned,
                now,
                summoned.Actor.ActorId,
                summoned.DefinitionId,
                SourceActorId: source.Actor.ActorId,
                TargetActorId: summoned.Actor.ActorId));
        }

        _nextSummonAtUtc = Status == CombatSessionStatus.Active
            ? now + _summonProfile.Interval
            : null;
    }

    private static CombatParticipantDefinition CreateSummonedParticipant(
        MonsterDefinition monster)
    {
        CombatActorState actor = new(
            Guid.NewGuid(),
            monster.MaxHp,
            monster.MaxHp,
            0,
            0,
            monster.Stats);

        return new CombatParticipantDefinition(
            actor,
            CombatActorKind.Monster,
            monster.Id,
            monster.DisplayName ?? monster.Name,
            "NONE",
            new AutoAttackProfile(
                monster.AutoAttackInterval,
                monster.AutoAttackBaseDamage,
                monster.AutoAttackAttackPowerCoefficient,
                0,
                monster.AutoAttackBaseDamageMin,
                monster.AutoAttackBaseDamageMax),
            new HashSet<string>(monster.AbilityIds, StringComparer.Ordinal));
    }

    private void ResolveEnemyAction(
        CombatParticipantDefinition enemy,
        DateTimeOffset now)
    {
        Guid enemyActorId = enemy.Actor.ActorId;
        EnemyAiRuntime aiRuntime = _enemyAiRuntimes[enemyActorId];
        CombatRuntimeState runtime = _enemyRuntimes[enemyActorId];
        if (enemy.Actor.IsDead)
        {
            aiRuntime.State = MonsterAiState.Dead;
            aiRuntime.NextActionAtUtc = null;
            return;
        }

        if (runtime.ActiveCast is { } activeCast)
        {
            aiRuntime.NextActionAtUtc = activeCast.ResolvesAtUtc;
            return;
        }

        aiRuntime.State = MonsterAiState.InCombat;
        SyncBerserkerConditionalEffects(now);
        SyncMageConditionalEffects(now);
        SyncArcherConditionalEffects(now);
        foreach (string abilityId in aiRuntime.Profile.PriorityAbilityIds)
        {
            if (!enemy.KnownAbilityIds.Contains(abilityId)
                || !_abilities.TryGetValue(abilityId, out AbilityDefinition? ability))
            {
                continue;
            }

            Guid[] targetIds = ResolveEnemyAbilityTargetIds(enemy, ability, now);
            if (targetIds.Length == 0)
                continue;

            Dictionary<Guid, AbilityTargetModifier>? targetModifiers =
                ResolveEnemyAbilityTargetModifiers(ability, targetIds);

            string commandId = $"ai:{enemyActorId:N}:{Sequence + 1}:{abilityId}";
            AbilityExecutionResult execution = AbilityEngine.Execute(
                runtime,
                ability,
                new AbilityIntent(
                    commandId,
                    abilityId,
                    targetIds[0],
                    targetIds,
                    targetModifiers),
                now,
                _random);
            if (!execution.Succeeded)
                continue;

            ApplyKernelEvents(
                execution.Events,
                enemyActorId,
                targetIds[0],
                abilityId);
            Append(new CombatEvent(
                CombatEventType.AbilityUsed,
                now,
                enemyActorId,
                abilityId,
                SourceActorId: enemyActorId,
                TargetActorId: targetIds[0]));
            SyncArcherConditionalEffects(now);
            if (Status != CombatSessionStatus.Active || enemy.Actor.IsDead)
            {
                aiRuntime.NextActionAtUtc = null;
                return;
            }

            aiRuntime.NextActionAtUtc = runtime.ActiveCast?.ResolvesAtUtc
                ?? NextEnemyActionAfter(enemy, now);
            return;
        }

        if (EffectEngine.HasControl(enemy.Actor, EffectKind.Stun, now))
        {
            aiRuntime.NextActionAtUtc =
                NextEffectDue(enemy.Actor) ?? NextEnemyActionAfter(enemy, now);
            return;
        }

        if (enemy.CanAutoAttack)
        {
            CombatParticipantDefinition target = SelectEnemyPartyTarget(enemy.Actor.ActorId, now);
            ResolveAutoAttack(enemy, target, now);
        }
        aiRuntime.NextActionAtUtc =
            Status == CombatSessionStatus.Active && !enemy.Actor.IsDead
                ? NextEnemyActionAfter(enemy, now)
                : null;
    }

    private Guid[] ResolveEnemyAbilityTargetIds(
        CombatParticipantDefinition enemy,
        AbilityDefinition ability,
        DateTimeOffset now)
    {
        if (_player.Actor.IsDead)
            return [];

        Guid[] hostileActors = _companion is not null && !_companion.Actor.IsDead
            ? [_player.Actor.ActorId, _companion.Actor.ActorId]
            : [_player.Actor.ActorId];

        return ability.TargetType switch
        {
            AbilityTargetType.Self => [enemy.Actor.ActorId],
            AbilityTargetType.SingleEnemy => [SelectEnemyPartyTarget(enemy.Actor.ActorId, now).Actor.ActorId],
            AbilityTargetType.AllEnemiesInCombat => hostileActors,
            AbilityTargetType.NEnemiesInCombat when ability.TargetCount > 0 =>
                hostileActors.Take(ability.TargetCount).ToArray(),
            AbilityTargetType.SingleAlly when ability.AllowSelfTarget =>
                [enemy.Actor.ActorId],
            _ => []
        };
    }

    private CombatParticipantDefinition SelectEnemyPartyTarget(
        Guid enemyActorId,
        DateTimeOffset now)
    {
        IReadOnlyList<CombatParticipantDefinition> candidates = _companion is null
            ? [_player]
            : [_player, _companion];
        Guid selected = TargetSelectionPolicy.SelectForcedOrThreatTarget(
            _enemyForcedTargets[enemyActorId],
            _enemyThreatTables[enemyActorId],
            candidates
                .Where(candidate => !candidate.Actor.IsDead)
                .Select(candidate => new CombatActor(
                    candidate.Actor.ActorId,
                    CombatActorSide.Friendly))
                .ToArray(),
            now) ?? _player.Actor.ActorId;
        return candidates.First(candidate => candidate.Actor.ActorId == selected);
    }

    private Dictionary<Guid, AbilityTargetModifier>?
        ResolveEnemyAbilityTargetModifiers(
            AbilityDefinition ability,
            Guid[] targetActorIds)
    {
        if (!IsArcher
            || targetActorIds.Length <= 1
            || ability.TargetType is not (
                AbilityTargetType.AllEnemiesInCombat
                or AbilityTargetType.NEnemiesInCombat))
        {
            return null;
        }

        CombatParticipantDefinition? companion = _companion;
        if (companion is null
            || companion.Actor.IsDead
            || !IsPhysicalCompanion
            || !targetActorIds.Contains(companion.Actor.ActorId))
        {
            return null;
        }

        if (!TryGetArcherHook(
                "B-5-2",
                out ResolvedTalentEventHook hardened))
        {
            return null;
        }

        return new Dictionary<Guid, AbilityTargetModifier>
        {
            [companion.Actor.ActorId] = new(
                DamageMultiplier: Math.Max(
                    0,
                    1 - hardened.Value / 100m))
        };
    }

    private static DateTimeOffset NextCompanionActionAfter(
        CombatParticipantDefinition companion,
        DateTimeOffset now)
    {
        decimal multiplier = EffectEngine.CalculateStat(
            companion.Actor,
            EffectStat.AttackSpeed,
            1,
            now);
        double seconds = companion.AutoAttack.Interval.TotalSeconds
            / Math.Max(0.1, (double)multiplier);
        return now + TimeSpan.FromSeconds(Math.Max(0.05, seconds));
    }

    private static DateTimeOffset NextEnemyActionAfter(
        CombatParticipantDefinition enemy,
        DateTimeOffset now)
    {
        decimal multiplier = EffectEngine.CalculateStat(
            enemy.Actor,
            EffectStat.AttackSpeed,
            1,
            now);
        double seconds = enemy.AutoAttack.Interval.TotalSeconds
            / Math.Max(0.1, (double)multiplier);
        return now + TimeSpan.FromSeconds(Math.Max(0.05, seconds));
    }


    private void ResolveAutoAttack(
        CombatParticipantDefinition source,
        CombatParticipantDefinition target,
        DateTimeOffset now,
        AutoAttackProfile? playerProfile = null)
    {
        if (source.Kind == CombatActorKind.Player)
        {
            ResolvePlayerAutoAttack(target, playerProfile ?? source.AutoAttack, now);
            return;
        }

        SyncBerserkerConditionalEffects(now);
        decimal attackPower = EffectEngine.CalculateStat(
            source.Actor,
            EffectStat.AttackPower,
            source.Actor.Stats.AttackPower,
            now);
        decimal spellPower = EffectEngine.CalculateStat(
            source.Actor,
            EffectStat.SpellPower,
            source.Actor.Stats.SpellPower,
            now);
        decimal baseDamage = AutoAttackDamageRoller.RollBaseDamage(
                source.AutoAttack,
                _random)
            + attackPower * source.AutoAttack.AttackPowerCoefficient
            + spellPower * source.AutoAttack.SpellPowerCoefficient;
        decimal archerCompanionMultiplier = source.Kind == CombatActorKind.Companion
            ? ResolveArcherCompanionDamageMultiplier(target.Actor, now)
            : 1;
        DamageResult damage = DamagePipeline.Resolve(
            new DamageRequest(
                source.Actor,
                target.Actor,
                baseDamage,
                source.AutoAttack.DamageType,
                DamageMultiplier: archerCompanionMultiplier),
            _random,
            now);
        ApplyKernelEvents(
            damage.Events,
            source.Actor.ActorId,
            target.Actor.ActorId,
            "AUTO_ATTACK");
        if (damage.Avoidance == DamageAvoidance.None
            && damage.HpDamage > 0
            && source.AutoAttack.ResourceOnHit > 0)
        {
            AddResource(
                source.Actor,
                source.AutoAttack.ResourceOnHit,
                now,
                "AUTO_ATTACK");
        }
    }

    private void CompleteReadyCast(
        CombatRuntimeState runtime,
        Guid sourceActorId,
        DateTimeOffset now)
    {
        if (runtime.ActiveCast?.ResolvesAtUtc > now) return;
        ActiveCast? cast = runtime.ActiveCast;
        if (cast is null) return;
        if (runtime != _playerRuntime)
            SyncMageConditionalEffects(now);
        AbilityExecutionResult completion =
            AbilityEngine.CompleteCast(runtime, now, _random);
        if (!completion.Succeeded) return;

        Guid primaryTargetActorId = cast.TargetIds is { Count: > 0 }
            ? cast.TargetIds[0]
            : cast.TargetId;
        ApplyKernelEvents(
            completion.Events,
            sourceActorId,
            primaryTargetActorId,
            cast.Ability.Id);
        if (runtime == _playerRuntime)
        {
            OnPlayerAbilitySucceeded(
                cast.Ability,
                completion,
                now);
            OnPyromancerAbilityResolved(
                cast.Ability,
                completion,
                now);
            OnMageAbilityResolved(
                cast.Ability,
                completion,
                now);
            OnArcherAbilityResolved(
                cast.Ability,
                completion,
                now);
        }
    }

    private void ProcessEffects(DateTimeOffset now)
    {
        ApplyKernelEvents(
            EffectEngine.Process(
                _player.Actor,
                now,
                (effect, tickAt) => ResolvePeriodicEffectDamage(effect, _player.Actor, tickAt)),
            _primaryEnemy.Actor.ActorId,
            _player.Actor.ActorId,
            null);
        if (Status != CombatSessionStatus.Active) return;

        if (_companion is not null)
        {
            ApplyKernelEvents(
                EffectEngine.Process(
                    _companion.Actor,
                    now,
                    (effect, tickAt) => ResolvePeriodicEffectDamage(effect, _companion.Actor, tickAt)),
                _companion.Actor.ActorId,
                _companion.Actor.ActorId,
                null);
            if (Status != CombatSessionStatus.Active) return;
        }

        foreach (CombatParticipantDefinition enemy in _enemies)
        {
            ApplyKernelEvents(
                EffectEngine.Process(
                    enemy.Actor,
                    now,
                    (effect, tickAt) => ResolvePeriodicEffectDamage(effect, enemy.Actor, tickAt)),
                _player.Actor.ActorId,
                enemy.Actor.ActorId,
                null);
            if (Status != CombatSessionStatus.Active) return;
        }
    }

    private IReadOnlyList<CombatEvent> ResolvePeriodicEffectDamage(
        ActiveEffect effect,
        CombatActorState target,
        DateTimeOffset tickAt)
    {
        CombatActorState? source = effect.SourceId == _player.Actor.ActorId
            ? _player.Actor
            : _companion is not null && effect.SourceId == _companion.Actor.ActorId
                ? _companion.Actor
                : _enemiesById.TryGetValue(
                effect.SourceId,
                out CombatParticipantDefinition? enemySource)
                ? enemySource.Actor
                : null;
        if (source is null || source.IsDead || target.IsDead) return [];

        DamageType type = effect.Definition.Id is "BERSERKER_BLOOD_TRAIL" or "BERSERKER_RENDING_RAMPAGE"
            ? DamageType.Physical
            : effect.Definition.PeriodicDamageType;
        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                source,
                target,
                effect.Definition.Magnitude * effect.Stacks,
                type,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false,
                MinimumDamage: 0,
                SkipDefenseMitigation: effect.Definition.Id is "MAGE_ARCANE_ECHO" or "MAGE_ARCHMAGE_ECHO"),
            _random,
            tickAt);
        return result.Events;
    }

    private void ApplyKernelEvents(
        IEnumerable<CombatEvent> events,
        Guid sourceActorId,
        Guid targetActorId,
        string? definitionId,
        CombatWeaponHand? weaponHand = null,
        string? weaponDefinitionId = null)
    {
        CombatEvent[] normalizedEvents = events
            .Select(item => item with
            {
                DefinitionId = item.DefinitionId ?? definitionId,
                SourceActorId = item.SourceActorId ?? sourceActorId,
                TargetActorId = item.TargetActorId ?? targetActorId,
                WeaponHand = item.WeaponHand ?? weaponHand,
                WeaponDefinitionId = item.WeaponDefinitionId ?? weaponDefinitionId
            })
            .ToArray();
        HashSet<Guid> pendingEnemyDeaths = normalizedEvents
            .Where(item => item.Type == CombatEventType.ActorDied
                && item.ActorId != _player.Actor.ActorId
                && _enemiesById.ContainsKey(item.ActorId)
                && !_deadActors.Contains(item.ActorId))
            .Select(item => item.ActorId)
            .ToHashSet();

        foreach (CombatEvent normalized in normalizedEvents)
        {
            if (normalized.Type == CombatEventType.ActorDied
                && !_deadActors.Add(normalized.ActorId))
                continue;

            bool enemyDeath = normalized.Type == CombatEventType.ActorDied
                && normalized.ActorId != _player.Actor.ActorId
                && _enemiesById.ContainsKey(normalized.ActorId);
            if (enemyDeath)
                pendingEnemyDeaths.Remove(normalized.ActorId);

            Append(normalized);
            ApplyTalentHooks(normalized);
            if (normalized.Type == CombatEventType.ActorDied)
            {
                FinishForDeath(
                    normalized,
                    deferVictory: enemyDeath && pendingEnemyDeaths.Count > 0);
                if (Status != CombatSessionStatus.Active)
                    break;
            }
        }
    }

    private void ApplyTalentHooks(CombatEvent combatEvent)
    {
        if (combatEvent.TargetActorId is { } threatTarget
            && _enemyThreatTables.TryGetValue(threatTarget, out ThreatTable? threatTable)
            && combatEvent.SourceActorId is { } threatSource
            && IsPartyActor(threatSource)
            && combatEvent.Amount > 0)
        {
            threatTable.AddThreat(
                threatSource,
                combatEvent.Amount,
                threatSource == _player.Actor.ActorId
                    ? GuardianThreatMultiplier
                    : 1);
        }

        if (combatEvent.Type == CombatEventType.TauntApplied
            && combatEvent.TargetActorId is { } tauntTarget
            && _enemyForcedTargets.TryGetValue(tauntTarget, out ForcedTargetState? forcedTarget)
            && combatEvent.SourceActorId is { } tauntSource)
        {
            TimeSpan duration = TimeSpan.FromSeconds((double)combatEvent.Amount);
            if (tauntSource == _player.Actor.ActorId
                && GetGuardianHookValue("G-2-2") is { } extraDuration)
            {
                duration += TimeSpan.FromSeconds((double)extraDuration);
            }
            forcedTarget.Set(tauntSource, combatEvent.OccurredAtUtc, duration);
            if (tauntSource == _player.Actor.ActorId
                && GetGuardianHookValue("G-4-2") is { } extraThreat)
            {
                _enemyThreatTables[tauntTarget].AddThreat(
                    tauntSource,
                    extraThreat,
                    GuardianThreatMultiplier);
            }
        }

        if (combatEvent.Type == CombatEventType.AbilityCompleted
            && combatEvent.SourceActorId == _player.Actor.ActorId)
        {
            TriggerTalent(
                TalentModifierKeys.OnAbilityUsed,
                combatEvent.OccurredAtUtc,
                ToRuntimeEvent(combatEvent, CombatRuntimeEventKind.AbilityCompleted));
            ApplyGuardianAbilityHooks(combatEvent);
            ApplyWarlordAbilityHooks(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.Dodge
            && combatEvent.TargetActorId == _player.Actor.ActorId)
        {
            TriggerTalent(
                TalentModifierKeys.OnDodge,
                combatEvent.OccurredAtUtc,
                ToRuntimeEvent(combatEvent, CombatRuntimeEventKind.Dodge));
            ApplyGuardianDodgeHooks(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.Amount > 0)
        {
            bool playerTarget = combatEvent.TargetActorId == _player.Actor.ActorId;
            bool partyTarget = playerTarget
                || _companion is not null
                && combatEvent.TargetActorId == _companion.Actor.ActorId;
            if (partyTarget && !combatEvent.IsPeriodic)
            {
                if (playerTarget
                    && string.Equals(_player.ResourceType, "RAGE", StringComparison.Ordinal))
                {
                    AddResource(
                        _player.Actor,
                        BaseRageFromDirectDamageTaken * GuardianRageMultiplier,
                        combatEvent.OccurredAtUtc,
                        "DIRECT_DAMAGE_TAKEN");
                }
                if (playerTarget)
                {
                    TriggerTalent(
                        TalentModifierKeys.OnDamageTaken,
                        combatEvent.OccurredAtUtc,
                        ToRuntimeEvent(combatEvent, CombatRuntimeEventKind.DamageTaken));
                    ApplyGuardianDamageTakenHooks(combatEvent);
                }
                ApplyWarlordPartyDamageHooks(combatEvent);
            }

            if (playerTarget)
            {
                ApplyBerserkerDamageTakenHooks(combatEvent);
                ApplyMageDamageTakenHooks(combatEvent);
                ApplyArcherDamageTakenHooks(combatEvent);
            }

            ApplyWarlordAutoAttackHooks(combatEvent);
            ApplyGuardianAutoAttackHooks(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.ShieldAbsorbed
            && combatEvent.TargetActorId == _player.Actor.ActorId)
        {
            ApplyMageShieldAbsorbedHooks(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.ResourceChanged
            && combatEvent.ActorId == _player.Actor.ActorId)
        {
            ApplyMageResourceThresholdHooks(combatEvent);
            ApplyArcherResourceThresholdHooks(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.CriticalHit
            && combatEvent.SourceActorId == _player.Actor.ActorId)
        {
            TriggerTalent(
                TalentModifierKeys.OnCriticalHit,
                combatEvent.OccurredAtUtc,
                ToRuntimeEvent(combatEvent, CombatRuntimeEventKind.CriticalHit));
            ApplyBerserkerCriticalHooks(combatEvent);
            ApplyGuardianCriticalHooks(combatEvent);
            ApplyPyromancerCriticalHooks(combatEvent);
            ApplyMageCriticalHooks(combatEvent);
            ApplyArcherCriticalHooks(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.CriticalHit
            && _companion is not null
            && combatEvent.SourceActorId == _companion.Actor.ActorId)
        {
            ApplyArcherCriticalHooks(combatEvent);
            ApplyWarlordPartyCriticalHooks(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.CriticalHit
            && combatEvent.TargetActorId == _player.Actor.ActorId)
        {
            ApplyPyromancerIncomingCriticalHooks(combatEvent);
            ApplyMageIncomingCriticalHooks(combatEvent);
            ApplyArcherIncomingCriticalHooks(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.AbilityInterrupted
            && combatEvent.ActorId == _player.Actor.ActorId)
        {
            OnPyromancerAbilityInterrupted(combatEvent);
            OnMageAbilityInterrupted(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.DamageDealt)
            ApplyArcherCompanionDamageHooks(combatEvent);

        if (combatEvent.Type == CombatEventType.HealingApplied)
            ApplyArcherHealingHooks(combatEvent);
    }

    private void TriggerTalent(
        string key,
        DateTimeOffset now,
        CombatRuntimeEvent? runtimeEvent = null)
    {
        if (_genericTalentModifiers.EventHooks.Count == 0)
            return;

        runtimeEvent ??= CreateRuntimeEvent(key, now);
        foreach (TalentRuntimeAction action in _talentRuntimeEngine.Publish(
                     runtimeEvent,
                     _genericTalentModifiers))
        {
            if (action.Kind == TalentRuntimeActionKind.ResourceChange)
                AddResource(_player.Actor, action.Value, now, action.TalentId);
        }
    }

    private bool IsClassRuntimeTalent(string talentId) =>
        _player.DefinitionId switch
        {
            "WARRIOR" => BerserkerTalentRuntimeCatalog.TryGetEventKey(talentId, out _)
                || GuardianTalentRuntimeCatalog.SupportedTalentIds.Contains(talentId)
                || WarlordTalentRuntimeCatalog.SupportedTalentIds.Contains(talentId),
            "MAGE" => PyromancerTalentRuntimeCatalog.TryGetEventKey(talentId, out _)
                || MageTalentRuntimeCatalog.TryGetEventKey(talentId, out _),
            "ARCHER" => ArcherTalentRuntimeCatalog.OwnsTalentId(talentId),
            _ => false
        };

    private CombatRuntimeEvent CreateRuntimeEvent(string key, DateTimeOffset now) =>
        new(
            key switch
            {
                TalentModifierKeys.OnDamageTaken => CombatRuntimeEventKind.DamageTaken,
                TalentModifierKeys.OnCriticalHit => CombatRuntimeEventKind.CriticalHit,
                TalentModifierKeys.OnEnemyKilled => CombatRuntimeEventKind.EnemyKilled,
                TalentModifierKeys.OnAutoAttack => CombatRuntimeEventKind.AutoAttackStarted,
                TalentModifierKeys.OnAbilityUsed => CombatRuntimeEventKind.AbilityCompleted,
                TalentModifierKeys.OnHpThreshold => CombatRuntimeEventKind.HpThresholdReached,
                TalentModifierKeys.OnPartyEvent => CombatRuntimeEventKind.PartyEvent,
                _ => throw new InvalidOperationException(
                    $"Talent event key '{key}' is not mapped to a combat runtime event.")
            },
            now,
            _player.Actor.ActorId,
            TargetActorId: key == TalentModifierKeys.OnDamageTaken
                ? _player.Actor.ActorId
                : null,
            Sequence: Sequence);

    private CombatRuntimeEvent ToRuntimeEvent(
        CombatEvent combatEvent,
        CombatRuntimeEventKind kind) =>
        new(
            kind,
            combatEvent.OccurredAtUtc,
            combatEvent.SourceActorId ?? combatEvent.ActorId,
            combatEvent.TargetActorId,
            combatEvent.DefinitionId,
            Amount: combatEvent.Amount,
            DamageType: combatEvent.DamageType,
            IsPeriodic: combatEvent.IsPeriodic,
            Sequence: Sequence);

    private void FinishForDeath(
        CombatEvent death,
        bool deferVictory = false)
    {
        if (Status != CombatSessionStatus.Active) return;

        if (death.ActorId == _player.Actor.ActorId)
        {
            if (!_player.Actor.IsDead)
                return;
            Status = CombatSessionStatus.Defeat;
            EndCombat(death);
            return;
        }

        if (_companion is not null && death.ActorId == _companion.Actor.ActorId)
        {
            _nextCompanionAutoAttackAtUtc = null;
            ApplyWarlordPartyDeathHooks(death.OccurredAtUtc, _companion.Actor.ActorId);
            SyncArcherConditionalEffects(death.OccurredAtUtc);
            return;
        }

        if (!_enemiesById.TryGetValue(
                death.ActorId,
                out CombatParticipantDefinition? killedEnemy))
        {
            return;
        }

        Append(new CombatEvent(
            CombatEventType.EnemyKilled,
            death.OccurredAtUtc,
            _player.Actor.ActorId,
            killedEnemy.DefinitionId,
            SourceActorId: death.SourceActorId ?? _player.Actor.ActorId,
            TargetActorId: killedEnemy.Actor.ActorId,
            IsPeriodic: death.IsPeriodic,
            DamageType: death.DamageType,
            WeaponHand: death.WeaponHand,
            WeaponDefinitionId: death.WeaponDefinitionId));
        TriggerTalent(
            TalentModifierKeys.OnEnemyKilled,
            death.OccurredAtUtc);
        ApplyBerserkerEnemyKilledHooks(death.OccurredAtUtc);
        ApplyPyromancerEnemyKilledHooks(death);
        ApplyArcherEnemyKilledHooks(death.OccurredAtUtc);
        ApplyWarlordEnemyKilledHooks(death);

        EnemyAiRuntime killedAi = _enemyAiRuntimes[killedEnemy.Actor.ActorId];
        killedAi.State = MonsterAiState.Dead;
        killedAi.NextActionAtUtc = null;

        if (_summonProfile is not null
            && string.Equals(
                killedEnemy.DefinitionId,
                _summonProfile.SourceDefinitionId,
                StringComparison.Ordinal))
        {
            _nextSummonAtUtc = null;
        }

        CombatParticipantDefinition? nextAlive = _enemies
            .FirstOrDefault(enemy => !enemy.Actor.IsDead);
        if (nextAlive is null)
        {
            if (deferVictory)
                return;

            Status = CombatSessionStatus.Victory;
            EndCombat(death);
            return;
        }

        if (_selectedTargetActorId == killedEnemy.Actor.ActorId)
        {
            _selectedTargetActorId = nextAlive.Actor.ActorId;
            Append(new CombatEvent(
                CombatEventType.TargetChanged,
                death.OccurredAtUtc,
                _player.Actor.ActorId,
                nextAlive.DefinitionId,
                SourceActorId: _player.Actor.ActorId,
                TargetActorId: nextAlive.Actor.ActorId));
        }

        SyncBerserkerConditionalEffects(death.OccurredAtUtc);
    }

    private void EndCombat(CombatEvent terminalEvent)
    {
        _playerAutoAttackEnabled = false;
        _nextPlayerMainHandAutoAttackAtUtc = null;
        _nextPlayerOffHandAutoAttackAtUtc = null;
        _nextCompanionAutoAttackAtUtc = null;
        StopEnemyAi(MonsterAiState.Resetting);
        Append(new CombatEvent(
            CombatEventType.CombatEnded,
            terminalEvent.OccurredAtUtc,
            terminalEvent.ActorId,
            Status.ToString(),
            SourceActorId: terminalEvent.SourceActorId,
            TargetActorId: terminalEvent.ActorId,
            WeaponHand: terminalEvent.WeaponHand,
            WeaponDefinitionId: terminalEvent.WeaponDefinitionId));
    }

    private void StopEnemyAi(MonsterAiState state)
    {
        foreach (CombatParticipantDefinition enemy in _enemies)
        {
            EnemyAiRuntime aiRuntime = _enemyAiRuntimes[enemy.Actor.ActorId];
            if (enemy.Actor.IsDead)
            {
                aiRuntime.State = MonsterAiState.Dead;
            }
            else
            {
                aiRuntime.State = state;
            }
            aiRuntime.NextActionAtUtc = null;
        }
    }

    private void ApplyPlayerResourceRegen(DateTimeOffset now)
    {
        if (now <= _lastPlayerResourceRegenAtUtc) return;
        TimeSpan elapsed = now - _lastPlayerResourceRegenAtUtc;
        _lastPlayerResourceRegenAtUtc = now;
        decimal regenPerSecond = EffectiveArcherResourceRegenPerSecond(
            EffectivePlayerResourceRegenPerSecond(now),
            now);
        if (regenPerSecond <= 0 || _player.Actor.IsDead) return;

        decimal amount = regenPerSecond * (decimal)elapsed.TotalSeconds;
        AddResource(_player.Actor, amount, now, "COMBAT_REGEN");
    }

    private void AddResource(
        CombatActorState actor,
        decimal amount,
        DateTimeOffset now,
        string definitionId)
    {
        decimal actual = actor.AddResource(ScaleWarlordResource(definitionId, amount));
        if (actual == 0) return;
        Append(new CombatEvent(
            CombatEventType.ResourceChanged,
            now,
            actor.ActorId,
            definitionId,
            actual,
            SourceActorId: actor.ActorId,
            TargetActorId: actor.ActorId));
    }

    private void Append(CombatEvent combatEvent)
    {
        Sequence++;
        _events.Add(combatEvent with { Sequence = Sequence });
    }

    private CombatCommandResult Result(
        bool succeeded,
        string? errorCode,
        long before) =>
        new(succeeded, errorCode, Snapshot(), GetEventsAfter(before));

    private static CombatRuntimeState CreateRuntime(
        CombatActorState actor,
        IEnumerable<CombatActorState> others)
    {
        CombatRuntimeState runtime = new(actor);
        foreach (CombatActorState other in others)
        {
            if (other.ActorId != actor.ActorId)
                runtime.AddActor(other);
        }
        return runtime;
    }

    private CombatActorSnapshot ActorSnapshot(
        CombatParticipantDefinition definition,
        CombatRuntimeState runtime,
        bool autoAttackEnabled)
    {
        IReadOnlySet<string> knownAbilityIds = definition.Kind == CombatActorKind.Player
            ? GetPlayerKnownAbilityIds(CurrentTimeUtc)
            : definition.KnownAbilityIds;
        CombatAbilitySnapshot[] abilities = knownAbilityIds
            .Where(_abilities.ContainsKey)
            .Select(id => definition.Kind == CombatActorKind.Player
                ? ResolveArcherAbility(
                    ResolveMageAbility(
                        ResolvePyromancerAbility(
                            ResolveWarlordAbility(
                                ResolvePlayerAbilityForSnapshot(_abilities[id], CurrentTimeUtc),
                                CurrentTimeUtc),
                            CurrentTimeUtc),
                        CurrentTimeUtc),
                    CurrentTimeUtc)
                : _abilities[id])
            .OrderBy(ability => ability.Id, StringComparer.Ordinal)
            .Select(ability => new CombatAbilitySnapshot(
                ability.Id,
                ability.ResourceCost,
                ability.Cooldown))
            .ToArray();
        return new(
            definition.Actor.ActorId,
            definition.Kind,
            definition.DefinitionId,
            definition.Name,
            definition.Actor.CurrentHp,
            definition.Actor.MaxHp,
            definition.ResourceType,
            definition.Actor.CurrentResource,
            definition.Actor.MaxResource,
            autoAttackEnabled,
            definition.Kind == CombatActorKind.Player
                ? NextConsumableCooldownReadyAtUtc()
                : null,
            new Dictionary<string, DateTimeOffset>(
                runtime.Cooldowns,
                StringComparer.Ordinal),
            new HashSet<string>(knownAbilityIds, StringComparer.Ordinal),
            abilities,
            definition.Actor.ActiveEffects.Select(effect =>
                new CombatEffectSnapshot(
                    effect.Definition.Id,
                    effect.Stacks,
                    effect.ExpiresAtUtc)).ToArray(),
            runtime.ActiveCast is null
                ? null
                : new CombatCastSnapshot(
                    runtime.ActiveCast.Ability.Id,
                    runtime.ActiveCast.StartedAtUtc,
                    runtime.ActiveCast.ResolvesAtUtc),
            definition.Kind == CombatActorKind.Player
                ? new Dictionary<string, DateTimeOffset>(
                    _consumableCooldowns,
                    StringComparer.Ordinal)
                : null);
    }

    private DateTimeOffset? NextConsumableCooldownReadyAtUtc()
    {
        DateTimeOffset[] active = _consumableCooldowns.Values
            .Where(readyAt => readyAt > CurrentTimeUtc)
            .ToArray();
        return active.Length == 0 ? null : active.Min();
    }

    private static string MapAbilityError(AbilityErrorCode code) => code switch
    {
        AbilityErrorCode.CooldownActive or AbilityErrorCode.GlobalCooldownActive =>
            CombatErrorCodes.AbilityOnCooldown,
        AbilityErrorCode.InsufficientResource => CombatErrorCodes.InsufficientResource,
        AbilityErrorCode.InvalidTarget => CombatErrorCodes.InvalidTarget,
        AbilityErrorCode.DeadActor => CombatErrorCodes.ActorDead,
        AbilityErrorCode.DuplicateCommand => CombatErrorCodes.DuplicateCommand,
        _ => CombatErrorCodes.CommandRejected
    };

    private static DateTimeOffset? NextEffectDue(CombatActorState actor) =>
        actor.ActiveEffects
            .Select(effect => Min(effect.NextTickAtUtc, effect.ExpiresAtUtc))
            .Where(value => value.HasValue)
            .Min();

    private static DateTimeOffset? Min(
        DateTimeOffset? left,
        DateTimeOffset? right) =>
        left is null ? right : right is null ? left : left <= right ? left : right;

    private static TimeSpan InitialOffHandDelay(AutoAttackProfile profile)
    {
        long halfTicks = Math.Max(1, profile.Interval.Ticks / 2);
        return TimeSpan.FromTicks(halfTicks);
    }

    private static Dictionary<Guid, MonsterAiProfile> CreateSharedEnemyAiProfiles(
        IReadOnlyList<CombatParticipantDefinition> enemies,
        MonsterAiProfile profile)
    {
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(profile);
        return enemies.ToDictionary(enemy => enemy.Actor.ActorId, _ => profile);
    }

    private sealed class EnemyAiRuntime(
        MonsterAiProfile profile,
        DateTimeOffset nextActionAtUtc)
    {
        public MonsterAiProfile Profile { get; } = profile;
        public MonsterAiState State { get; set; } = MonsterAiState.InCombat;
        public DateTimeOffset? NextActionAtUtc { get; set; } = nextActionAtUtc;
    }

    private static void ValidateAutoAttack(AutoAttackProfile profile)
    {
        decimal minimum = profile.BaseDamageMin ?? profile.BaseDamage;
        decimal maximum = profile.BaseDamageMax ?? minimum;
        if (profile.Interval <= TimeSpan.Zero
            || profile.BaseDamage < 0
            || minimum < 0
            || maximum < minimum
            || profile.AttackPowerCoefficient < 0
            || profile.ResourceOnHit < 0)
            throw new ArgumentException(
                "Auto attack profile is invalid.",
                nameof(profile));
    }
}
