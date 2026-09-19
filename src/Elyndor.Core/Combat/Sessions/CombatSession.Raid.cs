using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Contribution;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private CombatGroupContext _groupContext = CombatGroupContext.Party;

    public CombatGroupContext GroupContext => _groupContext;

    public CombatSession(
        Guid sessionId,
        CombatParticipantDefinition player,
        CombatParticipantDefinition enemy,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        MonsterAiProfile enemyAi,
        ResolvedTalentModifiers playerTalents,
        IGameRandom random,
        DateTimeOffset startedAtUtc,
        string contentVersion,
        string balanceVersion,
        IReadOnlyDictionary<string, DateTimeOffset>? initialPlayerCooldowns,
        CombatSummonProfile? summonProfile,
        CombatParticipantDefinition? companion,
        Guid playerAccountId,
        IReadOnlyList<CombatPlayerDefinition> additionalPlayers,
        CombatGroupContext groupContext,
        int maximumParticipants)
        : this(
            sessionId,
            player,
            enemy,
            abilities,
            enemyAi,
            playerTalents,
            random,
            startedAtUtc,
            contentVersion,
            balanceVersion,
            initialPlayerCooldowns,
            summonProfile,
            companion,
            playerAccountId,
            additionalPlayers: null)
    {
        ArgumentNullException.ThrowIfNull(additionalPlayers);
        if (groupContext is not CombatGroupContext.Raid)
            throw new ArgumentException("The extended combat constructor is reserved for raid context.", nameof(groupContext));
        if (maximumParticipants <= CombatParticipantLimit.DefaultParty
            || maximumParticipants > CombatParticipantLimit.MaximumRaid)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumParticipants));
        }
        if (additionalPlayers.Count + 1 > maximumParticipants)
            throw new ArgumentException("The combat roster exceeds its participant limit.", nameof(additionalPlayers));

        CombatPlayerDefinition[] allAdditional = additionalPlayers.ToArray();
        if (allAdditional.Any(item =>
                item.AccountId == Guid.Empty
                || item.Participant.Kind != CombatActorKind.Player
                || item.TalentModifiers is null)
            || allAdditional.Select(item => item.AccountId).Distinct().Count() != allAdditional.Length
            || allAdditional.Select(item => item.Participant.Actor.ActorId).Distinct().Count() != allAdditional.Length
            || allAdditional.Any(item => item.AccountId == playerAccountId
                || item.Participant.Actor.ActorId == player.Actor.ActorId))
        {
            throw new ArgumentException("Raid player participant identifiers and definitions must be unique.", nameof(additionalPlayers));
        }

        _groupContext = groupContext;

        foreach (CombatPlayerDefinition playerDefinition in allAdditional)
        {
            ValidateAutoAttack(playerDefinition.Participant.AutoAttack);
            if (playerDefinition.Participant.OffHandAutoAttack is not null)
                ValidateAutoAttack(playerDefinition.Participant.OffHandAutoAttack);
            if (playerDefinition.Participant.ResourceRegenPerSecond < 0)
                throw new ArgumentOutOfRangeException(nameof(additionalPlayers));

            foreach (CombatPlayerRuntimeState existing in _playerStatesByActorId.Values)
                existing.Runtime.AddActor(playerDefinition.Participant.Actor);
            _companionRuntime?.AddActor(playerDefinition.Participant.Actor);
            foreach (CombatRuntimeState enemyRuntime in _enemyRuntimes.Values)
                enemyRuntime.AddActor(playerDefinition.Participant.Actor);

            CombatPlayerRuntimeState state = new(
                playerDefinition,
                CreateRuntime(
                    playerDefinition.Participant.Actor,
                    _playerStatesByActorId.Values
                        .Select(item => item.Definition.Actor)
                        .Concat(companion is null ? [] : [companion.Actor])
                        .Concat(_enemies.Select(item => item.Actor))));
            state.InitializeTalentRuntime(_random);
            state.SelectedTargetActorId = _primaryEnemyActorId;
            state.LastResourceRegenAtUtc = startedAtUtc;
            state.AutoAttackEnabled = playerDefinition.InitiallyAttached
                && playerDefinition.Participant.CanAutoAttack;
            state.NextMainHandAutoAttackAtUtc = state.AutoAttackEnabled
                ? startedAtUtc
                : null;
            state.NextOffHandAutoAttackAtUtc = state.AutoAttackEnabled
                && playerDefinition.Participant.OffHandAutoAttack is not null
                    ? startedAtUtc + InitialOffHandDelay(playerDefinition.Participant.OffHandAutoAttack)
                    : null;
            foreach ((string abilityId, DateTimeOffset readyAtUtc) in
                     playerDefinition.InitialCooldowns
                     ?? new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal))
            {
                if (readyAtUtc > startedAtUtc)
                    state.Runtime.Cooldowns[abilityId] = readyAtUtc;
            }

            _playerStatesByActorId.Add(playerDefinition.Participant.Actor.ActorId, state);
            if (playerDefinition.InitiallyAttached)
            {
                foreach (ThreatTable threat in _enemyThreatTables.Values)
                    threat.AddThreat(playerDefinition.Participant.Actor.ActorId, 1);
            }
        }

        CombatParticipantSnapshot primaryBinding = _participantRoster.Participants.Single();
        CombatParticipantIdentity[] capturedRoster =
        [
            new(primaryBinding.AccountId, primaryBinding.CharacterId, primaryBinding.ActorId),
            .. allAdditional.Select(item => new CombatParticipantIdentity(
                item.AccountId,
                item.Participant.Actor.ActorId,
                item.Participant.Actor.ActorId))
        ];
        _participantRoster = new CombatParticipantRoster(
            capturedRoster,
            startedAtUtc,
            maximumParticipants);
        if (!_participantRoster.TryAttach(primaryBinding.CharacterId, startedAtUtc, out _))
            throw new InvalidOperationException("Raid leader could not be attached to the captured combat roster.");
        foreach (CombatPlayerDefinition playerDefinition in allAdditional.Where(item => item.InitiallyAttached))
        {
            if (!_participantRoster.TryAttach(
                    playerDefinition.Participant.Actor.ActorId,
                    startedAtUtc,
                    out _))
            {
                throw new InvalidOperationException("A raid combat participant could not be attached to the captured roster.");
            }
        }

        Dictionary<Guid, Guid> contributionActorOwners = _playerStatesByActorId.Values
            .ToDictionary(
                state => state.Definition.Actor.ActorId,
                state => state.Definition.Actor.ActorId);
        if (companion is not null)
            contributionActorOwners[companion.Actor.ActorId] = player.Actor.ActorId;
        _contributionLedger = new ContributionLedger(contributionActorOwners);
        foreach (CombatParticipantSnapshot participant in _participantRoster.Participants
                     .Where(item => item.Status == CombatParticipantStatus.Active))
        {
            _contributionLedger.Register(
                participant.CharacterId,
                participant.ActorId,
                startedAtUtc);
        }
        foreach (CombatEvent existingEvent in _events)
            _contributionLedger.Record(existingEvent);

        Guid activeBeforeRaidInitialization = _activePlayerState.Definition.Actor.ActorId;
        foreach (CombatPlayerDefinition playerDefinition in allAdditional.Where(item => item.InitiallyAttached))
        {
            ActivatePlayer(playerDefinition.Participant.Actor.ActorId);
            ApplyGuardianStartingEffects(startedAtUtc);
            ApplyWarlordPassiveEffects(startedAtUtc);
        }
        ActivatePlayer(activeBeforeRaidInitialization);
    }
}
