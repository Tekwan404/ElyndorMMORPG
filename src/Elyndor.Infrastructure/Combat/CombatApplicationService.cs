using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.World;

namespace Elyndor.Infrastructure.Combat;

public sealed class CombatApplicationService(
    CombatSessionFactory factory,
    CombatSessionRegistry registry,
    WorldEncounterRegistry encounterRegistry,
    GameContentPackage content,
    InventoryEquipmentService inventoryService,
    CharacterOperationGuard operationGuard)
{
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
            (session, now) =>
            {
                if (session.SessionId != sessionId)
                    return new CombatCommandResult(false, CombatErrorCodes.NotFound,
                        session.Snapshot(), []);
                AbilityDefinition? ability = content.Abilities?.SingleOrDefault(candidate =>
                    string.Equals(candidate.Id, abilityId, StringComparison.Ordinal));
                Guid targetId = ability?.TargetType is AbilityTargetType.Self or AbilityTargetType.Owner
                    ? session.PlayerActorId
                    : session.EnemyActorId;
                return session.Handle(
                    new UseAbilityCommand(commandId, abilityId, targetId), now);
            }, cancellationToken);

    public Task<CombatOperationResult> UseConsumableAsync(
        Guid accountId,
        Guid sessionId,
        string commandId,
        string itemDefinitionId,
        CancellationToken cancellationToken) => registry.ExecuteAsync(
            accountId,
            async (session, now) =>
            {
                if (session.SessionId != sessionId)
                    return new CombatCommandResult(false, CombatErrorCodes.NotFound, session.Snapshot(), []);
                if (IsTraining(session.Snapshot()))
                    return new CombatCommandResult(false, CombatErrorCodes.CommandRejected, session.Snapshot(), []);
                if (session.HasProcessedCommand(commandId))
                    return new CombatCommandResult(false, CombatErrorCodes.DuplicateCommand, session.Snapshot(), []);

                ItemDefinition? definition = (content.Items ?? []).SingleOrDefault(candidate =>
                    string.Equals(candidate.Id, itemDefinitionId, StringComparison.Ordinal));
                if (definition is null || definition.Type != ItemType.Consumable || definition.HealAmount <= 0)
                    return new CombatCommandResult(false, CombatErrorCodes.CommandRejected, session.Snapshot(), []);

                string? validationError = session.ValidateConsumableUse(now, definition.HealAmount);
                if (validationError is not null)
                    return new CombatCommandResult(false, validationError, session.Snapshot(), []);

                string? inventoryError = await inventoryService.ConsumeOneForCombatAsync(
                    accountId,
                    itemDefinitionId,
                    cancellationToken);
                if (inventoryError is not null)
                    return new CombatCommandResult(false, CombatErrorCodes.CommandRejected, session.Snapshot(), []);

                return session.Handle(
                    new UseConsumableCommand(
                        commandId,
                        definition.Id,
                        definition.HealAmount,
                        TimeSpan.FromSeconds((double)definition.ConsumableCooldownSeconds)),
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

    public CombatOperationResult Resume(Guid accountId) => registry.Resume(accountId);

    public Task<CombatOperationResult> LeaveAsync(Guid accountId, CancellationToken cancellationToken) =>
        registry.LeaveAsync(accountId, cancellationToken);

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
        if (!registry.TryAdd(accountId, created.CharacterId, created.Session!))
            return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);
        return CombatOperationResult.FromSnapshot(created.Session!.Snapshot()) with
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
