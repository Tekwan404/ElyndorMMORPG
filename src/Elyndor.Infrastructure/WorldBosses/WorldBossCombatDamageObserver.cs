using System.Buffers.Binary;
using System.Security.Cryptography;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elyndor.Infrastructure.WorldBosses;

public sealed class WorldBossCombatDamageObserver(IServiceScopeFactory scopeFactory)
    : ICombatResultObserver
{
    public async Task ObserveAsync(
        CombatSession session,
        IReadOnlyList<CombatParticipantSnapshot> participants,
        IReadOnlyList<CombatEvent> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (events.Count == 0)
            return;

        Guid combatSessionId = session.SessionId;

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        GameDbContext db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        WorldBossCombatSessionBinding? binding = await db.WorldBossCombatSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CombatSessionId == combatSessionId,
                cancellationToken);
        if (binding is null)
            return;

        Guid[] characterIds = events
            .Where(combatEvent => combatEvent.Type == CombatEventType.DamageDealt
                && combatEvent.TargetActorId == binding.BossActorId
                && combatEvent.SourceActorId is not null)
            .Select(combatEvent =>
            {
                return session.TryResolveContributionCharacterId(
                    combatEvent.SourceActorId!.Value,
                    out Guid ownerCharacterId)
                        ? ownerCharacterId
                        : Guid.Empty;
            })
            .Where(characterId => characterId != Guid.Empty)
            .Distinct()
            .ToArray();
        Dictionary<Guid, Guid> currentPartyByCharacter = await db.PartyMembers
            .AsNoTracking()
            .Where(member => characterIds.Contains(member.CharacterId))
            .ToDictionaryAsync(
                member => member.CharacterId,
                member => member.PartyId,
                cancellationToken);
        WorldBossDamageService damageService =
            scope.ServiceProvider.GetRequiredService<WorldBossDamageService>();

        foreach (CombatEvent combatEvent in events)
        {
            if (combatEvent.Type != CombatEventType.DamageDealt
                || combatEvent.Amount <= 0
                || combatEvent.TargetActorId != binding.BossActorId
                || combatEvent.SourceActorId is not { } sourceActorId
                || !session.TryResolveContributionCharacterId(
                    sourceActorId,
                    out Guid characterId))
            {
                continue;
            }

            if (combatEvent.Sequence <= 0)
                throw new InvalidOperationException(
                    "Authoritative combat damage events must have a positive sequence.");

            WorldBossDamageCommitResult result = await damageService.ApplyDamageAsync(
                binding.SpawnId,
                characterId,
                combatSessionId,
                currentPartyByCharacter.TryGetValue(characterId, out Guid partyId)
                    ? partyId
                    : null,
                combatEvent.Amount,
                CreateMutationId(combatSessionId, combatEvent.Sequence),
                cancellationToken);

            if (result.Succeeded)
            {
                if (result.DefeatedNow)
                {
                    WorldBossSettlementBatchResult settlement =
                        await scope.ServiceProvider
                            .GetRequiredService<WorldBossSettlementService>()
                            .SettleAsync(binding.SpawnId, cancellationToken);
                    if (!settlement.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"World boss settlement failed with '{settlement.ErrorCode}'.");
                    }

                    break;
                }

                continue;
            }

            if (result.ErrorCode is WorldBossErrorCodes.AlreadyDefeated
                or WorldBossErrorCodes.Expired
                or WorldBossErrorCodes.NotActive)
            {
                break;
            }

            throw new InvalidOperationException(
                $"World boss damage bridge failed with '{result.ErrorCode}'.");
        }
    }

    internal static Guid CreateMutationId(Guid combatSessionId, long eventSequence)
    {
        if (combatSessionId == Guid.Empty)
            throw new ArgumentException("Combat session identifier cannot be empty.", nameof(combatSessionId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventSequence);

        Span<byte> input = stackalloc byte[24];
        combatSessionId.TryWriteBytes(input);
        BinaryPrimitives.WriteInt64LittleEndian(input[16..], eventSequence);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return new Guid(hash[..16]);
    }
}
