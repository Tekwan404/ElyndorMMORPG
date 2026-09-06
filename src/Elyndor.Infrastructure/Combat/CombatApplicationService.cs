using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Content;

namespace Elyndor.Infrastructure.Combat;

public sealed class CombatApplicationService(
    CombatSessionFactory factory,
    CombatSessionRegistry registry,
    WorldEncounterRegistry encounterRegistry,
    IContentSnapshotProvider contentProvider,
    InventoryEquipmentService inventoryService,
    CharacterOperationGuard operationGuard)
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
            operationGuard)
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
        CancellationToken cancellationToken) => registry.ExecuteAsync(
            accountId,
            (session, pinnedContent, now) =>
            {
                if (session.SessionId != sessionId)
                    return new CombatCommandResult(false, CombatErrorCodes.NotFound,
                        session.Snapshot(), []);
                return session.Handle(
                    new UseAbilityCommand(commandId, abilityId, Guid.Empty), now);
            }, cancellationToken);

    public Task<CombatOperationResult> UseConsumableAsync(
        Guid accountId,
        Guid sessionId,
        string commandId,
        string itemDefinitionId,
        CancellationToken cancellationToken) => registry.ExecuteAsync(
            accountId,
            async (session, pinnedContent, now) =>
            {
                if (session.SessionId != sessionId)
                    return new CombatCommandResult(false, CombatErrorCodes.NotFound, session.Snapshot(), []);
                if (IsTraining(session.Snapshot()))
                    return new CombatCommandResult(false, CombatErrorCodes.CommandRejected, session.Snapshot(), []);
                if (session.HasProcessedCommand(commandId))
                    return new CombatCommandResult(false, CombatErrorCodes.DuplicateCommand, session.Snapshot(), []);

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
                        session.Snapshot(),
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
                        session.Snapshot(),
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
                        session.Snapshot(),
                        []);
                }

                string? inventoryError = await inventoryService.ConsumeOneForCombatAsync(
                    accountId,
                    itemDefinitionId,
                    contentSnapshot,
                    cancellationToken);
                if (inventoryError is not null)
                {
                    return new CombatCommandResult(
                        false,
                        CombatErrorCodes.CommandRejected,
                        session.Snapshot(),
                        []);
                }

                return session.Handle(
                    new UseConsumableCommand(
                        commandId,
                        definition.Id,
                        actions,
                        definition.ConsumableCooldownCategoryId,
                        cooldown),
                    now);
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

    public Task<CombatOperationResult> LeaveAsync(Guid accountId, CancellationToken cancellationToken) =>
        registry.LeaveAsync(accountId, cancellationToken);

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
        CancellationToken cancellationToken)
    {
        CombatSessionCreationResult created = await factory.CreateAsync(
            accountId,
            monsterId,
            locationId,
            cancellationToken);
        if (!created.Succeeded)
            return CombatOperationResult.Failure(created.ErrorCode!);
        if (!registry.TryAdd(
                accountId,
                created.CharacterId,
                created.Session!,
                created.ContentSnapshot))
            return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);
        return CombatOperationResult.FromSnapshot(
            created.Session!.Snapshot(),
            created.ContentSnapshot) with
        {
            Events = created.Session.GetEventsAfter(0)
        };
    }

    private Task<CombatOperationResult> ExecuteSessionCommand(
        Guid accountId, Guid sessionId, CombatCommand command, CancellationToken cancellationToken) =>
        registry.ExecuteAsync(accountId, (session, now) =>
            session.SessionId == sessionId
                ? session.Handle(command, now)
                : new CombatCommandResult(false, CombatErrorCodes.NotFound, session.Snapshot(), []),
            cancellationToken);

    private static bool IsTraining(CombatSessionSnapshot snapshot) =>
        string.Equals(
            snapshot.Enemy.DefinitionId,
            CombatSessionFactory.TrainingDummyId,
            StringComparison.Ordinal);
}
