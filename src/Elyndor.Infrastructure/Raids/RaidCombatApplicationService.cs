using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.World;

namespace Elyndor.Infrastructure.Raids;

public sealed class RaidCombatApplicationService(
    RaidService raidService,
    RaidCombatRosterResolver rosterResolver,
    RaidCombatSessionFactory sessionFactory,
    CombatSessionRegistry registry,
    WorldEncounterRegistry encounterRegistry,
    CombatDurabilityService durability,
    BootstrapService bootstrapService,
    IContentSnapshotProvider contentProvider,
    CharacterOperationGuard operationGuard)
{
    public Task<CombatOperationResult> StartAsync(
        Guid accountId,
        Guid encounterId,
        CancellationToken cancellationToken) =>
        operationGuard.ExecuteExclusiveAsync(
            accountId,
            () => StartCoreAsync(accountId, encounterId, cancellationToken),
            cancellationToken);

    public CombatOperationResult Resume(Guid accountId) => registry.Resume(accountId);

    public CombatOperationResult Resume(Guid accountId, Guid sessionId)
    {
        CombatOperationResult current = registry.Resume(accountId);
        return current.Succeeded
            && current.Snapshot?.SessionId == sessionId
                ? current
                : CombatOperationResult.Failure(CombatErrorCodes.NotFound);
    }

    private async Task<CombatOperationResult> StartCoreAsync(
        Guid accountId,
        Guid encounterId,
        CancellationToken cancellationToken)
    {
        registry.ClearFinished(accountId);
        if (registry.Resume(accountId).Succeeded)
            return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);

        RaidSnapshot? raid = await raidService.GetAsync(accountId, cancellationToken);
        if (raid is null)
            return CombatOperationResult.Failure(RaidCombatRosterErrorCodes.RaidNotFound);

        BootstrapSnapshot callerBootstrap = await bootstrapService.GetAsync(
            accountId,
            contentProvider.GetCurrent(),
            cancellationToken,
            checkpoint: true);
        if (callerBootstrap.Character is null
            || callerBootstrap.Character.Id != raid.LeaderCharacterId)
        {
            return CombatOperationResult.Failure(RaidCombatRosterErrorCodes.NotLeader);
        }

        if (!encounterRegistry.TryConsume(accountId, encounterId, out PendingWorldEncounter pending))
            return CombatOperationResult.Failure(CombatErrorCodes.InvalidEncounter);

        RaidCombatRosterResult roster = await rosterResolver.ResolveAsync(
            raid.RaidId,
            raid.LeaderCharacterId,
            pending.LocationId,
            CombatParticipantLimit.MaximumRaid,
            cancellationToken);
        if (!roster.Succeeded)
            return CombatOperationResult.Failure(roster.ErrorCode!);

        CombatSessionCreationResult created = await sessionFactory.CreateAsync(
            accountId,
            pending.MonsterId,
            pending.LocationId,
            roster.Members,
            cancellationToken);
        if (!created.Succeeded || created.Session is null)
            return CombatOperationResult.Failure(created.ErrorCode ?? CombatErrorCodes.CommandRejected);

        CombatSessionParticipant[] participants = created.Participants?.ToArray()
            ?? [new CombatSessionParticipant(accountId, created.CharacterId)];
        List<Guid> durableParticipants = [];
        foreach (CombatSessionParticipant participant in participants)
        {
            if (await durability.BeginAsync(
                    participant.CharacterId,
                    created.Session.Snapshot(participant.CharacterId),
                    cancellationToken))
            {
                durableParticipants.Add(participant.CharacterId);
                continue;
            }

            await durability.CompleteAsync(created.Session.SessionId, cancellationToken);
            return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);
        }

        CombatParticipantBinding[] additionalBindings = participants
            .Where(participant => participant.CharacterId != created.CharacterId)
            .Select(participant => new CombatParticipantBinding(
                participant.AccountId,
                participant.CharacterId))
            .ToArray();
        if (!registry.TryAdd(
                accountId,
                created.CharacterId,
                created.Session,
                created.ContentSnapshot,
                additionalBindings,
                pending.LocationId))
        {
            await durability.CompleteAsync(created.Session.SessionId, cancellationToken);
            return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);
        }

        return CombatOperationResult.FromSnapshot(
            created.Session.Snapshot(created.CharacterId),
            created.ContentSnapshot) with
        {
            Events = created.Session.GetEventsAfter(0)
        };
    }
}
