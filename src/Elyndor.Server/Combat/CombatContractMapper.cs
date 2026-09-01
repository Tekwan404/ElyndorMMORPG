using Elyndor.Contracts.Combat;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Infrastructure.Combat;

namespace Elyndor.Server.Combat;

internal static class CombatContractMapper
{
    public static CombatUpdateResponse ToResponse(CombatOperationResult result) => new(
        result.Succeeded,
        result.ErrorCode,
        result.Snapshot is null ? null : ToResponse(result.Snapshot),
        result.Events.Select(ToResponse).ToArray());

    private static CombatSnapshotResponse ToResponse(CombatSessionSnapshot snapshot) => new(
        snapshot.SessionId,
        snapshot.Sequence,
        snapshot.Status.ToString(),
        snapshot.ServerTimeUtc,
        ToResponse(snapshot.Player),
        ToResponse(snapshot.Enemy));

    private static CombatActorResponse ToResponse(CombatActorSnapshot actor) => new(
        actor.ActorId,
        actor.Kind.ToString(),
        actor.DefinitionId,
        actor.Name,
        actor.Hp,
        actor.MaxHp,
        actor.ResourceType,
        actor.Resource,
        actor.MaxResource,
        actor.AutoAttackEnabled,
        actor.Cooldowns,
        actor.KnownAbilityIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
        actor.Abilities.Select(ability => new CombatAbilityResponse(
            ability.Id, ability.ResourceCost, ability.Cooldown.TotalSeconds)).ToArray(),
        actor.Effects.Select(effect => new CombatEffectResponse(
            effect.Id, effect.Stacks, effect.ExpiresAtUtc)).ToArray());

    private static CombatEventResponse ToResponse(CombatEvent combatEvent) => new(
        combatEvent.Sequence,
        combatEvent.Type.ToString(),
        combatEvent.ActorId,
        combatEvent.SourceActorId,
        combatEvent.TargetActorId,
        combatEvent.DefinitionId,
        combatEvent.Amount,
        combatEvent.OccurredAtUtc);
}
