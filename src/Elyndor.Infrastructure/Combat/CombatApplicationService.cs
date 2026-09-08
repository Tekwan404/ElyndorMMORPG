using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Parties;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Dungeons;

namespace Elyndor.Infrastructure.Combat;

public sealed class CombatApplicationService(
    CombatSessionFactory factory,
    CombatSessionRegistry registry,
    WorldEncounterRegistry encounterRegistry,
    IContentSnapshotProvider contentProvider,
    InventoryEquipmentService inventoryService,
    CharacterOperationGuard operationGuard,
    CombatDurabilityService? durability = null,
    PartyService? partyService = null,
    DungeonService? dungeonService = null,
    BootstrapService? bootstrapService = null)
{
    public CombatApplicationService(
        CombatSessionFactory factory,
        CombatSessionRegistry registry,
        WorldEncounterRegistry encounterRegistry,
        GameContentPackage content,
        InventoryEquipmentService inventoryService,
        CharacterOperationGuard operationGuard)
        : this(
            factory,
            registry,
            encounterRegistry,
            new StaticContentSnapshotProvider(content),
            inventoryService,
            operationGuard,
            null,
            null,
            null,
            null)
    {
    }

    public Task<CombatOperationResult> StartAsync(
        Guid accountId,
        Guid encounterId,
        CancellationToken cancellationToken) =>
        operationGuard.ExecuteExclusiveAsync(
            accountId,
            () => StartEncounterCoreAsync(accountId, encounterId, cancellationToken),
            cancellationToken);

    public Task<CombatOperationResult> StartTrainingAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        operationGuard.ExecuteExclusiveAsync(
            accountId,
            () => StartTrainingCoreAsync(accountId, cancellationToken),
            cancellationToken);

    public Task<CombatOperationResult> StartDungeonEncounterAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken) =>
        operationGuard.ExecuteExclusiveAsync(
            accountId,
            () => StartDungeonEncounterCoreAsync(accountId, runId, cancellationToken),
            cancellationToken);

    public Task<CombatOperationResult> ResetTrainingAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        operationGuard.ExecuteExclusiveAsync(
            accountId,
            () => ResetTrainingCoreAsync(accountId, cancellationToken),
            cancellationToken);

    private async Task<CombatOperationResult> StartEncounterCoreAsync(
        Guid accountId,
        Guid encounterId,
        CancellationToken cancellationToken)
    {
        CombatOperationResult? active = PrepareStart(accountId);
        if (active is not null) return active;

        if (partyService is not null
            && (await partyService.GetCombatMembersAsync(accountId, cancellationToken))
                .SingleOrDefault(member => member.AccountId == accountId)
                is not { IsLeader: true })
        {
            return CombatOperationResult.Failure(CombatErrorCodes.CommandRejected);
        }

        if (!encounterRegistry.TryConsume(accountId, encounterId, out PendingWorldEncounter pending))
            return CombatOperationResult.Failure(CombatErrorCodes.InvalidEncounter);

        return await StartCoreAsync(
            accountId,
            pending.MonsterId,
            pending.LocationId,
            cancellationToken);
    }

    private async Task<CombatOperationResult> StartTrainingCoreAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        CombatOperationResult? active = PrepareStart(accountId);
        if (active is not null) return active;
        encounterRegistry.Clear(accountId);
        return await StartCoreAsync(
            accountId,
            CombatSessionFactory.TrainingDummyId,
            CombatSessionFactory.StarterTownId,
            cancellationToken);
    }

    private async Task<CombatOperationResult> StartDungeonEncounterCoreAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        CombatOperationResult? active = PrepareStart(accountId);
        if (active is not null) return active;
        if (dungeonService is null)
            return CombatOperationResult.Failure(CombatErrorCodes.CommandRejected);

        (DungeonPreparation? preparation, string? errorCode) =
            await dungeonService.PrepareEncounterAsync(accountId, runId, cancellationToken);
        if (preparation is null)
            return CombatOperationResult.Failure(errorCode ?? CombatErrorCodes.CommandRejected);

        CombatOperationResult result = await StartCoreAsync(
            accountId,
            preparation.MonsterId,
            preparation.LocationId,
            cancellationToken,
            preparation.Participants);
        if (!result.Succeeded || result.Snapshot is null)
            return result;

        bool bound = await dungeonService.BindCombatAsync(
            preparation.RunId,
            preparation.EncounterId,
            result.Snapshot.SessionId,
            cancellationToken);
        if (!bound)
        {
            await registry.DiscardAsync(accountId, cancellationToken);
            if (durability is not null)
                await durability.CompleteAsync(result.Snapshot.SessionId, cancellationToken);
        }
        return bound
            ? result
            : CombatOperationResult.Failure(CombatErrorCodes.CommandRejected);
    }

    private async Task<CombatOperationResult> ResetTrainingCoreAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        CombatOperationResult current = registry.Resume(accountId);
        if (!current.Succeeded
            || current.Snapshot is null
            || !IsTraining(current.Snapshot))
            return CombatOperationResult.Failure(CombatErrorCodes.CommandRejected);

        if (!await registry.DiscardAsync(accountId, cancellationToken))
            return CombatOperationResult.Failure(CombatErrorCodes.NotFound);
        return await StartTrainingCoreAsync(accountId, cancellationToken);
    }

    public Task<CombatOperationResult> UseAbilityAsync(
        Guid accountId, Guid sessionId, string commandId, string abilityId,
        CancellationToken cancellationToken) => registry.ExecuteParticipantAsync(
            accountId,
            (session, characterId, pinnedContent, now) =>
            {
                if (session.SessionId != sessionId)
                    return new CombatCommandResult(false, CombatErrorCodes.NotFound,
                        session.Snapshot(characterId), []);
                return session.Handle(
                    characterId,
                    new UseAbilityCommand(commandId, abilityId, Guid.Empty),
                    now);
            }, cancellationToken);

    public Task<CombatOperationResult> UseConsumableAsync(
        Guid accountId,
        Guid sessionId,
        string commandId,
        string itemDefinitionId,
        CancellationToken cancellationToken) => registry.ExecuteParticipantAsync(
            accountId,
            async (session, characterId, pinnedContent, now) =>
            {
                if (session.SessionId != sessionId)
                    return new CombatCommandResult(false, CombatErrorCodes.NotFound, session.Snapshot(characterId), []);
                if (IsTraining(session.Snapshot(characterId)))
                    return new CombatCommandResult(false, CombatErrorCodes.CommandRejected, session.Snapshot(characterId), []);
                if (session.HasProcessedCommand(commandId))
                    return new CombatCommandResult(false, CombatErrorCodes.DuplicateCommand, session.Snapshot(characterId), []);

                GameContentSnapshot contentSnapshot =
                    pinnedContent ?? contentProvider.GetCurrent();
                ItemDefinition? definition = contentSnapshot.Indexes.ItemsById
                    .GetValueOrDefault(itemDefinitionId);
                if (definition is null
                    || definition.Type != ItemType.Consumable
                    || definition.ConsumableActions is not { Count: > 0 }
                    || string.IsNullOrWhiteSpace(definition.ConsumableCooldownCategoryId))
                {
                    return new CombatCommandResult(
                        false,
                        CombatErrorCodes.CommandRejected,
                        session.Snapshot(characterId),
                        []);
                }

                ResolvedConsumableAction[]? actions = ResolveConsumableActions(
                    definition,
                    contentSnapshot.Indexes);
                if (actions is null)
                {
                    return new CombatCommandResult(
                        false,
                        CombatErrorCodes.CommandRejected,
                        session.Snapshot(characterId),
                        []);
                }

                TimeSpan cooldown = TimeSpan.FromSeconds(
                    (double)definition.ConsumableCooldownSeconds);
                string? validationError = session.ValidateConsumableUse(
                    now,
                    actions,
                    definition.ConsumableCooldownCategoryId,
                    cooldown);
                if (validationError is not null)
                {
                    return new CombatCommandResult(
                        false,
                        validationError,
                        session.Snapshot(characterId),
                        []);
                }

                string? inventoryError = durability is null
                    ? await inventoryService.ConsumeOneForCombatAsync(
                        accountId,
                        itemDefinitionId,
                        contentSnapshot,
                        cancellationToken)
                    : await durability.ReserveConsumableAsync(
                        accountId,
                        sessionId,
                        commandId,
                        itemDefinitionId,
                        contentSnapshot,
                        now,
                        cancellationToken);
                if (inventoryError is not null)
                {
                    return new CombatCommandResult(
                        false,
                        CombatErrorCodes.CommandRejected,
                        session.Snapshot(),
                        []);
                }

                try
                {
                    CombatCommandResult applied = session.Handle(
                        characterId,
                        new UseConsumableCommand(
                            commandId,
                            definition.Id,
                            actions,
                            definition.ConsumableCooldownCategoryId,
                            cooldown),
                        now);
                    if (!applied.Succeeded && durability is not null)
                    {
                        await durability.RollbackConsumableAsync(
                            sessionId,
                            commandId,
                            cancellationToken);
                    }

                    return applied;
                }
                catch
                {
                    if (durability is not null)
                    {
                        await durability.RollbackConsumableAsync(
                            sessionId,
                            commandId,
                            cancellationToken);
                    }

                    throw;
                }
            }, cancellationToken);

    public Task<CombatOperationResult> StartAutoAttackAsync(
        Guid accountId, Guid sessionId, string commandId, CancellationToken cancellationToken) =>
        ExecuteSessionCommand(accountId, sessionId,
            new StartAutoAttackCommand(commandId), cancellationToken);

    public Task<CombatOperationResult> StopAutoAttackAsync(
        Guid accountId, Guid sessionId, string commandId, CancellationToken cancellationToken) =>
        ExecuteSessionCommand(accountId, sessionId,
            new StopAutoAttackCommand(commandId), cancellationToken);

    public Task<CombatOperationResult> SelectTargetAsync(
        Guid accountId,
        Guid sessionId,
        string commandId,
        Guid targetActorId,
        CancellationToken cancellationToken) =>
        ExecuteSessionCommand(
            accountId,
            sessionId,
            new SelectTargetCommand(commandId, targetActorId),
            cancellationToken);

    public CombatOperationResult Resume(Guid accountId) => registry.Resume(accountId);

    public async Task<CombatOperationResult> AttachAsync(
        Guid accountId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (partyService is null)
            return CombatOperationResult.Failure(CombatErrorCodes.CommandRejected);

        PartyCombatMember? member = (await partyService.GetCombatMembersAsync(
                accountId,
                cancellationToken))
            .SingleOrDefault(candidate => candidate.AccountId == accountId);
        if (member is null || bootstrapService is null)
            return CombatOperationResult.Failure(CombatErrorCodes.CommandRejected);

        CombatOperationResult current = registry.Resume(accountId);
        if (!current.Succeeded
            || current.Snapshot is null
            || current.Snapshot.SessionId != sessionId)
        {
            return CombatOperationResult.Failure(CombatErrorCodes.NotFound);
        }

        BootstrapSnapshot bootstrap = await bootstrapService.GetAsync(
            accountId,
            contentProvider.GetCurrent(),
            cancellationToken,
            checkpoint: true);
        if (bootstrap.World is null || bootstrap.World.Travel is not null)
            return CombatOperationResult.Failure(CombatErrorCodes.InvalidLocation);

        CombatDurabilityBeginResult? durabilityBegin = null;
        if (durability is not null)
        {
            durabilityBegin = await durability.BeginParticipantAsync(
                member.CharacterId,
                current.Snapshot,
                cancellationToken);
            if (!durabilityBegin.Succeeded)
                return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);
        }

        CombatOperationResult result = await registry.AttachAsync(
                accountId,
                sessionId,
                member.CharacterId,
                bootstrap.World.CurrentLocation.Id,
                cancellationToken);
        if (!result.Succeeded
            && durability is not null
            && durabilityBegin?.Created == true)
        {
            await durability.RemoveParticipantAsync(
                sessionId,
                member.CharacterId,
                cancellationToken);
        }

        return result;
    }

    public async Task<CombatOperationResult> LeaveAsync(
        Guid accountId,
        string commandId,
        CancellationToken cancellationToken)
    {
        CombatOperationResult current = registry.Resume(accountId);
        if (!current.Succeeded || current.Snapshot is null)
            return current;
        if (IsTraining(current.Snapshot))
            return await registry.LeaveAsync(accountId, cancellationToken);

        return await FleeAsync(
            accountId,
            current.Snapshot.SessionId,
            commandId,
            cancellationToken);
    }

    public Task<CombatOperationResult> FleeAsync(
        Guid accountId,
        Guid sessionId,
        string commandId,
        CancellationToken cancellationToken) =>
        ExecuteSessionCommand(
            accountId,
            sessionId,
            new FleeCommand(commandId),
            cancellationToken);

    private static ResolvedConsumableAction[]? ResolveConsumableActions(
        ItemDefinition definition,
        GameContentIndexes indexes)
    {
        if (definition.ConsumableActions is not { Count: > 0 })
            return null;

        List<ResolvedConsumableAction> actions = [];
        foreach (ConsumableActionDefinition action in definition.ConsumableActions)
        {
            EffectDefinition? effect = null;
            if (action.Type == ConsumableActionType.ApplyEffect)
            {
                if (string.IsNullOrWhiteSpace(action.EffectId)
                    || !indexes.EffectsById.TryGetValue(action.EffectId, out effect))
                {
                    return null;
                }
            }

            actions.Add(new ResolvedConsumableAction(
                action.Type,
                action.Amount,
                action.ResourceType,
                effect,
                action.Type == ConsumableActionType.RemoveEffect
                    ? action.EffectId
                    : null,
                action.DispelCategory));
        }

        return actions.ToArray();
    }

    private CombatOperationResult? PrepareStart(Guid accountId)
    {
        registry.ClearFinished(accountId);
        CombatOperationResult existing = registry.Resume(accountId);
        return existing.Succeeded
            ? CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive)
            : null;
    }

    private async Task<CombatOperationResult> StartCoreAsync(
        Guid accountId,
        string monsterId,
        string locationId,
        CancellationToken cancellationToken,
        IReadOnlyList<PartyCombatMember>? partyMembersOverride = null)
    {
        CombatSessionCreationResult created = await factory.CreateAsync(
            accountId,
            monsterId,
            locationId,
            cancellationToken,
            partyMembersOverride);
        if (!created.Succeeded)
            return CombatOperationResult.Failure(created.ErrorCode!);

        bool isTraining = string.Equals(
            monsterId,
            CombatSessionFactory.TrainingDummyId,
            StringComparison.Ordinal);
        if (!isTraining
            && durability is not null
            )
        {
            HashSet<Guid> initiallyAttachedCharacterIds = created.Session!
                .Snapshot()
                .ParticipantRoster?
                .Where(participant => participant.Status == CombatParticipantStatus.Active)
                .Select(participant => participant.CharacterId)
                .ToHashSet()
                ?? [created.CharacterId];
            foreach (CombatSessionParticipant participant in (created.Participants
                         ?? [new CombatSessionParticipant(accountId, created.CharacterId)])
                         .Where(participant => initiallyAttachedCharacterIds.Contains(participant.CharacterId)))
            {
                if (await durability.BeginAsync(
                        participant.CharacterId,
                        created.Session!.Snapshot(participant.CharacterId),
                        cancellationToken))
                    continue;

                await durability.CompleteAsync(
                    created.Session!.SessionId,
                    cancellationToken);
                return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);
            }
        }

        CombatSessionParticipant[] participants = created.Participants?.ToArray()
            ?? [new CombatSessionParticipant(accountId, created.CharacterId)];
        CombatParticipantBinding[] additionalParticipants = participants
            .Where(participant => participant.CharacterId != created.CharacterId)
            .Select(participant => new CombatParticipantBinding(
                participant.AccountId,
                participant.CharacterId))
            .ToArray();

        if (!registry.TryAdd(
                accountId,
                created.CharacterId,
                created.Session!,
                created.ContentSnapshot,
                additionalParticipants,
                locationId))
        {
            if (!isTraining && durability is not null)
            {
                await durability.CompleteAsync(
                    created.Session!.SessionId,
                    cancellationToken);
            }
            return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);
        }

        return CombatOperationResult.FromSnapshot(
            created.Session!.Snapshot(),
            created.ContentSnapshot) with
        {
            Events = created.Session.GetEventsAfter(0)
        };
    }

    private Task<CombatOperationResult> ExecuteSessionCommand(
        Guid accountId, Guid sessionId, CombatCommand command, CancellationToken cancellationToken) =>
        registry.ExecuteParticipantAsync(accountId, (session, characterId, now) =>
            session.SessionId == sessionId
                ? session.Handle(characterId, command, now)
                : new CombatCommandResult(false, CombatErrorCodes.NotFound, session.Snapshot(characterId), []),
            cancellationToken);

    private static bool IsTraining(CombatSessionSnapshot snapshot) =>
        string.Equals(
            snapshot.Enemy.DefinitionId,
            CombatSessionFactory.TrainingDummyId,
            StringComparison.Ordinal);
}
