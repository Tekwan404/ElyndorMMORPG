using Elyndor.Contracts.Combat;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Combat;

namespace Elyndor.Server.Combat;

internal static class CombatContractMapper
{
    public static CombatUpdateResponse ToResponse(
        CombatOperationResult result,
        GameContentPackage content) => new(
        result.Succeeded,
        result.ErrorCode,
        result.Snapshot is null ? null : ToResponse(result.Snapshot, content),
        result.Events.Select(ToResponse).ToArray(),
        result.Reward?.Progression is null
            ? null
            : new CombatRewardResponse(
                result.Reward.XpEarned,
                result.Reward.GoldEarned,
                result.Reward.Progression.LeveledUp,
                result.Reward.Progression.PreviousLevel,
                result.Reward.Progression.CurrentLevel,
                result.Reward.Items.Select(item => new CombatRewardItemResponse(
                    item.ItemId,
                    item.Name,
                    item.Type.ToString(),
                    item.Rarity.ToString(),
                    item.Quantity)).ToArray()));

    private static CombatSnapshotResponse ToResponse(
        CombatSessionSnapshot snapshot,
        GameContentPackage content) => new(
        snapshot.SessionId,
        snapshot.Sequence,
        snapshot.Status.ToString(),
        snapshot.ServerTimeUtc,
        ToResponse(snapshot.Player, content),
        ToResponse(snapshot.Enemy, content));

    private static CombatActorResponse ToResponse(
        CombatActorSnapshot actor,
        GameContentPackage content)
    {
        MonsterDefinition? monster = actor.Kind == CombatActorKind.Monster
            ? content.Monsters?.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, actor.DefinitionId, StringComparison.Ordinal))
            : null;

        return new CombatActorResponse(
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
            actor.Abilities.Select(ability =>
            {
                var definition = (content.Abilities ?? []).SingleOrDefault(candidate =>
                    string.Equals(candidate.Id, ability.Id, StringComparison.Ordinal));
                return new CombatAbilityResponse(
                    ability.Id,
                    definition?.DisplayName ?? ability.Id,
                    definition?.Description ?? string.Empty,
                    definition?.IconId,
                    ability.ResourceCost,
                    ability.Cooldown.TotalSeconds);
            }).ToArray(),
            actor.Effects.Select(effect => new CombatEffectResponse(
                effect.Id, effect.Stacks, effect.ExpiresAtUtc)).ToArray(),
            monster?.Level ?? 1,
            monster?.ArtId);
    }

    private static CombatEventResponse ToResponse(CombatEvent combatEvent) => new(
        combatEvent.Sequence,
        combatEvent.Type.ToString(),
        combatEvent.ActorId,
        combatEvent.SourceActorId,
        combatEvent.TargetActorId,
        combatEvent.DefinitionId,
        combatEvent.Amount,
        combatEvent.OccurredAtUtc,
        combatEvent.AmountBeforeShields);
}
