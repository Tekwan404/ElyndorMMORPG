using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const decimal BaseRageFromDirectDamageTaken = 5;
    private readonly CombatParticipantDefinition _player;
    private readonly CombatParticipantDefinition[] _enemies;
    private readonly Dictionary<Guid, CombatParticipantDefinition> _enemiesById;
    private readonly CombatRuntimeState _playerRuntime;
    private readonly Dictionary<Guid, CombatRuntimeState> _enemyRuntimes;
    private readonly Guid _primaryEnemyActorId;
    private Guid _selectedTargetActorId;

    // Transitional compatibility projection: existing class runtimes operate on the
    // currently selected enemy while the authoritative session state stores all enemies.
    private CombatParticipantDefinition _enemy => _enemiesById[_selectedTargetActorId];
    private CombatRuntimeState _enemyRuntime => _enemyRuntimes[_selectedTargetActorId];
    private CombatParticipantDefinition _primaryEnemy => _enemiesById[_primaryEnemyActorId];
    private CombatRuntimeState _primaryEnemyRuntime => _enemyRuntimes[_primaryEnemyActorId];
    private readonly IReadOnlyDictionary<string, AbilityDefinition> _abilities;
    private readonly MonsterAiProfile _enemyAi;
    private readonly ResolvedTalentModifiers _playerTalents;
    private readonly IGameRandom _random;
    private readonly HashSet<string> _processedCommandIds = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _deadActors = [];
    private readonly List<CombatEvent> _events = [];
    private readonly Dictionary<string, DateTimeOffset> _talentInternalCooldowns = new(StringComparer.Ordinal);
    private DateTimeOffset? _nextPlayerMainHandAutoAttackAtUtc;
    private DateTimeOffset? _nextPlayerOffHandAutoAttackAtUtc;
    private DateTimeOffset? _nextEnemyActionAtUtc;
    private DateTimeOffset? _consumableCooldownReadyAtUtc;
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
        string balanceVersion = "UNVERSIONED")
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
            balanceVersion)
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
        string balanceVersion = "UNVERSIONED")
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(primaryEnemyAi);
        ArgumentNullException.ThrowIfNull(playerTalents);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(balanceVersion);
        if (enemies.Count == 0)
            throw new ArgumentException("CombatSession requires at least one enemy.", nameof(enemies));
        if (player.Kind != CombatActorKind.Player
            || enemies.Any(enemy => enemy.Kind != CombatActorKind.Monster))
        {
            throw new ArgumentException("CombatSession requires one player and monster enemies.");
        }
        if (enemies.Select(enemy => enemy.Actor.ActorId).Distinct().Count() != enemies.Count)
            throw new ArgumentException("Enemy actor identifiers must be unique.", nameof(enemies));
        if (enemies.Any(enemy => enemy.Actor.ActorId == player.Actor.ActorId))
            throw new ArgumentException("Player and enemy actor identifiers must be unique.", nameof(enemies));

        ValidateAutoAttack(player.AutoAttack);
        if (player.OffHandAutoAttack is not null)
            ValidateAutoAttack(player.OffHandAutoAttack);
        foreach (CombatParticipantDefinition enemy in enemies)
            ValidateAutoAttack(enemy.AutoAttack);
        if (player.ResourceRegenPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(player), "Resource regeneration cannot be negative.");

        SessionId = sessionId;
        ContentVersion = contentVersion;
        BalanceVersion = balanceVersion;
        _player = player;
        _enemies = enemies.ToArray();
        _enemiesById = _enemies.ToDictionary(enemy => enemy.Actor.ActorId);
        _primaryEnemyActorId = _enemies[0].Actor.ActorId;
        _selectedTargetActorId = _primaryEnemyActorId;
        _abilities = abilities;
        _enemyAi = primaryEnemyAi;
        _playerTalents = playerTalents;
        _random = random;

        CombatActorState[] enemyActors = _enemies.Select(enemy => enemy.Actor).ToArray();
        _playerRuntime = CreateRuntime(player.Actor, enemyActors);
        _enemyRuntimes = _enemies.ToDictionary(
            enemy => enemy.Actor.ActorId,
            enemy => CreateRuntime(
                enemy.Actor,
                new[] { player.Actor }.Concat(
                    enemyActors.Where(actor => actor.ActorId != enemy.Actor.ActorId))));

        CurrentTimeUtc = startedAtUtc;
        _lastPlayerResourceRegenAtUtc = startedAtUtc;
        Status = CombatSessionStatus.Active;
        _playerAutoAttackEnabled = player.CanAutoAttack;
        _nextPlayerMainHandAutoAttackAtUtc = player.CanAutoAttack ? startedAtUtc : null;
        _nextPlayerOffHandAutoAttackAtUtc = player.CanAutoAttack
            && player.OffHandAutoAttack is not null
                ? startedAtUtc + InitialOffHandDelay(player.OffHandAutoAttack)
                : null;
        _nextEnemyActionAtUtc = startedAtUtc + _primaryEnemy.AutoAttack.Interval;
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
            next = Min(next, _nextEnemyActionAtUtc);
            next = Min(next, _playerRuntime.ActiveCast?.ResolvesAtUtc);
            next = Min(next, NextEffectDue(_player.Actor));
            foreach (CombatParticipantDefinition enemy in _enemies)
            {
                next = Min(next, _enemyRuntimes[enemy.Actor.ActorId].ActiveCast?.ResolvesAtUtc);
                next = Min(next, NextEffectDue(enemy.Actor));
            }
            return next;
        }
    }

    public bool HasProcessedCommand(string commandId) =>
        !string.IsNullOrWhiteSpace(commandId) && _processedCommandIds.Contains(commandId);

    public string? ValidateConsumableUse(DateTimeOffset now, decimal healAmount)
    {
        if (Status != CombatSessionStatus.Active) return CombatErrorCodes.Ended;
        if (_player.Actor.IsDead) return CombatErrorCodes.ActorDead;
        if (healAmount <= 0) return CombatErrorCodes.CommandRejected;
        if (_player.Actor.CurrentHp >= _player.Actor.MaxHp) return CombatErrorCodes.ConsumableNotNeeded;
        if (_consumableCooldownReadyAtUtc is { } readyAt && readyAt > now)
            return CombatErrorCodes.ConsumableOnCooldown;
        return null;
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
            _selectedTargetActorId);
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
                _nextEnemyActionAtUtc = null;
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
        AbilityDefinition ability = ResolvePyromancerAbility(
            ResolvePlayerAbility(baseAbility, now),
            now);
        AbilityExecutionResult execution = AbilityEngine.Execute(
            _playerRuntime,
            ability,
            new AbilityIntent(command.CommandId, command.AbilityId, command.TargetActorId),
            now,
            _random);
        if (!execution.Succeeded)
            return Result(false, MapAbilityError(execution.ErrorCode), before);

        ApplyKernelEvents(
            execution.Events,
            _player.Actor.ActorId,
            command.TargetActorId,
            command.AbilityId);
        OnPyromancerAbilityStarted(ability, now);
        if (ability.Type != AbilityType.Casted)
        {
            OnPlayerAbilitySucceeded(
                ability,
                execution,
                command.TargetActorId,
                now);
            OnPyromancerAbilityResolved(
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
            TargetActorId: command.TargetActorId));
        return Result(true, null, before);
    }

    private CombatCommandResult UseConsumable(
        UseConsumableCommand command,
        DateTimeOffset now,
        long before)
    {
        string? validationError = ValidateConsumableUse(now, command.HealAmount);
        if (validationError is not null)
            return Result(false, validationError, before);
        if (command.Cooldown < TimeSpan.Zero)
            return Result(false, CombatErrorCodes.CommandRejected, before);

        decimal previousHp = _player.Actor.CurrentHp;
        _player.Actor.ApplyHealing(command.HealAmount);
        decimal healed = _player.Actor.CurrentHp - previousHp;
        _consumableCooldownReadyAtUtc = now + command.Cooldown;
        Append(new CombatEvent(
            CombatEventType.ConsumableUsed,
            now,
            _player.Actor.ActorId,
            command.ItemDefinitionId,
            healed,
            SourceActorId: _player.Actor.ActorId,
            TargetActorId: _player.Actor.ActorId));
        SyncBerserkerConditionalEffects(now);
        return Result(true, null, before);
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
            Append(new CombatEvent(
                CombatEventType.AutoAttackStarted,
                now,
                _player.Actor.ActorId,
                SourceActorId: _player.Actor.ActorId,
                TargetActorId: _enemy.Actor.ActorId));
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
            CompleteReadyCast(
                _playerRuntime,
                _player.Actor.ActorId,
                due);
            foreach (CombatParticipantDefinition enemy in _enemies)
            {
                CompleteReadyCast(
                    _enemyRuntimes[enemy.Actor.ActorId],
                    enemy.Actor.ActorId,
                    due);
                if (Status != CombatSessionStatus.Active) break;
            }

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

            if (Status == CombatSessionStatus.Active && _nextEnemyActionAtUtc <= due)
            {
                ResolveEnemyAction(due);
                _nextEnemyActionAtUtc = Status == CombatSessionStatus.Active
                    && !_primaryEnemy.Actor.IsDead
                        ? due + _primaryEnemy.AutoAttack.Interval
                        : null;
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

    private void ResolveEnemyAction(DateTimeOffset now)
    {
        if (_primaryEnemy.Actor.IsDead)
        {
            _nextEnemyActionAtUtc = null;
            return;
        }

        SyncBerserkerConditionalEffects(now);
        foreach (string abilityId in _enemyAi.PriorityAbilityIds)
        {
            if (!_primaryEnemy.KnownAbilityIds.Contains(abilityId)
                || !_abilities.TryGetValue(abilityId, out AbilityDefinition? ability))
                continue;
            string commandId = $"ai:{Sequence + 1}:{abilityId}";
            AbilityExecutionResult execution = AbilityEngine.Execute(
                _primaryEnemyRuntime,
                ability,
                new AbilityIntent(commandId, abilityId, _player.Actor.ActorId),
                now,
                _random);
            if (!execution.Succeeded) continue;
            ApplyKernelEvents(
                execution.Events,
                _primaryEnemy.Actor.ActorId,
                _player.Actor.ActorId,
                abilityId);
            Append(new CombatEvent(
                CombatEventType.AbilityUsed,
                now,
                _primaryEnemy.Actor.ActorId,
                abilityId,
                SourceActorId: _primaryEnemy.Actor.ActorId,
                TargetActorId: _player.Actor.ActorId));
            return;
        }

        ResolveAutoAttack(_primaryEnemy, _player, now);
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
        decimal baseDamage = AutoAttackDamageRoller.RollBaseDamage(
                source.AutoAttack,
                _random)
            + attackPower * source.AutoAttack.AttackPowerCoefficient;
        DamageResult damage = DamagePipeline.Resolve(
            new DamageRequest(
                source.Actor,
                target.Actor,
                baseDamage,
                DamageType.Physical),
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
        AbilityExecutionResult completion =
            AbilityEngine.CompleteCast(runtime, now, _random);
        if (!completion.Succeeded) return;

        ApplyKernelEvents(
            completion.Events,
            sourceActorId,
            cast.TargetId,
            cast.Ability.Id);
        if (runtime == _playerRuntime)
        {
            OnPlayerAbilitySucceeded(
                cast.Ability,
                completion,
                cast.TargetId,
                now);
            OnPyromancerAbilityResolved(
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
                MinimumDamage: 0),
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
        foreach (CombatEvent item in events)
        {
            CombatEvent normalized = item with
            {
                DefinitionId = item.DefinitionId ?? definitionId,
                SourceActorId = item.SourceActorId ?? sourceActorId,
                TargetActorId = item.TargetActorId ?? targetActorId,
                WeaponHand = item.WeaponHand ?? weaponHand,
                WeaponDefinitionId = item.WeaponDefinitionId ?? weaponDefinitionId
            };
            if (normalized.Type == CombatEventType.ActorDied
                && !_deadActors.Add(normalized.ActorId))
                continue;

            Append(normalized);
            ApplyTalentHooks(normalized);
            if (normalized.Type == CombatEventType.ActorDied)
            {
                FinishForDeath(normalized);
                if (Status != CombatSessionStatus.Active)
                {
                    break;
                }
            }
        }
    }

    private void ApplyTalentHooks(CombatEvent combatEvent)
    {
        if (combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.TargetActorId == _player.Actor.ActorId
            && combatEvent.Amount > 0)
        {
            if (!combatEvent.IsPeriodic)
            {
                if (string.Equals(_player.ResourceType, "RAGE", StringComparison.Ordinal))
                {
                    AddResource(
                        _player.Actor,
                        BaseRageFromDirectDamageTaken,
                        combatEvent.OccurredAtUtc,
                        "DIRECT_DAMAGE_TAKEN");
                }
                TriggerTalent(
                    TalentModifierKeys.OnDamageTaken,
                    combatEvent.OccurredAtUtc);
            }

            ApplyBerserkerDamageTakenHooks(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.CriticalHit
            && combatEvent.SourceActorId == _player.Actor.ActorId)
        {
            TriggerTalent(
                TalentModifierKeys.OnCriticalHit,
                combatEvent.OccurredAtUtc);
            ApplyBerserkerCriticalHooks(combatEvent);
            ApplyPyromancerCriticalHooks(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.CriticalHit
            && combatEvent.TargetActorId == _player.Actor.ActorId)
        {
            ApplyPyromancerIncomingCriticalHooks(combatEvent);
        }

        if (combatEvent.Type == CombatEventType.AbilityInterrupted
            && combatEvent.ActorId == _player.Actor.ActorId)
        {
            OnPyromancerAbilityInterrupted(combatEvent);
        }
    }

    private void TriggerTalent(string key, DateTimeOffset now)
    {
        foreach (ResolvedTalentEventHook hook in _playerTalents.EventHooks.Where(
                     item => item.Key == key))
        {
            if (BerserkerTalentRuntimeCatalog.TryGetEventKey(hook.TalentId, out _)
                || PyromancerTalentRuntimeCatalog.TryGetEventKey(hook.TalentId, out _))
            {
                continue;
            }

            if (_talentInternalCooldowns.TryGetValue(
                    hook.TalentId,
                    out DateTimeOffset readyAt)
                && readyAt > now)
                continue;

            AddResource(_player.Actor, hook.Value, now, hook.TalentId);
            if (hook.InternalCooldown > TimeSpan.Zero)
            {
                _talentInternalCooldowns[hook.TalentId] =
                    now + hook.InternalCooldown;
            }
        }
    }

    private void FinishForDeath(CombatEvent death)
    {
        if (Status != CombatSessionStatus.Active) return;

        if (death.ActorId == _player.Actor.ActorId)
        {
            Status = CombatSessionStatus.Defeat;
            EndCombat(death);
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

        if (killedEnemy.Actor.ActorId == _primaryEnemyActorId)
            _nextEnemyActionAtUtc = null;

        CombatParticipantDefinition? nextAlive = _enemies
            .FirstOrDefault(enemy => !enemy.Actor.IsDead);
        if (nextAlive is null)
        {
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
        _nextEnemyActionAtUtc = null;
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

    private void ApplyPlayerResourceRegen(DateTimeOffset now)
    {
        if (now <= _lastPlayerResourceRegenAtUtc) return;
        TimeSpan elapsed = now - _lastPlayerResourceRegenAtUtc;
        _lastPlayerResourceRegenAtUtc = now;
        if (_player.ResourceRegenPerSecond <= 0 || _player.Actor.IsDead) return;

        decimal amount = _player.ResourceRegenPerSecond * (decimal)elapsed.TotalSeconds;
        AddResource(_player.Actor, amount, now, "COMBAT_REGEN");
    }

    private void AddResource(
        CombatActorState actor,
        decimal amount,
        DateTimeOffset now,
        string definitionId)
    {
        decimal actual = actor.AddResource(amount);
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
                ? ResolvePyromancerAbility(
                    ResolvePlayerAbilityForSnapshot(_abilities[id], CurrentTimeUtc),
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
                ? _consumableCooldownReadyAtUtc
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
                    runtime.ActiveCast.ResolvesAtUtc));
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
