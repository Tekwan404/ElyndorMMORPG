using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.World;

namespace Elyndor.Infrastructure.Combat;

public sealed partial class CombatSessionRegistry
{
    public Guid[] OpenWorldAccounts() => _byAccount.Keys.Where(IsOpenWorldCombat).ToArray();

    public bool IsOpenWorldCombat(Guid accountId) => _byAccount.TryGetValue(accountId, out var entry)
        && entry.LocationId == OpenWorldPvpRules.LocationId && entry.Session.IsOpenWorldPvp
        && HasActiveCombat(accountId);

    public async Task<CombatOperationResult> JoinOpenWorldAsync(
        Guid targetAccountId, Guid accountId, Guid characterId,
        Func<GameContentSnapshot, CancellationToken, Task<CombatPlayerDefinition>> prepare,
        CombatDurabilityService durability, Guid? initialTargetActorId, CancellationToken cancellationToken)
    {
        if (!_byAccount.TryGetValue(targetAccountId, out var entry))
            return CombatOperationResult.Failure(CombatErrorCodes.NotFound);
        await AcquireGateAsync(entry, cancellationToken);
        bool reserved = false;
        bool journalCreated = false;
        bool admitted = false;
        try
        {
            await CompletePendingAsync(entry);
            if (!entry.Session.IsOpenWorldPvp || entry.LocationId != OpenWorldPvpRules.LocationId
                || entry.ContentSnapshot is null || !HasActiveCombat(targetAccountId)
                || entry.GetPublishedSnapshot(targetAccountId).Snapshot?.Player.Hp <= 0)
                return CombatOperationResult.Failure(CombatErrorCodes.InvalidLocation);

            var advanced = entry.Session.AdvanceTo(timeProvider.GetUtcNow());
            entry.PendingResult = advanced;
            await CompletePendingAsync(entry);
            if (entry.Session.Status != CombatSessionStatus.Active)
                return CombatOperationResult.Failure(CombatErrorCodes.Ended);
            lock (_indexGate)
            {
                if (_byAccount.ContainsKey(accountId) || _byCharacter.ContainsKey(characterId))
                    return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);
                _byAccount.TryAdd(accountId, entry);
                _byCharacter.TryAdd(characterId, entry);
                reserved = true;
            }
            var definition = await RunIoAsync(entry, "join-prepare",
                token => prepare(entry.ContentSnapshot, token), cancellationToken);
            if (definition.AccountId != accountId || definition.Participant.Actor.ActorId != characterId)
                return CombatOperationResult.Failure(CombatErrorCodes.CommandRejected);
            var journal = await RunIoAsync(entry, "join-journal",
                token => durability.BeginParticipantAsync(characterId, entry.Session.Snapshot(), token), cancellationToken);
            journalCreated = journal.Created;
            if (!journal.Succeeded)
                return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);
            long sequence = entry.Session.Sequence;
            if (!entry.Session.TryJoinOpenWorldParticipant(definition, entry.Session.CurrentTimeUtc, out var error))
                return CombatOperationResult.Failure(error ?? CombatErrorCodes.CommandRejected);
            entry.AddBinding(new(accountId, characterId));
            admitted = true;
            if (initialTargetActorId is { } targetId)
                entry.Session.Handle(characterId, new SelectTargetCommand(Guid.NewGuid().ToString(), targetId), entry.Session.CurrentTimeUtc);
            entry.Session.Handle(characterId, new StartAutoAttackCommand(Guid.NewGuid().ToString()), entry.Session.CurrentTimeUtc);
            var joined = new CombatCommandResult(true, null, entry.Session.Snapshot(characterId),
                entry.Session.GetEventsAfter(sequence));
            entry.PendingResult = joined;
            ScheduleGameplay(entry);
            await CompletePendingAsync(entry);
            return CombatOperationResult.From(joined, entry.ContentSnapshot);
        }
        catch
        {
            if (admitted) ScheduleRecovery(entry);
            throw;
        }
        finally
        {
            try
            {
                if (!admitted)
                {
                    if (reserved)
                    {
                        lock (_indexGate)
                        {
                            _byAccount.TryRemove(new KeyValuePair<Guid, SessionEntry>(accountId, entry));
                            _byCharacter.TryRemove(new KeyValuePair<Guid, SessionEntry>(characterId, entry));
                        }
                    }
                    if (journalCreated)
                    {
                        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await durability.RemoveParticipantAsync(entry.Session.SessionId, characterId, cleanup.Token);
                    }
                    ScheduleGameplay(entry);
                }
            }
            finally { entry.Gate.Release(); }
        }
    }
}
